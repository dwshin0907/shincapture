using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MediaPen = System.Windows.Media.Pen;

namespace ShinCapture.Capture;

public class FreeformCaptureMode : ICaptureMode
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
            var pos = e.GetPosition(_overlay);
            _points.Add(pos);
        }
        else if (e.ChangedButton == MouseButton.Right)
        {
            IsCancelled = true;
        }
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        if (_isDrawing)
        {
            var pos = e.GetPosition(_overlay);
            _points.Add(pos);
        }
    }

    public void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_isDrawing && e.LeftButton == MouseButtonState.Released)
        {
            _isDrawing = false;
            if (_points.Count > 10)
                IsComplete = true;
        }
    }

    public void Render(System.Windows.Media.DrawingContext dc, double overlayWidth, double overlayHeight)
    {
        if (_points.Count < 2) return;

        // Dim overlay
        var dimBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 0, 0, 0));
        dc.DrawRectangle(dimBrush, null, new Rect(0, 0, overlayWidth, overlayHeight));

        // Draw freeform polyline (white dashed)
        var pen = new MediaPen(System.Windows.Media.Brushes.White, 2.0)
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

        // If complete, show bounding rect
        if (IsComplete)
        {
            var bounds = GetBounds();
            var borderPen = new MediaPen(System.Windows.Media.Brushes.White, 1.0);
            dc.DrawRectangle(null, borderPen, bounds);
        }
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

    private Rect GetBounds()
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
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// bounding-box로 잘린 비트맵에 자유형 다각형 마스크 적용.
    /// 다각형 외부 픽셀은 투명(Alpha=0)으로 처리.
    ///
    /// 구현: 외곽선을 4-connected 라인으로 마스크에 찍고, 가장자리 픽셀에서 BFS flood fill로
    /// outside를 결정. self-intersection/반대 방향 작은 loop가 있어도 boundary로 둘러싸인
    /// 모든 영역은 inside로 처리됨. GDI+ GraphicsPath의 Alternate/Winding fill rule 한계
    /// (반대 방향 winding으로 일부 영역을 outside로 판정)를 근본적으로 우회.
    /// </summary>
    public Bitmap ApplyMask(Bitmap croppedBitmap)
    {
        if (_points.Count < 3 || croppedBitmap == null) return croppedBitmap!;

        var bounds = GetBounds();
        int w = croppedBitmap.Width;
        int h = croppedBitmap.Height;

        // overlay 좌표 → cropped 픽셀 좌표
        var pts = new (int X, int Y)[_points.Count];
        for (int i = 0; i < _points.Count; i++)
        {
            pts[i] = (
                (int)Math.Round((_points[i].X - bounds.X) * _scaleX),
                (int)Math.Round((_points[i].Y - bounds.Y) * _scaleY));
        }

        // 1) 외곽선을 4-connected 라인으로 마스크에 찍기 (마지막 점 → 첫 점 자동 close).
        //    4-connected라야 4-connected flood fill에서 대각선 사이로 새지 않음.
        var line = new bool[w * h];
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Length];
            DrawLine4(line, w, h, a.X, a.Y, b.X, b.Y);
        }

        // 2) boundary 2px dilate (5x5): 사용자가 손으로 그릴 때 발생하는 self-intersection
        //    좁은 갭(최대 5px 너비)과 좌표 round 오차로 인한 픽셀 틈을 닫아서 외부 flood가
        //    안쪽으로 침투하지 못하게 함. 3x3(1px)으론 빠르게 그릴 때 가끔 통로 못 막아 검은
        //    점이 남음. 5px 이내의 뾰족한 디테일이 둥글어지는 trade-off가 있지만 캡쳐 용도엔
        //    거의 영향 없음.
        var isBoundary = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                if (!line[row + x]) continue;
                for (int dy = -2; dy <= 2; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    int nrow = ny * w;
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= w) continue;
                        isBoundary[nrow + nx] = true;
                    }
                }
            }
        }

        // 3) 비트맵 가장자리 픽셀에서 BFS flood fill → outside 결정.
        var outside = new bool[w * h];
        var queue = new Queue<int>();
        void Seed(int idx)
        {
            if (idx < 0 || idx >= outside.Length) return;
            if (outside[idx] || isBoundary[idx]) return;
            outside[idx] = true;
            queue.Enqueue(idx);
        }
        for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
        for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + (w - 1)); }
        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int x = idx % w;
            int y = idx / w;
            if (x > 0) Seed(idx - 1);
            if (x < w - 1) Seed(idx + 1);
            if (y > 0) Seed(idx - w);
            if (y < h - 1) Seed(idx + w);
        }

        // 4) outside는 투명(알파 0), 나머지는 source 픽셀 복사 (알파 255).
        var masked = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, w, h);
        var srcData = croppedBitmap.LockBits(rect,
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var dstData = masked.LockBits(rect,
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        int stride = srcData.Stride;
        var buf = new byte[stride * h];
        System.Runtime.InteropServices.Marshal.Copy(srcData.Scan0, buf, 0, buf.Length);
        for (int y = 0; y < h; y++)
        {
            int rowOff = y * stride;
            int maskRow = y * w;
            for (int x = 0; x < w; x++)
            {
                int idx = rowOff + x * 4;
                if (outside[maskRow + x])
                {
                    buf[idx] = 0; buf[idx + 1] = 0; buf[idx + 2] = 0; buf[idx + 3] = 0;
                }
                else
                {
                    buf[idx + 3] = 255;
                }
            }
        }
        System.Runtime.InteropServices.Marshal.Copy(buf, 0, dstData.Scan0, buf.Length);
        croppedBitmap.UnlockBits(srcData);
        masked.UnlockBits(dstData);
        return masked;
    }

    /// <summary>4-connected Bresenham 변형: 매 step에 x 또는 y 한 축만 이동.</summary>
    private static void DrawLine4(bool[] mask, int w, int h, int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        while (true)
        {
            if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h)
                mask[y0 * w + x0] = true;
            if (x0 == x1 && y0 == y1) break;
            int e2 = err * 2;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            else if (e2 < dx) { err += dx; y0 += sy; }
        }
    }
}
