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

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using MediaPortal.Presentation.DataObjects;
using MediaPortal.SkinEngine.ContentManagement;
using MediaPortal.SkinEngine.DirectX;
using MediaPortal.SkinEngine.Rendering;
using MediaPortal.SkinEngine.Xaml;
using SlimDX.Direct3D9;
using MediaPortal.Utilities.DeepCopy;
using MediaPortal.SkinEngine.SkinManagement;

namespace MediaPortal.SkinEngine.Controls.Visuals.Shapes
{

  public class Path : Shape
  {
    #region Private fields

    Property _dataProperty;
    PrimitiveType _fillPrimitiveType;
    bool _fillDisabled;

    #endregion

    #region Ctor

    public Path()
    {
      Init();
    }

    void Init()
    {
      _dataProperty = new Property(typeof(string), "");
    }

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      base.DeepCopy(source, copyManager);
      Path p = (Path) source;
      Data = copyManager.GetCopy(p.Data);
    }

    #endregion

    public Property DataProperty
    {
      get { return _dataProperty; }
    }

    public string Data
    {
      get { return (string)_dataProperty.GetValue(); }
      set { _dataProperty.SetValue(value); }
    }

    protected override void PerformLayout()
    {
      TimeSpan ts;
      DateTime now = DateTime.Now;
      double w = ActualWidth;
      double h = ActualHeight;
      float centerX, centerY;
      SizeF rectSize = new SizeF((float)w, (float)h);

      ExtendedMatrix m = new ExtendedMatrix();
      if (_finalLayoutTransform != null)
        m.Matrix *= _finalLayoutTransform.Matrix;
      if (LayoutTransform != null)
      {
        ExtendedMatrix em;
        LayoutTransform.GetTransform(out em);
        m.Matrix *= em.Matrix;
      }
      m.InvertSize(ref rectSize);
      System.Drawing.RectangleF rect = new System.Drawing.RectangleF((float)ActualPosition.X, (float)ActualPosition.Y, rectSize.Width, rectSize.Height);

      //Fill brush
      PositionColored2Textured[] verts;
      GraphicsPath path;
      if (Fill != null || ((Stroke != null && StrokeThickness > 0)))
      {
        bool isClosed;
        if (Fill != null)
        {
          using (path = GetPath(rect, _finalLayoutTransform, out isClosed, 0))
          {
            if (!_fillDisabled)
            {
              CalcCentroid(path, out centerX, out centerY);
              //Trace.WriteLine(String.Format("Path.PerformLayout() {0} points: {1} closed:{2}", this.Name, path.PointCount, isClosed));
              if (Fill != null)
              {
                if (SkinContext.UseBatching == false)
                {
                  if (_fillAsset == null)
                  {
                    _fillAsset = new VisualAssetContext("Path._fillContext:" + this.Name);
                    ContentManager.Add(_fillAsset);
                  }
                  _fillAsset.VertexBuffer = Triangulate(path, centerX, centerY, isClosed, out verts, out _fillPrimitiveType);
                  if (_fillAsset.VertexBuffer != null)
                  {
                    Fill.SetupBrush(this, ref verts);

                    PositionColored2Textured.Set(_fillAsset.VertexBuffer, ref verts);
                    if (_fillPrimitiveType == PrimitiveType.TriangleList)
                      _verticesCountFill = (verts.Length / 3);
                    else
                      _verticesCountFill = (verts.Length - 2);
                  }
                }
                else
                {
                  Shape.PathToTriangleList(path, centerX, centerY, out verts);
                  _verticesCountFill = (verts.Length / 3);
                  Fill.SetupBrush(this, ref verts);
                  if (_fillContext == null)
                  {
                    _fillContext = new PrimitiveContext(_verticesCountFill, ref verts);
                    Fill.SetupPrimitive(_fillContext);
                    RenderPipeline.Instance.Add(_fillContext);
                  }
                  else
                  {
                    _fillContext.OnVerticesChanged(_verticesCountFill, ref verts);
                  }
                }
              }
            }
          }
          ts = DateTime.Now - now;
          //Trace.WriteLine(String.Format(" fill:{0}", ts.TotalMilliseconds));
        }
        if (Stroke != null && StrokeThickness > 0)
        {
          using (path = GetPath(rect, _finalLayoutTransform, out isClosed, (float)(StrokeThickness)))
          {
            if (SkinContext.UseBatching == false)
            {
              if (_borderAsset == null)
              {
                _borderAsset = new VisualAssetContext("Path._borderContext:" + this.Name);
                ContentManager.Add(_borderAsset);
              }
              _borderAsset.VertexBuffer = ConvertPathToTriangleStrip(path, (float)(StrokeThickness), isClosed, out verts, _finalLayoutTransform, true);
              if (_borderAsset.VertexBuffer != null)
              {
                Stroke.SetupBrush(this, ref verts);

                PositionColored2Textured.Set(_borderAsset.VertexBuffer, ref verts);
                _verticesCountBorder = verts.Length / 3;
              }
            }
            else
            {
              Shape.StrokePathToTriangleStrip(path, (float)(StrokeThickness / 2.0), isClosed, out verts, _finalLayoutTransform);
              _verticesCountBorder = (verts.Length / 3);
              Stroke.SetupBrush(this, ref verts);
              if (_strokeContext == null)
              {
                _strokeContext = new PrimitiveContext(_verticesCountBorder, ref verts);
                Stroke.SetupPrimitive(_strokeContext);
                RenderPipeline.Instance.Add(_strokeContext);
              }
              else
              {
                _strokeContext.OnVerticesChanged(_verticesCountBorder, ref verts);
              }
            }

          }
        }
      }

      ts = DateTime.Now - now;
      //Trace.WriteLine(String.Format("total:{0}", ts.TotalMilliseconds));
    }


