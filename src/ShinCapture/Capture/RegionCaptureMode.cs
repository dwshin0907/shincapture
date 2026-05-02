using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MediaPen = System.Windows.Media.Pen;

namespace ShinCapture.Capture;

public class RegionCaptureMode : ICaptureMode
{
    private Bitmap? _screenBitmap;
    private FrameworkElement? _overlay;

    private bool _isDragging = false;
    private System.Windows.Point _startPoint;
    private System.Windows.Point _endPoint;

    public bool IsComplete { get; private set; } = false;
    public bool IsCancelled { get; private set; } = false;

    // Scale factor: overlay logical pixels → screen physical pixels
    private double _scaleX = 1.0;
    private double _scaleY = 1.0;

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _screenBitmap = screenBitmap;
        _overlay = overlay;

        // Compute scale: screen px / overlay logical px
        if (overlay.ActualWidth > 0 && screenBitmap.Width > 0)
            _scaleX = screenBitmap.Width / overlay.ActualWidth;
        if (overlay.ActualHeight > 0 && screenBitmap.Height > 0)
            _scaleY = screenBitmap.Height / overlay.ActualHeight;
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _startPoint = e.GetPosition(_overlay);
            _endPoint = _startPoint;
            _isDragging = true;
            IsComplete = false;
        }
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        if (_isDragging)
        {
            _endPoint = e.GetPosition(_overlay);
        }
    }

    public void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Released)
        {
            _endPoint = e.GetPosition(_overlay);
            _isDragging = false;

            var rect = GetLogicalRect();
            if (rect.Width > 5 && rect.Height > 5)
                IsComplete = true;
        }
    }

    public void Render(DrawingContext dc, double overlayWidth, double overlayHeight)
    {
        if (!_isDragging && !IsComplete) return;

        var selRect = GetLogicalRect();

        var dimBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 0, 0, 0));

        // Top strip
        dc.DrawRectangle(dimBrush, null,
            new Rect(0, 0, overlayWidth, selRect.Top));
        // Bottom strip
        dc.DrawRectangle(dimBrush, null,
            new Rect(0, selRect.Bottom, overlayWidth, overlayHeight - selRect.Bottom));
        // Left strip
        dc.DrawRectangle(dimBrush, null,
            new Rect(0, selRect.Top, selRect.Left, selRect.Height));
        // Right strip
        dc.DrawRectangle(dimBrush, null,
            new Rect(selRect.Right, selRect.Top, overlayWidth - selRect.Right, selRect.Height));

        // Selection border
        var borderPen = new MediaPen(System.Windows.Media.Brushes.White, 1.0);
        borderPen.DashStyle = DashStyles.Solid;
        dc.DrawRectangle(null, borderPen, selRect);

        // Size label
        if (selRect.Width > 40 && selRect.Height > 20)
        {
            var region = GetSelectedRegion();
            if (region != null)
            {
                var label = $"{region.Value.Width} × {region.Value.Height}";
                var ft = new FormattedText(
                    label,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    12,
                    System.Windows.Media.Brushes.White,
                    96);

                var labelX = selRect.Left + (selRect.Width - ft.Width) / 2;
                var labelY = selRect.Top + (selRect.Height - ft.Height) / 2;
                dc.DrawText(ft, new System.Windows.Point(labelX, labelY));
            }
        }
    }

    public Rectangle? GetSelectedRegion()
    {
        if (!_isDragging && !IsComplete) return null;

        var rect = GetLogicalRect();
        // Math.Round로 floor 누적 오차 제거 — DPI 1.25/1.5 등에서 1~2px 위/왼쪽 어긋남 방지
        int x = (int)Math.Round(rect.Left * _scaleX);
        int y = (int)Math.Round(rect.Top * _scaleY);
        int right = (int)Math.Round((rect.Left + rect.Width) * _scaleX);
        int bottom = (int)Math.Round((rect.Top + rect.Height) * _scaleY);
        return new Rectangle(x, y, right - x, bottom - y);
    }

    private Rect GetLogicalRect()
    {
        double x = Math.Min(_startPoint.X, _endPoint.X);
        double y = Math.Min(_startPoint.Y, _endPoint.Y);
        double w = Math.Abs(_endPoint.X - _startPoint.X);
        double h = Math.Abs(_endPoint.Y - _startPoint.Y);
        return new Rect(x, y, w, h);
    }
}
