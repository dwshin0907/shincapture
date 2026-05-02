using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using MediaPen = System.Windows.Media.Pen;
using SDPoint = System.Drawing.Point;

namespace ShinCapture.Capture;

/// <summary>
/// 자유형 다각형 입력을 받고 GrabCut으로 객체 경계를 픽셀 단위 정밀화하는 캡쳐 모드.
/// FreeformCaptureMode와 마우스/렌더 흐름은 동일하나, ApplyMask 단계에서 GrabCut을 추가 실행.
/// </summary>
public class SmartCutCaptureMode : ICaptureMode
{
    private Bitmap? _screenBitmap;
    private FrameworkElement? _overlay;

    private double _scaleX = 1.0;
    private double _scaleY = 1.0;

    private bool _isDrawing = false;
    private readonly List<System.Windows.Point> _points = new();

    public bool IsComplete { get; private set; } = false;
    public bool IsCancelled { get; private set; } = false;

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _screenBitmap = screenBitmap;
        _overlay = overlay;
        if (overlay.ActualWidth > 0 && screenBitmap.Width > 0)
            _scaleX = screenBitmap.Width / overlay.ActualWidth;
        if (overlay.ActualHeight > 0 && screenBitmap.Height > 0)
            _scaleY = screenBitmap.Height / overlay.ActualHeight;
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _points.Clear();
            _isDrawing = true;
            _points.Add(e.GetPosition(_overlay));
        }
        else if (e.ChangedButton == MouseButton.Right)
        {
            IsCancelled = true;
        }
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        if (_isDrawing) _points.Add(e.GetPosition(_overlay));
    }

    public void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_isDrawing && e.LeftButton == MouseButtonState.Released)
        {
            _isDrawing = false;
            if (_points.Count > 10) IsComplete = true;
        }
    }

    public void Render(System.Windows.Media.DrawingContext dc, double overlayWidth, double overlayHeight)
    {
        if (_points.Count < 2) return;

        var dimBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 0, 0, 0));
        dc.DrawRectangle(dimBrush, null, new System.Windows.Rect(0, 0, overlayWidth, overlayHeight));

        // SmartCut은 매젠타 점선 (자유캡쳐는 흰색 점선)
        var pen = new MediaPen(System.Windows.Media.Brushes.Magenta, 2.0)
        {
            DashStyle = DashStyles.Dash
        };
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(_points[0], false, false);
            for (int i = 1; i < _points.Count; i++)
                ctx.LineTo(_points[i], true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    public Rectangle? GetSelectedRegion()
    {
        if (!IsComplete || _points.Count == 0) return null;
        var bounds = GetBounds();
        int x = (int)Math.Round(bounds.Left * _scaleX);
        int y = (int)Math.Round(bounds.Top * _scaleY);
        int right = (int)Math.Round((bounds.Left + bounds.Width) * _scaleX);
        int bottom = (int)Math.Round((bounds.Top + bounds.Height) * _scaleY);
        return new Rectangle(x, y, right - x, bottom - y);
    }

    private System.Windows.Rect GetBounds()
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in _points)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }
        return new System.Windows.Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// bounding-box로 잘린 비트맵에 GrabCut을 적용해 객체 경계만 알파로 마스킹.
    /// 다각형 안 = "probable foreground", 다각형 밖 = "definite background"로 init.
    /// </summary>
    public Bitmap ApplyGrabCut(Bitmap croppedBitmap)
    {
        if (_points.Count < 3 || croppedBitmap == null) return croppedBitmap!;

        var bounds = GetBounds();

        // overlay 좌표 → cropped 비트맵 픽셀 좌표 (FreeformCaptureMode와 동일)
        var localPoints = new System.Drawing.PointF[_points.Count];
        for (int i = 0; i < _points.Count; i++)
        {
            localPoints[i] = new System.Drawing.PointF(
                (float)((_points[i].X - bounds.X) * _scaleX),
                (float)((_points[i].Y - bounds.Y) * _scaleY));
        }

        int w = croppedBitmap.Width;
        int h = croppedBitmap.Height;

        // BGR Mat 변환 (GrabCut은 3채널 입력 요구)
        using var src = BitmapConverter.ToMat(croppedBitmap);
        Mat src3;
        if (src.Channels() == 4)
        {
            src3 = new Mat();
            Cv2.CvtColor(src, src3, ColorConversionCodes.BGRA2BGR);
        }
        else if (src.Channels() == 3)
        {
            src3 = src.Clone();
        }
        else
        {
            src3 = new Mat();
            Cv2.CvtColor(src, src3, ColorConversionCodes.GRAY2BGR);
        }

        // 마스크 초기화: 기본 GC_BGD, 다각형 안은 GC_PR_FGD
        using var mask = new Mat(h, w, MatType.CV_8UC1, new Scalar((double)GrabCutClasses.BGD));
        var ocvPoly = localPoints.Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray();
        Cv2.FillPoly(mask, new[] { ocvPoly }, new Scalar((double)GrabCutClasses.PR_FGD));

        using var bgdModel = new Mat();
        using var fgdModel = new Mat();

        // GrabCut 실행 (마스크 초기화 모드, 5회 반복)
        Cv2.GrabCut(src3, mask, default(OpenCvSharp.Rect),
            bgdModel, fgdModel, 5, GrabCutModes.InitWithMask);

        // 결과 마스크: GC_FGD(1) 또는 GC_PR_FGD(3) 픽셀만 전경
        using var fgMask = new Mat();
        Cv2.Compare(mask, new Scalar((double)GrabCutClasses.PR_FGD), fgMask, CmpType.EQ);
        using var fgMask2 = new Mat();
        Cv2.Compare(mask, new Scalar((double)GrabCutClasses.FGD), fgMask2, CmpType.EQ);
        Cv2.BitwiseOr(fgMask, fgMask2, fgMask);

        src3.Dispose();

        // BGRA 결과 비트맵 생성: 전경 픽셀은 원본 색 + 알파 255, 배경은 알파 0
        var result = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = result.LockBits(
            new Rectangle(0, 0, w, h),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                byte* dst = (byte*)data.Scan0;
                int stride = data.Stride;
                var indexer = fgMask.GetGenericIndexer<byte>();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int offset = y * stride + x * 4;
                        if (indexer[y, x] != 0)
                        {
                            var src_pixel = croppedBitmap.GetPixel(x, y);
                            dst[offset + 0] = src_pixel.B;
                            dst[offset + 1] = src_pixel.G;
                            dst[offset + 2] = src_pixel.R;
                            dst[offset + 3] = 255;
                        }
                        else
                        {
                            dst[offset + 0] = 0;
                            dst[offset + 1] = 0;
                            dst[offset + 2] = 0;
                            dst[offset + 3] = 0;
                        }
                    }
                }
            }
        }
        finally
        {
            result.UnlockBits(data);
        }
        return result;
    }
}