    public override void Measure(ref SizeF totalSize)
    {
      bool isclosed;

      using (GraphicsPath p = GetPath(new RectangleF(0, 0, 0, 0), null, out isclosed, 0))
      {
        RectangleF bounds = p.GetBounds();

        _desiredSize = new SizeF((float)Width * SkinContext.Zoom.Width, (float)Height * SkinContext.Zoom.Height);

        if (Double.IsNaN(Width))
          _desiredSize.Width = bounds.Width * SkinContext.Zoom.Width;

        if (Double.IsNaN(Height))
          _desiredSize.Height = bounds.Height * SkinContext.Zoom.Height;

        if (LayoutTransform != null)
        {
          ExtendedMatrix m = new ExtendedMatrix();
          LayoutTransform.GetTransform(out m);
          SkinContext.AddLayoutTransform(m);
        }
        SkinContext.FinalLayoutTransform.TransformSize(ref _desiredSize);

        if (LayoutTransform != null)
        {
          SkinContext.RemoveLayoutTransform();
        }

        totalSize = _desiredSize;
        AddMargin(ref totalSize);

        //Trace.WriteLine(String.Format("path.measure :{0} returns {1}x{2}", this.Name, (int)_desiredSize.Width, (int)_desiredSize.Height));
      }
    }

