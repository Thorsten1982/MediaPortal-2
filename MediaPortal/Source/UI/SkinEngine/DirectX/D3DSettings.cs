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

using SlimDX.Direct3D9;

namespace MediaPortal.UI.SkinEngine.DirectX
{
  /// <summary>
  /// Current D3D settings: adapter, device, mode, formats, etc.
  /// </summary>
  public class D3DSettings
  {
    public bool IsWindowed;

    public GraphicsAdapterInfo WindowedAdapterInfo;
    public GraphicsDeviceInfo WindowedDeviceInfo;
    public DeviceCombo WindowedDeviceCombo;
    public DisplayMode WindowedDisplayMode; // not changable by the user
    public Format WindowedDepthStencilBufferFormat;
    public MultisampleType WindowedMultisampleType;
    public int WindowedMultisampleQuality;
    public VertexProcessingType WindowedVertexProcessingType;
    public PresentInterval WindowedPresentInterval;
    public int BackBufferCount = 4; // 2 to 4 are recommended for FlipEx swap mode
    public int WindowedWidth;
    public int WindowedHeight;

    public GraphicsAdapterInfo FullscreenAdapterInfo;
    public GraphicsDeviceInfo FullscreenDeviceInfo;
    public DeviceCombo FullscreenDeviceCombo;
    public DisplayMode[] FullscreenDisplayModes = new DisplayMode[100];
    public int CurrentFullscreenDisplayMode = 0;
    public int DesktopDisplayMode = 0;
    public Format FullscreenDepthStencilBufferFormat;
    public MultisampleType FullscreenMultisampleType;
    public int FullscreenMultisampleQuality;
    public VertexProcessingType FullscreenVertexProcessingType;
    public PresentInterval FullscreenPresentInterval;

    public GraphicsAdapterInfo AdapterInfo
    {
      get { return IsWindowed ? WindowedAdapterInfo : FullscreenAdapterInfo; }
      set
      {
        if (IsWindowed)
          WindowedAdapterInfo = value;
        else
          FullscreenAdapterInfo = value;
      }
    }

    public GraphicsDeviceInfo DeviceInfo
    {
      get { return IsWindowed ? WindowedDeviceInfo : FullscreenDeviceInfo; }
      set
      {
        if (IsWindowed)
          WindowedDeviceInfo = value;
        else
          FullscreenDeviceInfo = value;
      }
    }

    public DeviceCombo DeviceCombo
    {
      get { return IsWindowed ? WindowedDeviceCombo : FullscreenDeviceCombo; }
      set
      {
        if (IsWindowed)
          WindowedDeviceCombo = value;
        else
          FullscreenDeviceCombo = value;
      }
    }

    public int AdapterOrdinal
    {
      get { return DeviceCombo.AdapterOrdinal; }
    }

    public DeviceType DevType
    {
      get { return DeviceCombo.DevType; }
    }

    public Format BackBufferFormat
    {
      get { return DeviceCombo.BackBufferFormat; }
    }

    public DisplayMode DisplayMode
    {
      get { return IsWindowed ? WindowedDisplayMode : FullscreenDisplayModes[CurrentFullscreenDisplayMode]; }
    }

    public Format DepthStencilBufferFormat
    {
      get { return IsWindowed ? WindowedDepthStencilBufferFormat : FullscreenDepthStencilBufferFormat; }
      set
      {
        if (IsWindowed)
          WindowedDepthStencilBufferFormat = value;
        else
          FullscreenDepthStencilBufferFormat = value;
      }
    }

    public MultisampleType MultisampleType
    {
      get { return IsWindowed ? WindowedMultisampleType : FullscreenMultisampleType; }
      set
      {
        if (IsWindowed)
          WindowedMultisampleType = value;
        else
          FullscreenMultisampleType = value;
      }
    }

    public int MultisampleQuality
    {
      get { return IsWindowed ? WindowedMultisampleQuality : FullscreenMultisampleQuality; }
      set
      {
        if (IsWindowed)
          WindowedMultisampleQuality = value;
        else
          FullscreenMultisampleQuality = value;
      }
    }

    public VertexProcessingType VertexProcessingType
    {
      get { return IsWindowed ? WindowedVertexProcessingType : FullscreenVertexProcessingType; }
      set
      {
        if (IsWindowed)
          WindowedVertexProcessingType = value;
        else
          FullscreenVertexProcessingType = value;
      }
    }

    public PresentInterval PresentInterval
    {
      get { return IsWindowed ? WindowedPresentInterval : FullscreenPresentInterval; }
      set
      {
        if (IsWindowed)
          WindowedPresentInterval = value;
        else
          FullscreenPresentInterval = value;
      }
    }

    public D3DSettings Clone()
    {
      return (D3DSettings) MemberwiseClone();
    }
  }
}
