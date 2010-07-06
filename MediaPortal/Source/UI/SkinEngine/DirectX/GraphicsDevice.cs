#region Copyright (C) 2007-2010 Team MediaPortal

/*
    Copyright (C) 2007-2010 Team MediaPortal
    http://www.team-mediaportal.com
 
    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MediaPortal.Core;
using MediaPortal.Core.Logging;
using MediaPortal.UI.Presentation.Screens;
using MediaPortal.UI.SkinEngine.ContentManagement;
using MediaPortal.UI.SkinEngine.Players;
using MediaPortal.UI.SkinEngine.ScreenManagement;
using SlimDX;
using SlimDX.Direct3D9;

namespace MediaPortal.UI.SkinEngine.DirectX
{
  public class GraphicsDevice : IDisposable
  {
    #region Variables

    private static readonly D3DSetup _setup = new D3DSetup();
    private static DeviceEx _device;
    private static Surface _backBuffer;
    private static bool _deviceLost = false;
    private static int _anisotropy;
    private static bool _supportsFiltering;
    private static bool _supportsAlphaBlend;
    private static bool _supportsShaders = false;
    private static bool _firstTimeInitialisation = true;
    private static bool _firstTimeInitialisationMemory = true;

    #endregion

    public static Matrix TransformWorld;
    public static Matrix TransformView;
    public static Matrix TransformProjection;
    public static Matrix FinalTransform;

    public GraphicsDevice(Form window, bool maximize)
    {
      if (_firstTimeInitialisationMemory)
      {
        _firstTimeInitialisationMemory = false;
#if NOTUSED
        Microsoft.DirectX.DirectDraw.Device tmpDev = new Microsoft.DirectX.DirectDraw.Device(Microsoft.DirectX.DirectDraw.CreateFlags.HardwareOnly);
        Microsoft.DirectX.DirectDraw.GetCapsStruct caps = tmpDev.GetCaps();
        tmpDev.Dispose();
        tmpDev = null;

        int videoMemory = caps.HardwareCaps.VideoMemoryTotal / (1000 * 1000);
        ServiceScope.Get<ILogger>().Info("DirectX: Total Video Memory: {0} MB", videoMemory);
        if (videoMemory < 128)
        {
          string text = String.Format("MediaPortal 2 needs a graphics card with at least 128 MB video memory\nYour card does only has {0} MB.\nMediaportal-II will continue but migh run slow", videoMemory);
          MessageBox.Show(text, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
#endif
      }
      try
      {
        ServiceScope.Get<ILogger>().Debug("GraphicsDevice: Initialize DirectX");
        MPDirect3D.Load();
        _setup.SetupDirectX(window, maximize);
        _backBuffer = _device.GetRenderTarget(0);
        int ordinal = Device.Capabilities.AdapterOrdinal;
        AdapterInformation adapterInfo = MPDirect3D.Direct3D.Adapters[ordinal];
        ServiceScope.Get<ILogger>().Info("GraphicsDevice: DirectX initialized {0}x{1} (format: {2} {3} Hz)", Width,
            Height, adapterInfo.CurrentDisplayMode.Format,
            adapterInfo.CurrentDisplayMode.RefreshRate);
        GetCapabilities();
      }
      catch (Exception ex)
      {
        ServiceScope.Get<ILogger>().Critical("GraphicsDevice: Failed to set-up DirectX", ex);
        Environment.Exit(0);
      }
    }

    public static bool DeviceLost
    {
      get { return _deviceLost; }
      set { _deviceLost = value; }
    }

    private static void GetCapabilities()
    {
      _anisotropy = _device.Capabilities.MaxAnisotropy;
      _supportsFiltering = MPDirect3D.Direct3D.CheckDeviceFormat(
          _device.Capabilities.AdapterOrdinal,
          _device.Capabilities.DeviceType,
          _device.GetDisplayMode(0).Format,
          Usage.RenderTarget | Usage.QueryFilter, ResourceType.Texture,
          Format.A8R8G8B8);

      _supportsAlphaBlend = MPDirect3D.Direct3D.CheckDeviceFormat(_device.Capabilities.AdapterOrdinal,
          _device.Capabilities.DeviceType, _device.GetDisplayMode(0).Format,
          Usage.RenderTarget | Usage.QueryPostPixelShaderBlending,
          ResourceType.Surface, Format.A8R8G8B8);
      int vertexShaderVersion = Device.Capabilities.VertexShaderVersion.Major;
      int pixelShaderVersion = Device.Capabilities.PixelShaderVersion.Major;
      ServiceScope.Get<ILogger>().Info("DirectX: Pixel shader support: {0}.{1}", Device.Capabilities.PixelShaderVersion.Major, Device.Capabilities.PixelShaderVersion.Minor);
      ServiceScope.Get<ILogger>().Info("DirectX: Vertex shader support: {0}.{1}", Device.Capabilities.VertexShaderVersion.Major, Device.Capabilities.VertexShaderVersion.Minor);
      if (pixelShaderVersion >= 2 && vertexShaderVersion >= 2)
        _supportsShaders = true;
      else
        _supportsShaders = false;
      if (_firstTimeInitialisation)
      {
        _firstTimeInitialisation = false;
        if (pixelShaderVersion < 2 || vertexShaderVersion < 2)
        {
          string text = String.Format("MediaPortal 2 needs a graphics card wich supports shader model 2.0\nYour card does NOT support this.\nMediaportal-II will continue but migh run slow");
          MessageBox.Show(text, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
      }
    }

    /// <summary>
    /// Resets the DirectX device.
    /// </summary>
    public static bool Reset()
    {
      ServiceScope.Get<ILogger>().Debug("GraphicsDevice: Reset DirectX, {0} {1}", ContentManager.TextureReferences, ContentManager.VertexReferences);
      if (ContentManager.TextureReferences == 0 && ContentManager.VertexReferences == 0)
      {
        if (_backBuffer != null)
          _backBuffer.Dispose();
        _backBuffer = null;
        _setup.ResetDevice();
        int ordinal = Device.Capabilities.AdapterOrdinal;
        AdapterInformation adapterInfo = MPDirect3D.Direct3D.Adapters[ordinal];
        ServiceScope.Get<ILogger>().Debug("GraphicsDevice: DirectX reset {0}x{1} format: {2} {3} Hz", Width, Height,
            adapterInfo.CurrentDisplayMode.Format,
            adapterInfo.CurrentDisplayMode.RefreshRate);
        _backBuffer = _device.GetRenderTarget(0);
        GetCapabilities();
      }
      else
        ServiceScope.Get<ILogger>().Error("GraphicsDevice: Cannot reset DirectX due to pending texture- or vertex-references (#texture references: {0}, #vertex references: {1})", ContentManager.TextureReferences, ContentManager.VertexReferences);
      return true;
    }

    public static bool IsWindowed
    {
      get { return _setup.Windowed; }
    }

    #region IDisposable Members

    /// <summary>
    /// Disposes this instance.
    /// </summary>
    public void Dispose()
    {
      if (_backBuffer != null)
        _backBuffer.Dispose();
      _backBuffer = null;

      if (_device != null)
        _device.Dispose();
      _device = null;
      MPDirect3D.Unload();
    }

    #endregion

    /// <summary>
    /// Gets or sets the DirectX Device.
    /// </summary>
    public static DeviceEx Device
    {
      get { return _device; }
      set { _device = value; }
    }

    /// <summary>
    /// Gets the directx back-buffer width.
    /// </summary>
    public static int Width
    {
      get { return _setup.PresentParameters.BackBufferWidth; }
    }

    /// <summary>
    /// Gets the directx back-buffer height.
    /// </summary>
    public static int Height
    {
      get { return _setup.PresentParameters.BackBufferHeight; }
    }

    public static bool SupportsShaders
    {
      get { return _supportsShaders; }
    }

    /// <summary>
    /// Sets the directx render states and project matrices.
    /// </summary>
    public static void SetRenderState()
    {
      Device.SetRenderState(RenderState.CullMode, Cull.None);
      Device.SetRenderState(RenderState.Lighting, false);

      Device.SetRenderState(RenderState.ZEnable, false);
      Device.SetRenderState(RenderState.ZWriteEnable, false);

      Device.SetRenderState(RenderState.FillMode, FillMode.Solid);
      Device.SetRenderState(RenderState.AlphaBlendEnable, true);
      Device.SetRenderState(RenderState.SourceBlend, Blend.SourceAlpha);
      Device.SetRenderState(RenderState.DestinationBlend, Blend.InverseSourceAlpha);
      Device.SetTextureStageState(0, TextureStage.ColorOperation, TextureOperation.Modulate);
      Device.SetTextureStageState(0, TextureStage.ColorArg0, TextureArgument.Texture);
      Device.SetTextureStageState(0, TextureStage.ColorArg1, TextureArgument.Diffuse);
      Device.SetTextureStageState(0, TextureStage.AlphaOperation, TextureOperation.Modulate);
      Device.SetTextureStageState(0, TextureStage.AlphaArg0, TextureArgument.Texture);
      Device.SetTextureStageState(0, TextureStage.AlphaArg1, TextureArgument.Diffuse);

      //Device.SetTextureStageState(1, TextureStage.ColorOperation, TextureOperation.Modulate);
      //Device.SetTextureStageState(1, TextureStage.ColorArg0, TextureArgument.Texture);
      //Device.SetTextureStageState(1, TextureStage.ColorArg1, TextureArgument.Current);
      //Device.SetTextureStageState(1, TextureStage.AlphaOperation, TextureOperation.Modulate);
      //Device.SetTextureStageState(1, TextureStage.AlphaArg0, TextureArgument.Texture);
      //Device.SetTextureStageState(1, TextureStage.AlphaArg1, TextureArgument.Current);

      if (_supportsAlphaBlend)
      {
        Device.SetRenderState(RenderState.AlphaTestEnable, true);
        Device.SetRenderState(RenderState.AlphaRef, 0x01);
        Device.SetRenderState(RenderState.AlphaFunc, Compare.GreaterEqual);
      }
      if (_supportsFiltering)
      {
        /* DX9 supports the following filter combinations
         * Filter       Minification Magnifcation Mipmap
         * None                  No            No    Yes
         * Point                Yes           Yes    Yes
         * Linear               Yes           Yes    Yes
         * Anisotropic          Yes           Yes     No
         * Flat Cubic            No           Yes     No
         * Gaussian Cubic        No           Yes     No
         */
        try
        {
          Device.SetSamplerState(0, SamplerState.MinFilter, TextureFilter.Anisotropic);
          Device.SetSamplerState(0, SamplerState.MagFilter, TextureFilter.Anisotropic);
          Device.SetSamplerState(0, SamplerState.MipFilter, TextureFilter.Linear);
          Device.SetSamplerState(0, SamplerState.MaxAnisotropy, _anisotropy);
        }
        catch (Exception)
        {
          _supportsFiltering = false;
        }
      }
      else
      {
        Device.SetSamplerState(0, SamplerState.MinFilter, TextureFilter.Point);
        Device.SetSamplerState(0, SamplerState.MagFilter, TextureFilter.Point);
        Device.SetSamplerState(0, SamplerState.MipFilter, TextureFilter.Point);
      }

      int gw = Width;
      int gh = Height;
      Point camera = new Point(gw / 2, gh / 2);
      // And calculate the offset from the screen center
      Point offset = new Point(camera.X - (gw / 2), camera.Y - (gh / 2));

      // Grab the viewport dimensions and location
      Viewport viewport = Device.Viewport;
      float w = Width * 0.5f; // viewport.Width * 0.5f;
      float h = Height * 0.5f; // viewport.Height * 0.5f;

      //Matrix mtxWorld = Matrix.Identity;
      //GraphicsDevice.TransformWorld = mtxWorld;

      // Camera view. Multiply the Y coord by -1) { translate so that everything is relative to the camera position.
      Matrix flipY = Matrix.Scaling(1.0f, -1.0f, 1.0f);
      Matrix translate = Matrix.Translation(-(viewport.X + w + offset.X), -(viewport.Y + h + offset.Y), 2 * h);
      Matrix mtxView = Matrix.Multiply(translate, flipY);
      TransformView = mtxView;

      // projection onto screen space
      Matrix mtxProjection = Matrix.PerspectiveOffCenterLH(
          (-w - offset.X) * 0.5f, //Minimum x-value of the view volume.
          (w - offset.X) * 0.5f, //Maximum x-value of the view volume.
          (-h + offset.Y) * 0.5f, //Minimum y-value of the view volume.
          (h + offset.Y) * 0.5f, //Maximum y-value of the view volume.
          h, //Minimum z-value of the view volume.
          100 * h); //Maximum z-value of the view volume.
      TransformProjection = mtxProjection;

      FinalTransform = TransformView * TransformProjection;
      ////GraphicsDevice.TransformWorld = Matrix.Identity;
      //GraphicsDevice.TransformView = Matrix.LookAtLH(new Vector3(0, 0, -10.0f), new Vector3(0.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f));
      //GraphicsDevice.TransformProjection = Matrix.PerspectiveFovLH((float)Math.PI / 4.0f, 1.0f, 1.0f, 100.0f);
    }

    /// <summary>
    /// Renders the entire scene.
    /// </summary>
    /// <returns><c>true</c>, if the caller should wait some milliseconds before rendering the next time.</returns>
    public static bool Render()
    {
      if (_device == null || _deviceLost)
        return true;
      lock (_setup)
      {
        try
        {
          _device.Clear(ClearFlags.Target, Color.Black, 1.0f, 0);

          _device.BeginScene();

          ScreenManager manager = (ScreenManager) ServiceScope.Get<IScreenManager>();
          manager.Render();

          _device.EndScene();
          // TODO: Present.DoNotWait could be used to yield CPU time (no waiting for v-sync on CPU, only with GPU)
          _device.PresentEx(Present.None);
        }
        catch (Direct3D9Exception e)
        {
          ServiceScope.Get<ILogger>().Warn("GraphicsDevice: Lost DirectX device", e);
          _deviceLost = true;
          return true;
        }
        ContentManager.Clean();
      }
      return false;
    }

    public static bool ReclaimDevice()
    {
      if (_backBuffer != null)
      {
        _backBuffer.Dispose();

        _backBuffer = null;
        PlayersHelper.ReleaseGUIResources();
        ContentManager.Free();
      }

      Result result = _device.TestCooperativeLevel();

      if (result == ResultCode.DeviceNotReset)
      {
        ServiceScope.Get<ILogger>().Warn("GraphicsDevice: Aquired DirectX device");
        try
        {
          ServiceScope.Get<ILogger>().Warn("GraphicsDevice: Device reset");
          _setup.ResetDevice();
          int ordinal = Device.Capabilities.AdapterOrdinal;
          AdapterInformation adapterInfo = MPDirect3D.Direct3D.Adapters[ordinal];
          ServiceScope.Get<ILogger>().Debug("GraphicsDevice: DirectX reset {0}x{1} format: {2} {3} Hz", Width, Height,
              adapterInfo.CurrentDisplayMode.Format,
              adapterInfo.CurrentDisplayMode.RefreshRate);
          _backBuffer = _device.GetRenderTarget(0);
          PlayersHelper.ReallocGUIResources();
          ServiceScope.Get<ILogger>().Warn("GraphicsDevice: Aquired device reset");
          return true;
        }
        catch (Exception ex)
        {
          ServiceScope.Get<ILogger>().Warn("GraphicsDevice: Reset failed");
          ServiceScope.Get<ILogger>().Error(ex);
        }
      }
      return false;
    }

    /// <summary>
    /// Returns available display modes for the display device
    /// </summary>
    public static IList<DisplayMode> GetDisplayModes()
    {
      return _setup.GetDisplayModes();
    }

    public static string DesktopDisplayMode
    {
      get { return _setup.DesktopDisplayMode; }
    }

    public static int DesktopHeight
    {
      get { return _setup.DesktopHeight; }
    }

    public static int DesktopWidth
    {
      get { return _setup.DesktopWidth; }
    }
  }
}