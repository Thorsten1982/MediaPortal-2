﻿#region Copyright (C) 2007 Team MediaPortal

/*
    Copyright (C) 2007 Team MediaPortal
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using MediaPortal.Core;
using MediaPortal.Core.Logging;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace SkinEngine.Effects
{
  public class EffectAsset : IAsset
  {
    private string _effectName;
    private Effect _effect;
    private DateTime _lastUsed = DateTime.MinValue;
    private double _ticksPerSecond = 0;
    private double _lastElapsedTime = 0;
    Dictionary<string, object> _effectParameters;

    [SuppressUnmanagedCodeSecurity] // We won't use this maliciously
    [DllImport("kernel32")]
    public static extern bool QueryPerformanceFrequency(ref long PerformanceFrequency);

    [SuppressUnmanagedCodeSecurity] // We won't use this maliciously
    [DllImport("kernel32")]
    public static extern bool QueryPerformanceCounter(ref long PerformanceCount);

    /// <summary>
    /// Initializes a new instance of the <see cref="EffectAsset"/> class.
    /// </summary>
    /// <param name="effectName">Name of the effect.</param>
    public EffectAsset(string effectName)
    {
      _effectParameters = new Dictionary<string, object>();
      _effectName = effectName;
      long ticksPerSecond = 0;
      bool res = QueryPerformanceFrequency(ref ticksPerSecond);
      _ticksPerSecond = (float)ticksPerSecond;

      long time = 0;
      QueryPerformanceCounter(ref time);
      _lastElapsedTime = (double)time;
    }

    /// <summary>
    /// Allocates this instance.
    /// </summary>
    private void Allocate()
    {
      string effectFile = String.Format(@"skin\{0}\shaders\{1}.fx", SkinContext.SkinName, _effectName);
      if (File.Exists(effectFile))
      {
        ShaderFlags shaderFlags = ShaderFlags.NoPreShader;
        string errors = "";
        _effect = Effect.FromFile(GraphicsDevice.Device, effectFile, null, shaderFlags, null, out errors);
        if (_effect == null)
        {
          ServiceScope.Get<ILogger>().Error("Unable to load {0}", effectFile);
          ServiceScope.Get<ILogger>().Error("errors:{0}", errors);
          _lastUsed = SkinContext.Now;
        }
      }
    }

    public Dictionary<string, object> Parameters
    {
      get
      {
        return _effectParameters;
      }
    }

    #region IAsset Members

    /// <summary>
    /// Gets a value indicating the asset is allocated
    /// </summary>
    /// <value><c>true</c> if this asset is allocated; otherwise, <c>false</c>.</value>
    public bool IsAllocated
    {
      get { return (_effect != null); }
    }

    /// <summary>
    /// Gets a value indicating whether this asset can be deleted.
    /// </summary>
    /// <value>
    /// 	<c>true</c> if this asset can be deleted; otherwise, <c>false</c>.
    /// </value>
    public bool CanBeDeleted
    {
      get
      {
        TimeSpan ts = SkinContext.Now - _lastUsed;
        return (ts.TotalSeconds >= 5);
      }
    }

    /// <summary>
    /// Frees this asset.
    /// </summary>
    public void Free()
    {
      if (_effect != null)
      {
        _effect.Dispose();
        _effect = null;
      }
    }

    #endregion

    /// <summary>
    /// Renders the effect
    /// </summary>
    /// <param name="tex">The texture.</param>
    public void Render(TextureAsset tex)
    {
      if (!IsAllocated)
      {
        Allocate();
      }

      if (!IsAllocated)
      {
        //render without effect
        tex.Draw(0);
        return;
      }
      if (!tex.IsAllocated)
      {
        tex.Allocate();
        if (!tex.IsAllocated)
        {
          return;
        }
      }

      long time = 0;
      QueryPerformanceCounter(ref time);
      double elapsedTime = (double)(time - _lastElapsedTime) / (double)_ticksPerSecond;
      _effect.SetValue("worldViewProj",
                       SkinContext.FinalMatrix.Matrix * GraphicsDevice.Device.Transform.View *
                       GraphicsDevice.Device.Transform.Projection);
      _effect.SetValue("g_texture", tex.Texture);
      _effect.SetValue("appTime", (float)elapsedTime);
      _effect.Technique = "simple";
      SetEffectParameters();
      _effect.Begin(0);
      _effect.BeginPass(0);
      tex.Draw(0);
      _effect.EndPass();
      _effect.End();
      _lastUsed = SkinContext.Now;
    }

    public void StartRender(Texture tex)
    {
      if (!IsAllocated)
      {
        Allocate();
      }
      if (!IsAllocated)
      {
        //render without effect
        GraphicsDevice.Device.SetTexture(0, tex);
        return;
      }
      long time = 0;
      QueryPerformanceCounter(ref time);
      double elapsedTime = (double)(time - _lastElapsedTime) / (double)_ticksPerSecond;
      _effect.SetValue("worldViewProj",
                       SkinContext.FinalMatrix.Matrix * GraphicsDevice.Device.Transform.View *
                       GraphicsDevice.Device.Transform.Projection);
      _effect.SetValue("g_texture", tex);
      _effect.SetValue("appTime", (float)elapsedTime);
      _effect.Technique = "simple";
      SetEffectParameters();
      _effect.Begin(0);
      _effect.BeginPass(0);

      GraphicsDevice.Device.SetTexture(0, tex);
    }
    public void EndRender()
    {
      if (_effect != null)
      {
        _effect.EndPass();
        _effect.End();
        _lastUsed = SkinContext.Now;
      }
      GraphicsDevice.Device.SetTexture(0, null);
    }

    public void Render(Texture tex)
    {
      StartRender(tex);
      GraphicsDevice.Device.DrawPrimitives(PrimitiveType.TriangleFan, 0, 2);
      EndRender();
    }

    void SetEffectParameters()
    {
      Dictionary<string, object>.Enumerator enumer = _effectParameters.GetEnumerator();
      while (enumer.MoveNext())
      {
        object v = enumer.Current.Value;
        Type type = v.GetType();
        if (type == typeof(ColorValue))
          _effect.SetValue(enumer.Current.Key, (ColorValue)v);

        else if (type == typeof(ColorValue[]))
          _effect.SetValue(enumer.Current.Key, (ColorValue[])v);

        else if (type == typeof(float))
          _effect.SetValue(enumer.Current.Key, (float)v);

        else if (type == typeof(float[]))
          _effect.SetValue(enumer.Current.Key, (float[])v);


        else if (type == typeof(Matrix))
          _effect.SetValue(enumer.Current.Key, (Matrix)v);

        else if (type == typeof(Vector4))
          _effect.SetValue(enumer.Current.Key, (Vector4)v);

        else if (type == typeof(bool))
          _effect.SetValue(enumer.Current.Key, (bool)v);

        else if (type == typeof(float))
          _effect.SetValue(enumer.Current.Key, (float)v);

        else if (type == typeof(int))
          _effect.SetValue(enumer.Current.Key, (int)v);

      }
    }
  }
}