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
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2.  If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using SlimDX;
using SlimDX.Direct3D9;
using MediaPortal.UI.SkinEngine.DirectX;
using MediaPortal.UI.SkinEngine.Effects;
using MediaPortal.UI.SkinEngine.SkinManagement;

namespace MediaPortal.UI.SkinEngine.ContentManagement
{
  public class VertexBufferAsset : IAsset
  {
    #region Consts

    protected const string EFFECT_NORMAL = "normal";

    #endregion

    #region Variables

    private VertexBuffer _vertexBuffer = null;
    private TextureAsset _texture;
    private DateTime _lastTimeUsed = DateTime.MinValue;
    private float _previousX;
    private float _previousY;
    private float _previousZ;
    private float _previousWidth;
    private float _previousHeight;
    private float _previousColorUpperLeft;
    private float _previousColorBottomLeft;
    private float _previousColorBottomRight;
    private float _previousColorUpperRight;
    //private bool _previousGradientInUse = false;
    private float _previousUoff;
    private float _previousVoff;
    private float _previousUmax;
    private float _previousVMax;
    private readonly EffectAsset _effect;

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="VertexBufferAsset"/> class.
    /// </summary>
    /// <param name="texture">The texture.</param>
    public VertexBufferAsset(TextureAsset texture)
    {
      _texture = texture;
      _effect = ContentManager.GetEffect(EFFECT_NORMAL);
    }

    /// <summary>
    /// Allocates the vertex buffer
    /// </summary>
    public void Allocate()
    {
      if (_vertexBuffer != null)
        Free(true);
      _vertexBuffer = PositionColored2Textured.Create(4);
      ContentManager.VertexReferences++;
      Set(0, 0, 0, 0, 0, 0, 0, 1, 1, 0xff, 0xff, 0xff, 0xff);
    }


    /// <summary>
    /// Switches to a new texture.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    public void SwitchTexture(string fileName, bool thumbnail)
    {
      if (_texture.Name != fileName)
      {
        _previousX = _previousY = _previousZ = _previousWidth = _previousHeight = 0;
        _previousColorUpperLeft = 0;
        _previousColorBottomLeft = 0;
        _previousColorBottomRight = 0;
        _previousColorUpperRight = 0;
//        _previousGradientInUse = false;
        _texture = ContentManager.GetTexture(fileName, thumbnail);
        Set(0, 0, 0, 0, 0, 0, 0, 1, 1, 0xff, 0xff, 0xff, 0xff);
      }
    }

    /// <summary>
    /// Gets the texture.
    /// </summary>
    /// <value>The texture.</value>
    public TextureAsset Texture
    {
      get { return _texture; }
    }

    /// <summary>
    /// Fills the texture buffer with the rendering attribtues
    /// </summary>
    private void Set(float x, float y, float z, float w, float h,
        float uoff, float voff, float umax, float vmax,
        int colorUpperLeft, int colorBottomLeft,
        int colorBottomRight, int colorUpperRight)
    {
      if (!IsAllocated)
        return;
      if (x == _previousX && y == _previousY && z == _previousZ && w == _previousWidth && h == _previousHeight)
        if (colorUpperLeft == _previousColorUpperLeft && colorBottomLeft == _previousColorBottomLeft &&
            colorBottomRight == _previousColorBottomRight && colorUpperRight == _previousColorUpperRight
            /*&& _previousGradientInUse == SkinContext.GradientInUse*/)
          if (uoff == _previousUoff && voff == _previousVoff &&
              umax == _previousUmax && vmax == _previousVMax)
            return;
      _previousUoff = uoff;
      _previousVoff = voff;
      _previousUmax = umax;
      _previousVMax = vmax;

      uoff *= _texture.MaxU;
      voff *= _texture.MaxV;
      umax *= _texture.MaxU;
      vmax *= _texture.MaxV;
      UpdateVertexBuffer(x, y, z, w, h,
          uoff, voff, umax, vmax,
          colorUpperLeft, colorBottomLeft,
          colorBottomRight, colorUpperRight);
      _previousX = x;
      _previousY = y;
      _previousZ = z;
      _previousWidth = w;
      _previousHeight = h;
      _previousColorUpperLeft = colorUpperLeft;
      _previousColorBottomLeft = colorBottomLeft;
      _previousColorBottomRight = colorBottomRight;
      _previousColorUpperRight = colorUpperRight;
//      _previousGradientInUse = SkinContext.GradientInUse;
    }

