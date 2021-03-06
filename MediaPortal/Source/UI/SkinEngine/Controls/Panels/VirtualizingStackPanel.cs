#region Copyright (C) 2007-2011 Team MediaPortal

/*
    Copyright (C) 2007-2011 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MediaPortal.UI.SkinEngine.MpfElements;
using MediaPortal.UI.SkinEngine.ScreenManagement;
using MediaPortal.Utilities;
using MediaPortal.UI.SkinEngine.Controls.Visuals;
using MediaPortal.Utilities.DeepCopy;

namespace MediaPortal.UI.SkinEngine.Controls.Panels
{
  public class VirtualizingStackPanel : StackPanel
  {
    #region Consts

    /// <summary>
    /// Number of items to keep in the invisible range before disposing items.
    /// </summary>
    public const int INVISIBLE_KEEP_THRESHOLD = 100;

    /// <summary>
    /// We have to cope with the situation where our items have all a DesiredSize of 0 because they first need some render
    /// cycles to set their values to be able to calculate their size. To avoid that we iterate through the complete
    /// collection and only finding 0 sized items, we limit the maximum number of items to <see cref="MAX_NUM_VISIBLE_ITEMS"/>.
    /// </summary>
    public const int MAX_NUM_VISIBLE_ITEMS = 50;

    #endregion

    #region Protected fields

    protected IItemProvider _itemProvider = null;

    // Assigned in Arrange
    protected int _arrangedItemsStartIndex = 0;
    protected IList<FrameworkElement> _arrangedItems = new List<FrameworkElement>();

    // Assigned in CalculateInnerDesiredSize
    protected float _averageItemSize = 0;

    protected IItemProvider _newItemProvider = null; // Store new item provider until next render cylce

    #endregion

    #region Ctor

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      base.DeepCopy(source, copyManager);
      VirtualizingStackPanel p = (VirtualizingStackPanel) source;
      _itemProvider = copyManager.GetCopy(p._itemProvider);
      _arrangedItems.Clear();
      _averageItemSize = 0;
    }

    public override void Dispose()
    {
      base.Dispose();
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider != null)
        MPF.TryCleanupAndDispose(itemProvider);
    }

    #endregion

    #region Public properties

    public IItemProvider ItemProvider
    {
      get { return _itemProvider; }
      set
      {
        if (_itemProvider != value)
        {
          if (_elementState == ElementState.Running)
            lock (Children.SyncRoot)
            {
              if (_newItemProvider == value)
                return;
              if (_newItemProvider != null)
                MPF.TryCleanupAndDispose(_newItemProvider);
              _newItemProvider = value;
            }
          else
          {
            if (_newItemProvider != value && _newItemProvider != null)
              MPF.TryCleanupAndDispose(_newItemProvider);
            if (_itemProvider != null)
              MPF.TryCleanupAndDispose(_itemProvider);
            _itemProvider = value;
          }
          InvalidateLayout(true, true);
        }
      }
    }

    public bool IsVirtualizing
    {
      get { return _itemProvider != null; }
    }

    #endregion

    #region Layouting

    public override void SetScrollIndex(int childIndex, bool first)
    {
      // Albert, 2010-12-28: We need to override this method because we need to lock on Children.SyncRoot
      lock (Children.SyncRoot)
      {
        if (_pendingScrollIndex == childIndex && _scrollToFirst == first ||
            (!_pendingScrollIndex.HasValue &&
             ((_scrollToFirst && _actualFirstVisibleChild == childIndex) ||
              (!_scrollToFirst && _actualLastVisibleChild == childIndex))))
          return;
        _pendingScrollIndex = childIndex;
        _scrollToFirst = first;
      }
      InvalidateLayout(true, true);
      InvokeScrolled();
    }

    // It's actually "GetVisibleChildren", but that member already exists in Panel
    protected IList<FrameworkElement> GetMeasuredViewableChildren(SizeF totalSize, out SizeF resultSize)
    {
      resultSize = SizeF.Empty;
      IList<FrameworkElement> result = new List<FrameworkElement>(20);
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
        return result;

      int numItems = itemProvider.NumItems;
      if (numItems == 0)
        return result;
      float availableSize = GetExtendsInNonOrientationDirection(totalSize);
      if (!_doScroll)
        _actualFirstVisibleChild = 0;
      int start = _actualFirstVisibleChild;
      Bound(ref start, 0, numItems - 1);
      int end = start - 1;
      float sumExtendsInOrientationDirection = 0;
      float maxExtendsInNonOrientationDirection = 0;

      int ct = MAX_NUM_VISIBLE_ITEMS;

      // From scroll index until potentially up to the end
      do
      {
        if (end == numItems - 1)
          // Reached the last item
          break;
        FrameworkElement item = GetItem(end + 1, itemProvider, true);
        if (item == null || !item.IsVisible)
          continue;
        if (ct-- == 0)
          break;
        float childExtendsInOrientationDirection = GetExtendsInOrientationDirection(item.DesiredSize);
        if (childExtendsInOrientationDirection > availableSize + DELTA_DOUBLE)
          break;
        float childExtendsInNonOrientationDirection = GetExtendsInNonOrientationDirection(item.DesiredSize);
        availableSize -= childExtendsInOrientationDirection;
        sumExtendsInOrientationDirection += childExtendsInOrientationDirection;
        if (childExtendsInNonOrientationDirection > maxExtendsInNonOrientationDirection)
          maxExtendsInNonOrientationDirection = childExtendsInNonOrientationDirection;
        result.Add(item);
        end++;
      } while (availableSize > 0 || !_doScroll);
      // If there is still space left, try to get items above scroll index
      while (availableSize > 0)
      {
        if (start == 0)
          // Reached the last item
          break;
        FrameworkElement item = GetItem(start - 1, itemProvider, true);
        if (item == null || !item.IsVisible)
          continue;
        if (ct-- == 0)
          break;
        float childExtendsInOrientationDirection = GetExtendsInOrientationDirection(item.DesiredSize);
        if (childExtendsInOrientationDirection > availableSize + DELTA_DOUBLE)
          break;
        float childExtendsInNonOrientationDirection = GetExtendsInNonOrientationDirection(item.DesiredSize);
        availableSize -= childExtendsInOrientationDirection;
        sumExtendsInOrientationDirection += childExtendsInOrientationDirection;
        if (childExtendsInNonOrientationDirection > maxExtendsInNonOrientationDirection)
          maxExtendsInNonOrientationDirection = childExtendsInNonOrientationDirection;
        result.Insert(0, item);
        start--;
      }
      resultSize = Orientation == Orientation.Vertical ? new SizeF(maxExtendsInNonOrientationDirection, sumExtendsInOrientationDirection) :
          new SizeF(sumExtendsInOrientationDirection, maxExtendsInNonOrientationDirection);
      return result;
    }

    protected override SizeF CalculateInnerDesiredSize(SizeF totalSize)
    {
      FrameworkElementCollection children = Children;
      lock (children.SyncRoot)
      {
        if (_newItemProvider != null)
        {
          if (children.Count > 0)
          children.Clear(false);
          if (_itemProvider != null)
            MPF.TryCleanupAndDispose(_itemProvider);
          _itemProvider = _newItemProvider;
        }
        _averageItemSize = 0;
        IItemProvider itemProvider = ItemProvider;
        if (itemProvider == null)
          return base.CalculateInnerDesiredSize(totalSize);
        int numItems = itemProvider.NumItems;
        if (numItems == 0)
          return SizeF.Empty;

        SizeF resultSize;
        // Get all viewable children (= visible children inside our range)
        IList<FrameworkElement> exemplaryChildren = GetMeasuredViewableChildren(totalSize, out resultSize);
        if (exemplaryChildren.Count == 0)
        { // Might be the case if no item matches into totalSize. Fallback: Use the first visible item.
          for (int i = 0; i < numItems; i++)
          {
            FrameworkElement item = GetItem(i, itemProvider, true);
            if (item == null || !item.IsVisible)
              continue;
            exemplaryChildren.Add(item);
          }
        }
        if (exemplaryChildren.Count == 0)
          return SizeF.Empty;
        _averageItemSize = GetExtendsInOrientationDirection(resultSize) / exemplaryChildren.Count;
        return Orientation == Orientation.Vertical ? new SizeF(resultSize.Height * numItems / exemplaryChildren.Count, resultSize.Width) :
            new SizeF(resultSize.Height, resultSize.Width * numItems / exemplaryChildren.Count);
      }
    }

    protected FrameworkElement GetItem(int childIndex, IItemProvider itemProvider, bool forceMeasure)
    {
      lock (Children.SyncRoot)
      {
        bool newlyCreated;
        FrameworkElement item = itemProvider.GetOrCreateItem(childIndex, this, out newlyCreated);
        if (item == null)
          return null;
        if (newlyCreated)
          // VisualParent and item.Screen were set by the item provider
          item.SetElementState(_elementState == ElementState.Running ? ElementState.Running : ElementState.Preparing);
        if (newlyCreated || forceMeasure)
        {
          SizeF childSize = Orientation == Orientation.Vertical ? new SizeF((float) ActualWidth, float.NaN) :
              new SizeF(float.NaN, (float) ActualHeight);
          item.Measure(ref childSize);
        }
        return item;
      }
    }

    protected override void ArrangeChildren()
    {
      lock (Children.SyncRoot)
      {
        _arrangedItemsStartIndex = -1;
        IItemProvider itemProvider = ItemProvider;
        if (itemProvider == null)
        {
          base.ArrangeChildren();
          return;
        }

        _totalHeight = 0;
        _totalWidth = 0;
        int numItems = itemProvider.NumItems;
        if (numItems > 0)
        {
          SizeF actualSize = new SizeF((float) ActualWidth, (float) ActualHeight);

          // For Orientation == vertical, this is ActualHeight, for horizontal it is ActualWidth
          float actualExtendsInOrientationDirection = GetExtendsInOrientationDirection(actualSize);
          // For Orientation == vertical, this is ActualWidth, for horizontal it is ActualHeight
          float actualExtendsInNonOrientationDirection = GetExtendsInNonOrientationDirection(actualSize);
          // If set to true, we'll check available space from the last to first visible child.
          // That is necessary if we want to scroll a specific child to the last visible position.
          bool invertLayouting = false;
          if (_pendingScrollIndex.HasValue)
          {
            int pendingSI = _pendingScrollIndex.Value;
            Bound(ref pendingSI, 0, numItems - 1);
            if (_scrollToFirst)
              _actualFirstVisibleChild = pendingSI;
            else
            {
              _actualLastVisibleChild = pendingSI;
              invertLayouting = true;
            }
            _pendingScrollIndex = null;
          }

          // 1) Calculate scroll indices
          if (_doScroll)
          {
            float spaceLeft = actualExtendsInOrientationDirection;
            if (invertLayouting)
            {
              Bound(ref _actualLastVisibleChild, 0, numItems - 1);
              _actualFirstVisibleChild = _actualLastVisibleChild + 1;
              int ct = MAX_NUM_VISIBLE_ITEMS;
              for (int i = _actualLastVisibleChild; i >= 0; i--)
              {
                FrameworkElement item = GetItem(i, itemProvider, true);
                if (item == null || !item.IsVisible)
                  continue;
                if (ct-- == 0)
                  break;
                spaceLeft -= GetExtendsInOrientationDirection(item.DesiredSize);
                if (spaceLeft + DELTA_DOUBLE < 0)
                  break; // Found item which is not visible any more
                _actualFirstVisibleChild = i;
              }
              if (spaceLeft > 0)
              { // We need to correct the last scroll index
                for (int i = _actualLastVisibleChild + 1; i < numItems; i++)
                {
                  FrameworkElement item = GetItem(i, itemProvider, true);
                  if (item == null || !item.IsVisible)
                    continue;
                  if (ct-- == 0)
                    break;
                  spaceLeft -= GetExtendsInOrientationDirection(item.DesiredSize);
                  if (spaceLeft + DELTA_DOUBLE < 0)
                    break; // Found item which is not visible any more
                  _actualLastVisibleChild = i;
                }
              }
            }
            else
            {
              Bound(ref _actualFirstVisibleChild, 0, numItems - 1);
              _actualLastVisibleChild = _actualFirstVisibleChild - 1;
              int ct = MAX_NUM_VISIBLE_ITEMS;
              for (int i = _actualFirstVisibleChild; i < numItems; i++)
              {
                FrameworkElement item = GetItem(i, itemProvider, true);
                if (item == null || !item.IsVisible)
                  continue;
                if (ct-- == 0)
                  break;
                spaceLeft -= GetExtendsInOrientationDirection(item.DesiredSize);
                if (spaceLeft + DELTA_DOUBLE < 0)
                  break; // Found item which is not visible any more
                _actualLastVisibleChild = i;
              }
              if (spaceLeft > 0)
              { // We need to correct the first scroll index
                for (int i = _actualFirstVisibleChild - 1; i >= 0; i--)
                {
                  FrameworkElement item = GetItem(i, itemProvider, true);
                  if (item == null || !item.IsVisible)
                    continue;
                  if (ct-- == 0)
                    break;
                  spaceLeft -= GetExtendsInOrientationDirection(item.DesiredSize);
                  if (spaceLeft + DELTA_DOUBLE < 0)
                    break; // Found item which is not visible any more
                  _actualFirstVisibleChild = i;
                }
              }
            }
          }
          else
          {
            _actualFirstVisibleChild = 0;
            _actualLastVisibleChild = numItems - 1;
          }

          // 2) Arrange children
          if (Orientation == Orientation.Vertical)
            _totalWidth = actualExtendsInNonOrientationDirection;
          else
            _totalHeight = actualExtendsInNonOrientationDirection;
          _arrangedItems.Clear();

          _arrangedItemsStartIndex = _actualFirstVisibleChild;
          // Heavy scrolling works best with at least two times the number of visible items arranged above and below
          // our visible children. That was tested out. If someone has a better heuristic, please use it here.
          int numArrangeAroundViewport = ((int) (actualExtendsInOrientationDirection / _averageItemSize) + 1) * NUM_ADD_MORE_FOCUS_ELEMENTS;
          // Elements before _actualFirstVisibleChild
          float startOffset = 0;
          for (int i = _actualFirstVisibleChild - 1; i >= 0 && i >= _actualFirstVisibleChild - numArrangeAroundViewport; i--)
          {
            FrameworkElement item = GetItem(i, itemProvider, true);
            if (item == null || !item.IsVisible)
              continue;
            SizeF childSize = new SizeF(item.DesiredSize);
            // For Orientation == vertical, this is childSize.Height, for horizontal it is childSize.Width
            float desiredExtendsInOrientationDirection = GetExtendsInOrientationDirection(childSize);
            startOffset -= desiredExtendsInOrientationDirection;
            if (Orientation == Orientation.Vertical)
            {
              PointF position = new PointF(ActualPosition.X, ActualPosition.Y + startOffset);

              childSize.Width = actualExtendsInNonOrientationDirection;

              ArrangeChildHorizontal(item, item.HorizontalAlignment, ref position, ref childSize);
              item.Arrange(new RectangleF(position, childSize));
              _totalHeight += desiredExtendsInOrientationDirection;
            }
            else
            {
              PointF position = new PointF(ActualPosition.X + startOffset, ActualPosition.Y);

              childSize.Height = actualExtendsInNonOrientationDirection;

              ArrangeChildVertical(item, item.VerticalAlignment, ref position, ref childSize);
              item.Arrange(new RectangleF(position, childSize));
              _totalWidth += desiredExtendsInOrientationDirection;
            }
            _arrangedItems.Insert(0, item);
            _arrangedItemsStartIndex = i;
          }

          startOffset = 0;
          // Elements from _actualFirstVisibleChild to _actualLastVisibleChild + _numArrangeAroundViewport
          for (int i = _actualFirstVisibleChild; i < numItems && i <= _actualLastVisibleChild + numArrangeAroundViewport; i++)
          {
            FrameworkElement item = GetItem(i, itemProvider, true);
            if (item == null || !item.IsVisible)
              continue;
            SizeF childSize = new SizeF(item.DesiredSize);
            // For Orientation == vertical, this is childSize.Height, for horizontal it is childSize.Width
            float desiredExtendsInOrientationDirection = GetExtendsInOrientationDirection(childSize);
            if (Orientation == Orientation.Vertical)
            {
              PointF position = new PointF(ActualPosition.X, ActualPosition.Y + startOffset);

              childSize.Width = actualExtendsInNonOrientationDirection;

              ArrangeChildHorizontal(item, item.HorizontalAlignment, ref position, ref childSize);
              item.Arrange(new RectangleF(position, childSize));
              _totalHeight += desiredExtendsInOrientationDirection;

              startOffset += desiredExtendsInOrientationDirection;
            }
            else
            {
              PointF position = new PointF(ActualPosition.X + startOffset, ActualPosition.Y);

              childSize.Height = actualExtendsInNonOrientationDirection;

              ArrangeChildVertical(item, item.VerticalAlignment, ref position, ref childSize);
              item.Arrange(new RectangleF(position, childSize));
              _totalWidth += desiredExtendsInOrientationDirection;

              startOffset += desiredExtendsInOrientationDirection;
            }
            _arrangedItems.Add(item);
          }
          int numInvisible = numItems - _arrangedItems.Count;
          if (Orientation == Orientation.Vertical)
            _totalHeight += numInvisible * _averageItemSize;
          else
            _totalWidth += numInvisible * _averageItemSize;

          itemProvider.Keep(_arrangedItemsStartIndex - INVISIBLE_KEEP_THRESHOLD,
              _arrangedItemsStartIndex + _arrangedItems.Count + INVISIBLE_KEEP_THRESHOLD);
        }
        else
        {
          _arrangedItemsStartIndex = 0;
          _actualFirstVisibleChild = 0;
          _actualLastVisibleChild = -1;
        }
      }
    }

    protected override void MakeChildVisible(UIElement element, ref RectangleF elementBounds)
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
      {
        base.MakeVisible(element, elementBounds);
        return;
      }

      if (_doScroll)
      {
        IList<FrameworkElement> arrangedItemsCopy;
        int arrangedStart;
        int oldFirstViewableChild;
        int oldLastViewableChild;
        lock (Children.SyncRoot)
        {
          arrangedItemsCopy = new List<FrameworkElement>(_arrangedItems);
          arrangedStart = _arrangedItemsStartIndex;
          oldFirstViewableChild = _actualFirstVisibleChild - arrangedStart;
          oldLastViewableChild = _actualLastVisibleChild - arrangedStart;
        }
        if (arrangedStart < 0)
          return;
        int index = 0;
        foreach (FrameworkElement currentChild in arrangedItemsCopy)
        {
          if (InVisualPath(currentChild, element))
          {
            bool first;
            if (index < oldFirstViewableChild)
              first = true;
            else if (index <= oldLastViewableChild)
              break;
            else
              first = false;
            SetScrollIndex(index + arrangedStart, first);
            // Adjust the scrolled element's bounds; Calculate the difference between positions of childen at old/new child indices
            if (Orientation == Orientation.Horizontal)
              elementBounds.X -= (float) SumActualWidths(arrangedItemsCopy, first ? oldFirstViewableChild : oldLastViewableChild, index);
            else
              elementBounds.Y -= (float) SumActualHeights(arrangedItemsCopy, first ? oldFirstViewableChild : oldLastViewableChild, index);
            break;
          }
          index++;
        }
      }
    }

    public override FrameworkElement GetElement(int index)
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
        return base.GetElement(index);

      lock (Children.SyncRoot)
        return GetItem(index, itemProvider, true);
    }

    public override void AddChildren(ICollection<UIElement> childrenOut)
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
      {
        base.AddChildren(childrenOut);
        return;
      }

      lock (Children.SyncRoot)
        CollectionUtils.AddAll(childrenOut, _arrangedItems);
    }

    public override bool IsChildRenderedAt(UIElement child, float x, float y)
    {
      if (_doScroll)
      { // If we can scroll, check if child is completely in our range -> if not, it won't be rendered and thus isn't visible
        RectangleF elementBounds = ((FrameworkElement) child).ActualBounds;
        RectangleF bounds = ActualBounds;
        if (elementBounds.Right > bounds.Right + DELTA_DOUBLE) return false;
        if (elementBounds.Left < bounds.Left - DELTA_DOUBLE) return false;
        if (elementBounds.Top < bounds.Top - DELTA_DOUBLE) return false;
        if (elementBounds.Bottom > bounds.Bottom + DELTA_DOUBLE) return false;
      }
      return base.IsChildRenderedAt(child, x, y);
    }

    #endregion

    #region Rendering

    protected override IEnumerable<FrameworkElement> GetRenderedChildren()
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
        return base.GetRenderedChildren();

      return _arrangedItems.Skip(_actualFirstVisibleChild - _arrangedItemsStartIndex).
          Take(_actualLastVisibleChild - _actualFirstVisibleChild + 1);
    }

    #endregion

    #region Base overrides

    public override void AlignedPanelAddPotentialFocusNeighbors(RectangleF? startingRect, ICollection<FrameworkElement> elements,
        bool elementsBeforeAndAfter)
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
      {
        base.AlignedPanelAddPotentialFocusNeighbors(startingRect, elements, elementsBeforeAndAfter);
        return;
      }
      if (!IsVisible)
        return;
      if (Focusable)
        elements.Add(this);
      int first;
      int last;
      IList<FrameworkElement> arrangedItemsCopy;
      lock (Children.SyncRoot)
      {
        arrangedItemsCopy = new List<FrameworkElement>(_arrangedItems);
        first = _actualFirstVisibleChild - _arrangedItemsStartIndex;
        last = _actualLastVisibleChild - _arrangedItemsStartIndex;
      }
      int numElementsBeforeAndAfter = elementsBeforeAndAfter ? NUM_ADD_MORE_FOCUS_ELEMENTS : 0;
      AddFocusedElementRange(arrangedItemsCopy, startingRect, first, last,
          numElementsBeforeAndAfter, numElementsBeforeAndAfter, elements);
    }

    protected override void SaveChildrenState(IDictionary<string, object> state, string prefix)
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
        base.SaveChildrenState(state, prefix);
      else
      {
        IList<FrameworkElement> arrangedItemsCopy;
        int index;
        lock (Children.SyncRoot)
        {
          arrangedItemsCopy = new List<FrameworkElement>(_arrangedItems);
          index = _arrangedItemsStartIndex;
        }
        state[prefix + "/ItemsStartIndex"] = index;
        state[prefix + "/NumItems"] = arrangedItemsCopy.Count;
        foreach (FrameworkElement child in arrangedItemsCopy)
          child.SaveUIState(state, prefix + "/Child_" + (index++));
      }
    }

    public override void RestoreChildrenState(IDictionary<string, object> state, string prefix)
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
        base.RestoreChildrenState(state, prefix);
      else
      {
        object oNumItems;
        object oIndex;
        int? numItems;
        int? startIndex;
        if (state.TryGetValue(prefix + "/ItemsStartIndex", out oIndex) && state.TryGetValue(prefix + "/NumItems", out oNumItems) &&
            (startIndex = (int?) oIndex).HasValue && (numItems = (int?) oNumItems).HasValue)
        {
          int numRestoreItems = Math.Max(numItems.Value, itemProvider.NumItems);
          for (int i = 0; i < numRestoreItems; i++)
          {
            FrameworkElement child = GetItem(startIndex.Value + i, itemProvider, false);
            if (child == null)
              continue;
            child.RestoreUIState(state, prefix + "/Child_" + i);
          }
        }
      }
    }

    public override bool FocusPageUp()
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
        return base.FocusPageUp();

      if (Orientation == Orientation.Vertical)
      {
        FrameworkElement currentElement = GetFocusedElementOrChild();
        if (currentElement == null)
          return false;

        int firstLocal;
        int firstVisibleChildIndex;
        int numItems;
        IList<FrameworkElement> localChildren;
        lock (Children.SyncRoot) // We must aquire the children's lock when accessing the _renderOrder
        {
          firstLocal = _actualFirstVisibleChild - _arrangedItemsStartIndex;
          firstVisibleChildIndex = _actualFirstVisibleChild;
          numItems = itemProvider.NumItems;
          localChildren = new List<FrameworkElement>(_arrangedItems);
          if (localChildren.Count == 0)
            return false;
          Bound(ref firstLocal, 0, localChildren.Count - 1);
        }
        FrameworkElement firstVisibleChild = localChildren[firstLocal];
        if (firstVisibleChild == null)
          return false;
        if (_averageItemSize == 0)
          return false;
        if (InVisualPath(firstVisibleChild, currentElement))
        { // The topmost element is focused - move one page up
          int index = (int) (ActualHeight/_averageItemSize) - 1;
          LowerBound(ref index, 1);
          index = firstVisibleChildIndex - index;
          Bound(ref index, 0, numItems - 1);
          SetScrollIndex(index, true);
          FrameworkElement item = GetItem(index, itemProvider, false);
          if (item != null)
            item.SetFocusPrio = SetFocusPriority.Default;
          return true;
        }
        // An element inside our visible range is focused - move to first element
        float limitPosition = ActualPosition.Y;
        FrameworkElement nextElement;
        while ((nextElement = FindNextFocusElement(localChildren, currentElement.ActualBounds, MoveFocusDirection.Up)) != null &&
            (nextElement.ActualPosition.Y > limitPosition - DELTA_DOUBLE))
          currentElement = nextElement;
        return currentElement.TrySetFocus(true);
      }
      return false;
    }

    public override bool FocusPageDown()
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
        return base.FocusPageDown();

      if (Orientation == Orientation.Vertical)
      {
        FrameworkElement currentElement = GetFocusedElementOrChild();
        if (currentElement == null)
          return false;

        int lastLocal;
        int lastVisibleChildIndex;
        int numItems;
        IList<FrameworkElement> localChildren;
        lock (Children.SyncRoot) // We must aquire the children's lock when accessing the _renderOrder
        {
          lastLocal = _actualLastVisibleChild - _arrangedItemsStartIndex;
          lastVisibleChildIndex = _actualLastVisibleChild;
          numItems = itemProvider.NumItems;
          localChildren = new List<FrameworkElement>(_arrangedItems);
          if (localChildren.Count == 0)
            return false;
          Bound(ref lastLocal, 0, localChildren.Count - 1);
        }
        FrameworkElement lastVisibleChild = localChildren[lastLocal];
        if (lastVisibleChild == null)
          return false;
        if (_averageItemSize == 0)
          return false;
        if (InVisualPath(lastVisibleChild, currentElement))
        { // The element at the bottom is focused - move one page down
          int index = (int) (ActualHeight/_averageItemSize) - 1;
          LowerBound(ref index, 1);
          index = lastVisibleChildIndex + index;
          Bound(ref index, 0, numItems - 1);
          SetScrollIndex(index, false);
          FrameworkElement item = GetItem(index, itemProvider, false);
          if (item != null)
            item.SetFocusPrio = SetFocusPriority.Default;
          return true;
        }
        // An element inside our visible range is focused - move to last element
        float limitPosition = ActualPosition.Y + (float) ActualHeight;
        FrameworkElement nextElement;
        while ((nextElement = FindNextFocusElement(localChildren, currentElement.ActualBounds, MoveFocusDirection.Down)) != null &&
            (nextElement.ActualBounds.Bottom < limitPosition + DELTA_DOUBLE))
          currentElement = nextElement;
        return currentElement.TrySetFocus(true);
      }
      return false;
    }

    public override bool FocusPageLeft()
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
        return base.FocusPageLeft();

      if (Orientation == Orientation.Horizontal)
      {
        FrameworkElement currentElement = GetFocusedElementOrChild();
        if (currentElement == null)
          return false;

        int firstLocal;
        int firstVisibleChildIndex;
        int numItems;
        IList<FrameworkElement> localChildren;
        lock (Children.SyncRoot) // We must aquire the children's lock when accessing the _renderOrder
        {
          firstLocal = _actualFirstVisibleChild - _arrangedItemsStartIndex;
          firstVisibleChildIndex = _actualFirstVisibleChild;
          numItems = itemProvider.NumItems;
          localChildren = new List<FrameworkElement>(_arrangedItems);
          if (localChildren.Count == 0)
            return false;
          Bound(ref firstLocal, 0, localChildren.Count - 1);
        }
        FrameworkElement firstVisibleChild = localChildren[firstLocal];
        if (firstVisibleChild == null)
          return false;
        if (_averageItemSize == 0)
          return false;
        if (InVisualPath(firstVisibleChild, currentElement))
        { // The leftmost element is focused - move one page left
          int index = (int) (ActualWidth/_averageItemSize) - 1;
          LowerBound(ref index, 1);
          index = firstVisibleChildIndex - index;
          Bound(ref index, 0, numItems - 1);
          SetScrollIndex(index, true);
          FrameworkElement item = GetItem(index, itemProvider, false);
          if (item != null)
            item.SetFocusPrio = SetFocusPriority.Default;
          return true;
        }
        // An element inside our visible range is focused - move to first element
        float limitPosition = ActualPosition.X;
        FrameworkElement nextElement;
        while ((nextElement = FindNextFocusElement(localChildren, currentElement.ActualBounds, MoveFocusDirection.Left)) != null &&
            (nextElement.ActualPosition.X > limitPosition - DELTA_DOUBLE))
          currentElement = nextElement;
        return currentElement.TrySetFocus(true);
      }
      return false;
    }

    public override bool FocusPageRight()
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
        return base.FocusPageRight();

      if (Orientation == Orientation.Horizontal)
      {
        FrameworkElement currentElement = GetFocusedElementOrChild();
        if (currentElement == null)
          return false;

        int lastLocal;
        int lastVisibleChildIndex;
        int numItems;
        IList<FrameworkElement> localChildren;
        lock (Children.SyncRoot) // We must aquire the children's lock when accessing the _renderOrder
        {
          lastLocal = _actualLastVisibleChild - _arrangedItemsStartIndex;
          lastVisibleChildIndex = _actualLastVisibleChild;
          numItems = itemProvider.NumItems;
          localChildren = new List<FrameworkElement>(_arrangedItems);
          if (localChildren.Count == 0)
            return false;
          Bound(ref lastLocal, 0, localChildren.Count - 1);
        }
        FrameworkElement lastVisibleChild = localChildren[lastLocal];
        if (lastVisibleChild == null)
          return false;
        if (_averageItemSize == 0)
          return false;
        if (InVisualPath(lastVisibleChild, currentElement))
        { // The element at the bottom is focused - move one page down
          int index = (int) (ActualWidth/_averageItemSize) - 1;
          LowerBound(ref index, 1);
          index = lastVisibleChildIndex + index;
          Bound(ref index, 0, numItems - 1);
          SetScrollIndex(index, false);
          FrameworkElement item = GetItem(index, itemProvider, false);
          if (item != null)
            item.SetFocusPrio = SetFocusPriority.Default;
          return true;
        }
        // An element inside our visible range is focused - move to last element
        float limitPosition = ActualPosition.X + (float) ActualWidth;
        FrameworkElement nextElement;
        while ((nextElement = FindNextFocusElement(localChildren, currentElement.ActualBounds, MoveFocusDirection.Right)) != null &&
            (nextElement.ActualBounds.Right < limitPosition - DELTA_DOUBLE))
          currentElement = nextElement;
        return currentElement.TrySetFocus(true);
      }
      return false;
    }

    public override bool FocusHome()
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
        return base.FocusHome();

      lock (Children.SyncRoot)
      {
        if (itemProvider.NumItems == 0)
          return false;
        FrameworkElement item = GetItem(0, itemProvider, true);
        if (item != null)
          item.SetFocusPrio = SetFocusPriority.Default;
      }
      SetScrollIndex(0, true);
      return true;
    }

    public override bool FocusEnd()
    {
      IItemProvider itemProvider = ItemProvider;
      if (itemProvider == null)
        return base.FocusHome();

      int numItems;
      lock (Children.SyncRoot)
      {
        numItems = itemProvider.NumItems;
        if (numItems == 0)
          return false;
        FrameworkElement item = GetItem(numItems - 1, itemProvider, true);
        if (item != null)
          item.SetFocusPrio = SetFocusPriority.Default;
      }
      SetScrollIndex(numItems - 1, false);
      return true;
    }

    public override float ViewPortStartX
    {
      get
      {
        IItemProvider itemProvider = ItemProvider;
        if (itemProvider == null)
          return base.ViewPortStartX;

        if (Orientation == Orientation.Vertical)
          return 0;
        int firstVisibleChildIndex = _actualFirstVisibleChild;
        return firstVisibleChildIndex == 0 ? 0 : (firstVisibleChildIndex - 1) * _averageItemSize;
      }
    }

    public override float ViewPortStartY
    {
      get
      {
        IItemProvider itemProvider = ItemProvider;
        if (itemProvider == null)
          return base.ViewPortStartY;

        if (Orientation == Orientation.Horizontal)
          return 0;
        int firstVisibleChildIndex = _actualFirstVisibleChild;
        return firstVisibleChildIndex * _averageItemSize;
      }
    }

    public override bool IsViewPortAtTop
    {
      get
      {
        IItemProvider itemProvider = ItemProvider;
        if (itemProvider == null)
          return base.IsViewPortAtTop;

        if (Orientation == Orientation.Horizontal)
          return true;
        return _actualFirstVisibleChild == 0;
      }
    }

    public override bool IsViewPortAtBottom
    {
      get
      {
        IItemProvider itemProvider = ItemProvider;
        if (itemProvider == null)
          return base.IsViewPortAtBottom;

        if (Orientation == Orientation.Horizontal)
          return true;
        return _actualLastVisibleChild == itemProvider.NumItems - 1;
      }
    }

    public override bool IsViewPortAtLeft
    {
      get
      {
        IItemProvider itemProvider = ItemProvider;
        if (itemProvider == null)
          return base.IsViewPortAtLeft;

        if (Orientation == Orientation.Vertical)
          return true;
        return _actualFirstVisibleChild == 0;
      }
    }

    public override bool IsViewPortAtRight
    {
      get
      {
        IItemProvider itemProvider = ItemProvider;
        if (itemProvider == null)
          return base.IsViewPortAtRight;

        if (Orientation == Orientation.Vertical)
          return true;
        return _actualLastVisibleChild == itemProvider.NumItems - 1;
      }
    }

    #endregion
  }
}
