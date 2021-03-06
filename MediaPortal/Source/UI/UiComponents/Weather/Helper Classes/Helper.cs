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

using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MediaPortal.UiComponents.Weather
{
  public class Helper
  {
    /// <summary>
    /// dll Import to check Internet Connection
    /// </summary>
    /// <param name="description"></param>
    /// <param name="reservedValue"></param>
    /// <returns></returns>
    [DllImport("wininet.dll")]
    private static extern bool InternetGetConnectedState(out int description, int reservedValue);

    /// <summary>
    /// check if we have an Internetconnection
    /// </summary>
    /// <param name="code"></param>
    /// <returns>true if Internetconnection is available</returns>
    public static bool IsConnectedToInternet(ref int code)
    {
      return InternetGetConnectedState(out code, 0);
    }

    /// <summary>
    /// this will take the CityProviderInfo Object and turn it to a
    /// City object by adding empty data
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public static City CityInfoToCityObject(CitySetupInfo info)
    {
      return new City(info);
    }

    /// <summary>
    /// this will create a new list of cities, that
    /// already hold the providerinformation
    /// </summary>
    /// <param name="cpiList"></param>
    /// <returns></returns>
    public static List<City> CityInfoListToCityObjectList(List<CitySetupInfo> cpiList)
    {
      List<City> cList = new List<City>();
      foreach (CitySetupInfo cpi in cpiList)
        cList.Add(new City(cpi));
      return cList;
    }
  }
}
