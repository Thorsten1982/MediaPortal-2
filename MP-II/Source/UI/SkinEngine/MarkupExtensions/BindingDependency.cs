#region Copyright (C) 2007-2008 Team MediaPortal

/*
    Copyright (C) 2007-2008 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal II

    MediaPortal II is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal II is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using MediaPortal.SkinEngine.Controls.Visuals;
using MediaPortal.SkinEngine.Xaml;

namespace MediaPortal.SkinEngine.MarkupExtensions
{
  /// <summary>
  /// Handles the dependency between two data endpoints.
  /// </summary>
  public class BindingDependency
  {
    protected IDataDescriptor _sourceDd;
    protected IDataDescriptor _targetDd;
    protected bool _attachedToSource = false;
    protected bool _attachedToTarget = false;
    protected UIElement _attachedToLostFocus = null;
    protected bool _negate = false;

    /// <summary>
    /// Creates a new <see cref="BindingDependency"/> object.
    /// </summary>
    /// <param name="sourceDd">Souce data descriptor for the dependency.</param>
    /// <param name="targetDd">Target data descriptor for the dependency.</param>
    /// <param name="autoAttachToSource">If set to <c>true</c>, the new dependency object will be
    /// automatically attached to the <paramref name="sourceDd"/> data descriptor. This means it will
    /// capture changes from it and reflect them on the <paramref name="targetDd"/> data descriptor.</param>
    /// <param name="updateSourceTrigger">This parameter controls, which target object event makes this
    /// binding dependency copy the target value to the <paramref name="sourceDd"/> data descriptor.
    /// If set to <see cref="UpdateSourceTrigger.PropertyChanged"/>, the new binding dependency object
    /// will automatically attach to property changes of the <paramref name="targetDd"/> data descriptor and
    /// reflect the changed value to the <paramref name="sourceDd"/> data descriptor. If set to
    /// <see cref="UpdateSourceTrigger.LostFocus"/>, the new binding dependency will attach to the
    /// <see cref="UIElement.EventOccured"/> event of the <paramref name="targetParent"/> object.
    /// If set to <see cref="UpdateSourceTrigger.Explicit"/>, the new binding dependency won't attach to
    /// the target at all.</param>
    /// <param name="targetParent">This parameter is only necessary if <paramref name="updateSourceTrigger"/>
    /// is set to <see cref="UpdateSourceTrigger.LostFocus"/>. It specifies the parent UI object of the
    /// specified <paramref name="targetDd"/> data descriptor.</param>
    /// <param name="negate">If set to <c>true</c>, the bool value will be converted between source and
    /// target updates.</param>
    public BindingDependency(
        IDataDescriptor sourceDd, IDataDescriptor targetDd,
        bool autoAttachToSource, UpdateSourceTrigger updateSourceTrigger,
        UIElement targetParent, bool negate)
    {
      _sourceDd = sourceDd;
      _targetDd = targetDd;
      _negate = negate;
      if (autoAttachToSource && sourceDd.SupportsChangeNotification)
      {
        sourceDd.Attach(OnSourceChanged);
        _attachedToSource = true;
      }
      if (targetDd.SupportsChangeNotification)
      {
        if (updateSourceTrigger == UpdateSourceTrigger.PropertyChanged)
        {
          targetDd.Attach(OnTargetChanged);
          _attachedToTarget = true;
        }
        else if (updateSourceTrigger == UpdateSourceTrigger.LostFocus)
        {
          if (targetParent != null)
            targetParent.EventOccured += OnTargetElementEventOccured;
          _attachedToLostFocus = targetParent;
        }
      }
      // Initially update endpoints
      if (autoAttachToSource)
        UpdateTarget();
      if (updateSourceTrigger != UpdateSourceTrigger.Explicit &&
          !autoAttachToSource) // If we are attached to both, only update one direction
        UpdateSource();
    }

    protected void OnTargetElementEventOccured(string eventName)
    {
      if (eventName == FrameworkElement.LOSTFOCUS_EVENT)
        UpdateSource();
    }

    protected void OnSourceChanged(IDataDescriptor source)
    {
      UpdateTarget();
    }

    protected void OnTargetChanged(IDataDescriptor target)
    {
      UpdateSource();
    }

    public void Detach()
    {
      if (_attachedToSource)
        _sourceDd.Detach(OnSourceChanged);
      _attachedToSource = false;
      if (_attachedToTarget)
        _targetDd.Detach(OnTargetChanged);
      _attachedToTarget = false;
      if (_attachedToLostFocus != null)
        _attachedToLostFocus.EventOccured -= OnTargetElementEventOccured;
    }

    public void UpdateSource()
    {
      object newValue;
      if (!TypeConverter.Convert(_targetDd.Value, _sourceDd.DataType, out newValue))
        return;
      if (_negate)
        newValue = !(bool)newValue;
      if (_sourceDd.Value == newValue)
        return;
      _sourceDd.Value = newValue;
    }

    public void UpdateTarget()
    {
      object value = _sourceDd.Value;
      if (_negate && value != null)
      {
        // If negate, we need to convert to bool
        if (!TypeConverter.Convert(value, typeof(bool), out value))
          return;
        if (_negate)
          value = !(bool) value;
      }
      if (!TypeConverter.Convert(value, _targetDd.DataType, out value))
        return;
      if (_targetDd.Value == value)
        return;
      _targetDd.Value = value;
    }
  }
}