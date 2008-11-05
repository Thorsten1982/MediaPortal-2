#region Copyright (C) 2007-2008 Team MediaPortal

/*
 *  Copyright (C) 2007-2008 Team MediaPortal
 *  http://www.team-mediaportal.com
 *
 *  This file is part of MediaPortal II
 *
 *  MediaPortal II is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  MediaPortal II is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with MediaPortal II.  If not, see <http://www.gnu.org/licenses/>.
 * 
 */

#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using MediaPortal.Configuration.Settings.Regional;
using MediaPortal.Core;
using MediaPortal.Presentation.DataObjects;
using MediaPortal.Presentation.Localisation;
using MediaPortal.Configuration.Settings;

namespace Components.Configuration.Settings.Regional
{
  public class MainLanguage : SingleSelectionList
  {

    #region Variables

    private CultureInfo[] _cultures;

    #endregion

    #region Constructors

    public MainLanguage()
    {
      // Nothing to register
    }

    #endregion

    #region Public properties

    public override Type SettingsObjectType
    {
      get { return null; }
    }

    #endregion

    #region Public Methods

    public override void Load(object settingsObject)
    {
      _cultures = ServiceScope.Get<ILocalisation>().AvailableLanguages();
      CultureInfo current = ServiceScope.Get<ILocalisation>().CurrentCulture;
      // Fill items
      List<IResourceString> items = new List<IResourceString>(_cultures.Length);
      for (int i = 0; i < _cultures.Length; i++)
        items.Add(LocalizationHelper.CreateLabelProperty(_cultures[i].DisplayName));
      items.Sort();
      _items = items;
      // Find index to select after sorting
      for (int i = 0; i < _cultures.Length; i++)
      {
        if (_cultures[i].Name == current.Name)
        {
          Selected = i;
          break;
        }
      }
    }

    public override void Save(object settingsObject)
    {
      ServiceScope.Get<ILocalisation>().ChangeLanguage(_cultures[Selected].Name);
    }

    public override void Apply()
    {
      ServiceScope.Get<ILocalisation>().ChangeLanguage(_cultures[Selected].Name);
    }

    #endregion

  }
}