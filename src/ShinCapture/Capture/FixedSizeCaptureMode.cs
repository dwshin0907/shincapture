using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MediaPen = System.Windows.Media.Pen;

namespace ShinCapture.Capture;

public class FixedSizeCaptureMode : ICaptureMode
{
    private readonly int _width;
    private readonly int _height;

    private Bitmap? _screenBitmap;
    private FrameworkElement? _overlay;

    private double _scaleX = 1.0;
    private double _scaleY = 1.0;

    private System.Windows.Point _cursorPos;
    private bool _hasCursor = false;

    public bool IsComplete { get; private set; } = false;
    public bool IsCancelled { get; private set; } = false;

    public FixedSizeCaptureMode(int width, int height)
    {
        _width  = width;
        _height = height;
    }

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
        if (e.ChangedButton == MouseButton.Left)
        {
            _cursorPos = e.GetPosition(_overlay);
            _hasCursor = true;
            IsComplete = true;
        }
        else if (e.ChangedButton == MouseButton.Right)
        {
            IsCancelled = true;
        }
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        _cursorPos = e.GetPosition(_overlay);
        _hasCursor = true;
    }

    public void OnMouseUp(MouseButtonEventArgs e) { }

    public void Render(System.Windows.Media.DrawingContext dc, double overlayWidth, double overlayHeight)
    {
        if (!_hasCursor) return;

        // Frame in logical coords — fixed size, centered on cursor
        double frameW = _width  / _scaleX;
        double frameH = _height / _scaleY;
        double left   = _cursorPos.X - frameW / 2;
        double top    = _cursorPos.Y - frameH / 2;
        var frameRect = new Rect(left, top, frameW, frameH);

        // Dim outside
        var dimBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 0, 0, 0));
        // Top strip
        dc.DrawRectangle(dimBrush, null, new Rect(0, 0, overlayWidth, Math.Max(0, frameRect.Top)));
        // Bottom strip
        dc.DrawRectangle(dimBrush, null, new Rect(0, frameRect.Bottom, overlayWidth, Math.Max(0, overlayHeight - frameRect.Bottom)));
        // Left strip
        dc.DrawRectangle(dimBrush, null, new Rect(0, frameRect.Top, Math.Max(0, frameRect.Left), frameRect.Height));
        // Right strip
        dc.DrawRectangle(dimBrush, null, new Rect(frameRect.Right, frameRect.Top, Math.Max(0, overlayWidth - frameRect.Right), frameRect.Height));

        // Frame border (white dashed)
        var pen = new MediaPen(System.Windows.Media.Brushes.White, 1.5)
        {
            DashStyle = DashStyles.Dash
        };
        dc.DrawRectangle(null, pen, frameRect);

        // Label
        var label = $"{_width} × {_height}";
        var ft = new FormattedText(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            12,
            System.Windows.Media.Brushes.White,
            96);
        var lx = left + (frameW - ft.Width)  / 2;
        var ly = top  + (frameH - ft.Height) / 2;
        dc.DrawText(ft, new System.Windows.Point(lx, ly));
    }

    public Rectangle? GetSelectedRegion()
    {
        if (!IsComplete) return null;

        // Center in physical pixels
        int cx = (int)(_cursorPos.X * _scaleX);
        int cy = (int)(_cursorPos.Y * _scaleY);
        int left = cx - _width  / 2;
        int top  = cy - _height / 2;

        // Clamp to screen
        left = Math.Max(0, left);
        top  = Math.Max(0, top);
        int w = _screenBitmap != null ? Math.Min(_width,  _screenBitmap.Width  - left) : _width;
        int h = _screenBitmap != null ? Math.Min(_height, _screenBitmap.Height - top)  : _height;

        return new Rectangle(left, top, w, h);
    }
}
