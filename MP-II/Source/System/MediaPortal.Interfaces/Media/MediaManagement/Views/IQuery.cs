﻿#region Copyright (C) 2007-2008 Team MediaPortal

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

using System.Collections.Generic;

namespace MediaPortal.Media.MediaManagement.Views
{
  public enum SortOrder
  {
    None,
    Ascending,
    Descending
  }

  /// <summary>
  /// Holds the data for one (of many) sort field. Stores the name of the field to be sorted and the
  /// sort direction.
  /// </summary>
  public struct SortInformation
  {
    public string FieldName;
    public SortOrder SortOrder;
  }

  /// <summary>
  /// Specifieds a query to be evaluated on the media library database. The evaluation of this query
  /// will return a set of media items. A query might be executable on a media database or not, depending
  /// on the requested table data.
  /// </summary>
  public interface IQuery
  {
    int ClipSize { get; set; }

    IList<SortInformation> SortInformations { get;set; }

    // TODO: Getters/setters/accessors for query data and result data
  }
}