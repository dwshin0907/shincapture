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
        return new Rectangle(
            (int)(bounds.Left   * _scaleX),
            (int)(bounds.Top    * _scaleY),
            (int)(bounds.Width  * _scaleX),
            (int)(bounds.Height * _scaleY));
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
}
