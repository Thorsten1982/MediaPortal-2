using System;
using MediaPortal.UI.SkinEngine.SkinManagement;
using SlimDX;
using SlimDX.Direct3D9;
using System.Drawing;
using Font = SlimDX.Direct3D9.Font;

namespace MediaPortal.UI.SkinEngine.DirectX
{
  public class StatsRenderer
  {
    private static int _tearingPos;
    private static readonly Sprite fontSprite;
    private static readonly Font font;
    private static TimeSpan _totalGuiRenderDuration;
    private static TimeSpan _guiRenderDuration;
    private static int _totalFrameCount = 0;
    private static int _frameCount = 0;
    private static DateTime _frameRenderingStartTime;
    private static int _fpsCounter;
    private static DateTime _fpsTimer;
    private static string _perfLogString;

    static StatsRenderer()
    {
      fontSprite = new Sprite(GraphicsDevice.Device);
      font = new Font(GraphicsDevice.Device, 20,
        0, FontWeight.Normal, 0, false, CharacterSet.Default,
        Precision.Default, FontQuality.ClearTypeNatural, PitchAndFamily.DontCare, "tahoma");
    }
  
    public static void Dispose()
    {
      fontSprite.Dispose();
      font.Dispose();
    }

    public static void DrawTearingTest()
    {
      using (Surface surface = GraphicsDevice.Device.GetRenderTarget(0))
      {
        int left = _tearingPos;
        int width = surface.Description.Width;
        int height = surface.Description.Height;
        Size size = new Size(4, height);
        Point topLeft = new Point(left, 0);
        if (topLeft.X + size.Width >= width)
          topLeft.X = 0;

        Rectangle rcTearing = new Rectangle(topLeft, size);

        GraphicsDevice.Device.ColorFill(surface, rcTearing, new Color4(255,255,255,255));

        topLeft = new Point((rcTearing.Right + 15) % width, 0);
        if (topLeft.X + size.Width >= width)
          topLeft.X = 0;

        rcTearing = new Rectangle(topLeft, size);
        GraphicsDevice.Device.ColorFill(surface, rcTearing, new Color4(255, 100, 100, 100));

        _tearingPos = (_tearingPos + 7) % width;
      }
    }
    public static void DrawText(string text)
    {
      fontSprite.Begin(SpriteFlags.AlphaBlend);
      font.DrawString(fontSprite, text, new Rectangle(0, 0, GraphicsDevice.Width, GraphicsDevice.Height), DrawTextFormat.Left, new Color4(255, 100, 100, 100)); 
      fontSprite.End();
    }

    public static void BeginScene()
    {
      _frameRenderingStartTime = DateTime.Now;
    }

    public static void EndScene()
    {
      TimeSpan guiDur = DateTime.Now - _frameRenderingStartTime;
      _totalGuiRenderDuration += guiDur;
      _guiRenderDuration += guiDur;
      _totalFrameCount++;
      _frameCount++;

      _fpsCounter += 1;
      TimeSpan ts = DateTime.Now - _fpsTimer;
      if (ts.TotalSeconds >= 1.0f)
      {
        float totalAvgGuiTime = (float) _totalGuiRenderDuration.TotalMilliseconds / _totalFrameCount;
        float avgGuiTime = (float) _guiRenderDuration.TotalMilliseconds / _frameCount;
        float secs = (float) ts.TotalSeconds;
        SkinContext.FPS = _fpsCounter / secs;
        _perfLogString = string.Format("RenderLoop: {0:0.00} frames per second, {1} total frames until last measurement, avg GUI render time {2:0.00} last sec: {3:0.00}", SkinContext.FPS, _fpsCounter, totalAvgGuiTime, avgGuiTime);
        _fpsCounter = 0;
        _frameCount = 0;
        _guiRenderDuration = TimeSpan.Zero;
        _fpsTimer = DateTime.Now;
      }
      
      
      DrawTearingTest();
      DrawText(_perfLogString);
    }
  }

}