    private void UpdateVertexBuffer(float left, float top, float z, float width, float height,
        float uoff, float voff, float umax, float vmax,
        int alphaUpperLeft, int alphaBottomLeft,
        int alphaBottomRight, int alphaUpperRight)
    {
      float right = left + width;
      float bottom = top + height;

      float u1 = uoff;
      float u2 = umax;
      float v1 = voff;
      float v2 = vmax;

      Vector3 upperLeft = new Vector3(left, top, z);
      Vector3 bottomLeft = new Vector3(left, bottom, z);
      Vector3 bottomRight = new Vector3(right, bottom, z);
      Vector3 upperRight = new Vector3(right, top, z);

      PositionColored2Textured[] verts = new PositionColored2Textured[4];
      unchecked
      {
        long colorUpperLeft = alphaUpperLeft;
        colorUpperLeft <<= 24;
        colorUpperLeft += 0xffffff;
        long colorBottomLeft = alphaBottomLeft;
        colorBottomLeft <<= 24;
        colorBottomLeft += 0xffffff;
        long colorBottomRight = alphaBottomRight;
        colorBottomRight <<= 24;
        colorBottomRight += 0xffffff;
        long colorUpperRight = alphaUpperRight;
        colorUpperRight <<= 24;
        colorUpperRight += 0xffffff;

        //upper left
        verts[0].Tu1 = u1;
        verts[0].Tv1 = v1;
        verts[0].Position = upperLeft;
        verts[0].Color = (int)colorUpperLeft;

        //bottom left
        verts[1].Tu1 = u1;
        verts[1].Tv1 = v2;
        verts[1].Position = bottomLeft;
        verts[1].Color = (int)colorBottomLeft;
        
        //bottom right
        verts[2].Tu1 = u2;
        verts[2].Tv1 = v2;
        verts[2].Position = bottomRight;
        verts[2].Color = (int)colorBottomRight;

        //upper right
        verts[3].Tu1 = u2;
        verts[3].Tv1 = v1;
        verts[3].Position = upperRight;
        verts[3].Color = (int)colorUpperRight;
      }
      PositionColored2Textured.Set(_vertexBuffer, verts);
    }

    #region IAsset Members

    public bool IsAllocated
    {
      get { return _vertexBuffer != null; }
    }

    public bool CanBeDeleted
    {
      get
      {
        if (!IsAllocated)
          return false;
        TimeSpan ts = SkinContext.FrameRenderingStartTime - _lastTimeUsed;
        if (ts.TotalSeconds >= 5)
          return true;
        return false;
      }
    }

    public void Free(bool force)
    {
      if (_vertexBuffer != null)
      {
        _vertexBuffer.Dispose();
        _vertexBuffer = null;
        ContentManager.VertexReferences--;
      }
    }

    #endregion

    /// <summary>
    /// Draws the vertex buffer and associated texture.
    /// </summary>
    public void Draw(float x, float y, float z, float width, float height, float alpha, int streamNumber, Matrix finalTransform)
    {
      if (!_texture.IsAllocated)
        _texture.Allocate();
      if (!_texture.IsAllocated)
        return;
      if (!IsAllocated)
        Allocate();
      if (!IsAllocated)
        return;

      alpha *= 255;
      if (alpha < 0)
        alpha = 0;
      if (alpha > 255)
        alpha = 255;

      Set(x, y, z, width, height, 0, 0, 1, 1, (int) alpha, (int) alpha, (int) alpha, (int) alpha);

      GraphicsDevice.Device.VertexFormat = PositionColored2Textured.Format;
      GraphicsDevice.Device.SetStreamSource(streamNumber, _vertexBuffer, 0, PositionColored2Textured.StrideSize);
      _effect.Render(_texture, streamNumber, finalTransform);
      _lastTimeUsed = SkinContext.FrameRenderingStartTime;
    }

    /// <summary>
    /// Draws the vertex buffer and associated texture.
    /// </summary>
    public void Draw(float x, float y, float z, float width, float height,
        float uoff, float voff, float umax, float vmax,
        float alphaUpperLeft, float alphaBottomLeft,
        float alphaBottomRight, float alphaUpperRight, int streamNumber, Matrix finalTransform)
    {
      if (!_texture.IsAllocated)
        _texture.Allocate();
      if (!_texture.IsAllocated)
        return;
      if (!IsAllocated)
        Allocate();
      if (!IsAllocated)
        return;

      alphaUpperLeft *= 255;
      if (alphaUpperLeft < 0)
        alphaUpperLeft = 0;
      if (alphaUpperLeft > 255)
        alphaUpperLeft = 255;

      alphaBottomLeft *= 255;
      if (alphaBottomLeft < 0)
        alphaBottomLeft = 0;
      if (alphaBottomLeft > 255)
        alphaBottomLeft = 255;

      alphaBottomRight *= 255;
      if (alphaBottomRight < 0)
        alphaBottomRight = 0;
      if (alphaBottomRight > 255)
        alphaBottomRight = 255;

      alphaUpperRight *= 255;
      if (alphaUpperRight < 0)
        alphaUpperRight = 0;
      if (alphaUpperRight > 255)
        alphaUpperRight = 255;

      Set(x, y, z, width, height, uoff, voff, umax, vmax,
          (int) alphaUpperLeft, (int) alphaBottomLeft, (int) alphaBottomRight, (int) alphaUpperRight);
      GraphicsDevice.Device.VertexFormat = PositionColored2Textured.Format;
      GraphicsDevice.Device.SetStreamSource(0, _vertexBuffer, 0, PositionColored2Textured.StrideSize);

      _effect.Render(_texture, 0, finalTransform);
      _lastTimeUsed = SkinContext.FrameRenderingStartTime;
    }
  }
}