    private GraphicsPath GetPath(RectangleF baseRect, ExtendedMatrix finalTransform, out bool isClosed, float thickNess)
    {
      isClosed = false;
      GraphicsPath mPath = new GraphicsPath();
      mPath.FillMode = System.Drawing.Drawing2D.FillMode.Alternate;
      PointF lastPoint = new PointF();
      PointF startPoint = new PointF();
      Regex regex = new Regex(@"[a-zA-Z][-0-9\.,-0-9\. ]*");
      MatchCollection matches = regex.Matches(Data);

      foreach (Match match in matches)
      {
        char cmd = match.Value[0];
        PointF[] points = null;
        if (match.Value.Length > 1)
        {
          string[] txtpoints;
          txtpoints = match.Value.Substring(1).Trim().Split(new char[] { ',', ' ' });
          if (txtpoints.Length == 1)
          {
            points = new PointF[1];
            points[0].X = (float) TypeConverter.Convert(txtpoints[0], typeof(float));
          }
          else
          {
            int c = txtpoints.Length / 2;
            points = new PointF[c];
            for (int i = 0; i < c; i++)
            {
              points[i].X = (float)TypeConverter.Convert(txtpoints[i * 2], typeof(float));
              if (i + 1 < txtpoints.Length)
                points[i].Y = (float)TypeConverter.Convert(txtpoints[i * 2 + 1], typeof(float));
            }
          }
        }
        switch (cmd)
        {
          case 'm':
            {
              //relative origin
              PointF point = points[0];
              lastPoint = new PointF(lastPoint.X + point.X, lastPoint.Y + point.Y);
              startPoint = new PointF(lastPoint.X + point.X, lastPoint.Y + point.Y);
            }
            break;
          case 'M':
            {
              //absolute origin
              lastPoint = points[0]; ;
              startPoint = new PointF(points[0].X, points[0].Y);
            }
            break;
          case 'L':
            {
              //absolute Line
              for (int i = 0; i < points.Length; ++i)
              {
                mPath.AddLine(lastPoint, points[i]);
                lastPoint = points[i];
              }
            }
            break;
          case 'l':
            {
              //relative Line
              for (int i = 0; i < points.Length; ++i)
              {
                points[i].X += lastPoint.X;
                points[i].Y += lastPoint.Y;
                mPath.AddLine(lastPoint, points[i]);
                lastPoint = points[i];
              }
            }
            break;
          case 'H':
            {
              //Horizontal line to absolute X 
              PointF point1 = new PointF(points[0].X, lastPoint.Y);
              mPath.AddLine(lastPoint, point1);
              lastPoint = new PointF(point1.X, point1.Y);
            }
            break;
          case 'h':
            {
              //Horizontal line to relative X
              PointF point1 = new PointF(lastPoint.X + points[0].X, lastPoint.Y);
              mPath.AddLine(lastPoint, point1);
              lastPoint = new PointF(point1.X, point1.Y);
            }
            break;
          case 'V':
            {
              //Vertical line to absolute y 
              PointF point1 = new PointF(lastPoint.X, points[0].X);
              mPath.AddLine(lastPoint, point1);
              lastPoint = new PointF(point1.X, point1.Y);
            }
            break;
          case 'v':
            {
              //Vertical line to relative y
              PointF point1 = new PointF(lastPoint.X, lastPoint.Y + points[0].X);
              mPath.AddLine(lastPoint, point1);
              lastPoint = new PointF(point1.X, point1.Y);
            }
            break;
          case 'C':
            {
              //Quadratic Bezier Curve Command C21,17,17,21,13,21
              for (int i = 0; i < points.Length; i += 3)
              {
                mPath.AddBezier(lastPoint, points[i], points[i + 1], points[i + 2]);
                lastPoint = points[i + 2];
              }
            }
            break;
          case 'c':
            {
              //Quadratic Bezier Curve Command
              for (int i = 0; i < points.Length; i += 3)
              {
                points[i].X += lastPoint.X;
                points[i].Y += lastPoint.Y;
                mPath.AddBezier(lastPoint, points[i], points[i + 1], points[i + 2]);
                lastPoint = points[i + 2];
              }
            }
            break;
          case 'F':
            {
              //Horizontal line to relative X
              if (points[0].X == 0.0f)
              {
                //the EvenOdd fill rule
                //Rule that determines whether a point is in the fill region by drawing a ray 
                //from that point to infinity in any direction and counting the number of path 
                //segments within the given shape that the ray crosses. If this number is odd, 
                //the point is inside; if even, the point is outside.
                mPath.FillMode = System.Drawing.Drawing2D.FillMode.Alternate;
              }
              else if (points[0].X == 1.0f)
              {
                //the Nonzero fill rule.
                //Rule that determines whether a point is in the fill region of the 
                //path by drawing a ray from that point to infinity in any direction
                //and then examining the places where a segment of the shape crosses
                //the ray. Starting with a count of zero, add one each time a segment 
                //crosses the ray from left to right and subtract one each time a path
                //segment crosses the ray from right to left. After counting the crossings
                //, if the result is zero then the point is outside the path. Otherwise, it is inside.
                mPath.FillMode = System.Drawing.Drawing2D.FillMode.Winding;
              }
            }
            break;
          case 'z':
            {
              //close figure
              isClosed = true;
              if (Math.Abs(lastPoint.X - startPoint.X) >= 1 || Math.Abs(lastPoint.Y - startPoint.Y) >= 1)
              {
                mPath.AddLine(lastPoint, startPoint);
              }
              mPath.CloseFigure();
            }
            break;
        }
      }
      RectangleF bounds;
      System.Drawing.Drawing2D.Matrix m = new System.Drawing.Drawing2D.Matrix();
      bounds = mPath.GetBounds();
      _fillDisabled = false;
      if (bounds.Width < StrokeThickness || bounds.Height < StrokeThickness)
      {
        _fillDisabled = true;
      }
      if (Stretch == Stretch.Fill)
      {
        bounds = mPath.GetBounds();
        m.Translate(-bounds.X, -bounds.Y, MatrixOrder.Append);
        float xoff = 0;
        float yoff = 0;
        if (Width > 0) baseRect.Width = (float)(Width * SkinContext.Zoom.Width);
        if (Height > 0) baseRect.Height = (float)(Height * SkinContext.Zoom.Width);
        float scaleW = baseRect.Width / bounds.Width;
        float scaleH = baseRect.Height / bounds.Height;
        if (baseRect.Width == 0) scaleW = 1.0f;
        if (baseRect.Height == 0) scaleH = 1.0f;
        if (StrokeThickness > 0 && !_fillDisabled)
        {
          if (baseRect.Width > 0)
          {
            xoff = (float)(StrokeThickness / 2.0f);
            scaleW = (float)((baseRect.Width - StrokeThickness) / bounds.Width);
          }
          if (baseRect.Height > 0)
          {
            yoff = (float)(StrokeThickness / 2.0f);
            scaleH = (float)((baseRect.Height - StrokeThickness) / bounds.Height);
          }
        }
        if (float.IsNaN(scaleW) || float.IsInfinity(scaleW)) scaleW = 1;
        if (float.IsNaN(scaleH) || float.IsInfinity(scaleH)) scaleH = 1;
        m.Scale(scaleW, scaleH, MatrixOrder.Append);
        m.Translate(xoff, yoff, MatrixOrder.Append);
      }
      if (finalTransform != null)
      {
        m.Multiply(finalTransform.Get2dMatrix(), MatrixOrder.Append);
        if (Stretch != Stretch.Fill)
          m.Scale(SkinContext.Zoom.Width, SkinContext.Zoom.Height, MatrixOrder.Append);
      }
      ExtendedMatrix em = null;
      if (LayoutTransform != null)
      {
        LayoutTransform.GetTransform(out em);
        m.Multiply(em.Get2dMatrix(), MatrixOrder.Append);
      }
      m.Translate(baseRect.X, baseRect.Y, MatrixOrder.Append);
      mPath.Transform(m);

      if (thickNess != 0.0)
      {
        //thickNess /= 2.0f;
        bounds = mPath.GetBounds();
        m = new System.Drawing.Drawing2D.Matrix();
        float thicknessW = thickNess * SkinContext.Zoom.Width;
        float thicknessH = thickNess * SkinContext.Zoom.Height;
        if (finalTransform != null)
          finalTransform.TransformXY(ref thicknessW, ref thicknessH);
        if (em != null)
          em.TransformXY(ref thicknessW, ref thicknessH);
        thicknessW = (bounds.Width + Math.Abs(thicknessW));
        thicknessH = (bounds.Height + Math.Abs(thicknessH));

        float cx = bounds.X + (bounds.Width / 2.0f);
        float cy = bounds.Y + (bounds.Height / 2.0f);
        m.Translate(-cx, -cy, MatrixOrder.Append);
        float scaleW = thicknessW / bounds.Width;
        float scaleH = thicknessH / bounds.Height;
        if (float.IsNaN(scaleW) || float.IsInfinity(scaleW))
        {
          m.Translate(thickNess / 2.0f, 0, MatrixOrder.Append);
          scaleW = 1;
        }
        if (float.IsNaN(scaleH) || float.IsInfinity(scaleH))
        {
          m.Translate(0, thickNess / 2.0f, MatrixOrder.Append);
          scaleH = 1;
        }
        m.Scale(scaleW, scaleH, MatrixOrder.Append);
        m.Translate(cx, cy, MatrixOrder.Append);
        mPath.Transform(m);
      }
      mPath.Flatten();
      return mPath;
    }
  }
}