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

using System.Drawing;
using MediaPortal.Common.General;
using MediaPortal.UI.Control.InputManager;
using MediaPortal.UI.SkinEngine.Commands;
using MediaPortal.UI.SkinEngine.MpfElements;
using MediaPortal.UI.SkinEngine.ScreenManagement;
using MediaPortal.Utilities.DeepCopy;

namespace MediaPortal.UI.SkinEngine.Controls.Visuals
{
  public class KeyBinding : FrameworkElement
  {
    #region Protected fields

    protected AbstractProperty _keyProperty;
    protected AbstractProperty _commandProperty;

    protected Screen _registeredScreen = null;
    protected Key _registeredKey = null;

    #endregion

    #region Ctor

    public KeyBinding()
    {
      Init();
      Attach();

      IsVisible = false;
    }

    void Init()
    {
      _keyProperty = new SProperty(typeof(Key), null);
      _commandProperty = new SProperty(typeof(IExecutableCommand), null);
    }

    void Attach()
    {
      _keyProperty.Attach(OnBindingConcerningPropertyChanged);
      IsEnabledProperty.Attach(OnBindingConcerningPropertyChanged);
      ScreenProperty.Attach(OnBindingConcerningPropertyChanged);
    }

    void Detach()
    {
      _keyProperty.Detach(OnBindingConcerningPropertyChanged);
      IsEnabledProperty.Detach(OnBindingConcerningPropertyChanged);
      ScreenProperty.Detach(OnBindingConcerningPropertyChanged);
    }

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      Detach();
      base.DeepCopy(source, copyManager);
      KeyBinding kb = (KeyBinding) source;
      Key = kb.Key;
      Command = copyManager.GetCopy(kb.Command);
      Attach();
    }

    public override void Dispose()
    {
      UnregisterKeyBinding();
      MPF.TryCleanupAndDispose(Command);
      base.Dispose();
    }

    #endregion

    #region Private & protected members

    protected override SizeF CalculateInnerDesiredSize(SizeF totalSize)
    {
      return SizeF.Empty;
    }

    void OnBindingConcerningPropertyChanged(AbstractProperty prop, object oldValue)
    {
      UnregisterKeyBinding();
      RegisterKeyBinding();
    }

    protected void Execute()
    {
      if (Command != null)
        Command.Execute();
    }

    protected void RegisterKeyBinding()
    {
      if (Key == null)
        return;
      Screen screen = Screen;
      if (IsEnabled && screen != null)
      {
        _registeredScreen = screen;
        _registeredKey = Key;
        _registeredScreen.AddKeyBinding(_registeredKey, Execute);
      }
    }

    protected void UnregisterKeyBinding()
    {
      if (_registeredScreen != null && _registeredKey != null)
        _registeredScreen.RemoveKeyBinding(_registeredKey);
      _registeredScreen = null;
      _registeredKey = null;
    }

    #endregion

    #region Public properties

    public Key Key
    {
      get { return (Key) _keyProperty.GetValue(); }
      set { _keyProperty.SetValue(value); }
    }

    public AbstractProperty KeyProperty
    {
      get { return _keyProperty; }
    }

    public AbstractProperty CommandProperty
    {
      get { return _commandProperty; }
      set { _commandProperty = value; }
    }

    public IExecutableCommand Command
    {
      get { return (IExecutableCommand) _commandProperty.GetValue(); }
      set { _commandProperty.SetValue(value); }
    }

    #endregion
  }
}
