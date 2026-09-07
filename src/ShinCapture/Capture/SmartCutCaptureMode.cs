using System;
using System.Drawing;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MediaPen = System.Windows.Media.Pen;

namespace ShinCapture.Capture;

public class SmartCutCaptureMode : ICaptureMode
{
    private Bitmap? _screenBitmap;
    private FrameworkElement? _overlay;
    private double _scaleX = 1, _scaleY = 1;
    private bool _isDrawing;
    private readonly List<System.Windows.Point> _points = new();
    public bool IsComplete { get; private set; }
    public bool IsCancelled { get; private set; }

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _screenBitmap = screenBitmap;
        _overlay = overlay;
        _scaleX = overlay.ActualWidth > 0 ? screenBitmap.Width / overlay.ActualWidth : 1;
        _scaleY = overlay.ActualHeight > 0 ? screenBitmap.Height / overlay.ActualHeight : 1;
        _points.Clear();
        _isDrawing = false;
        IsComplete = false;
        IsCancelled = false;
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right) { Cancel(); return; }
        if (IsCancelled || e.LeftButton != MouseButtonState.Pressed || _overlay == null) return;
        _points.Clear(); IsComplete = false; _isDrawing = true;
        _points.Add(e.GetPosition(_overlay));
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        if (_isDrawing && !IsCancelled && _overlay != null)
        {
            System.Windows.Point point = e.GetPosition(_overlay);
            if (_points.Count == 0 || (point - _points[^1]).LengthSquared >= 1) _points.Add(point);
        }
    }

    public void OnMouseUp(MouseButtonEventArgs e)
    {
        if (!_isDrawing || e.ChangedButton != MouseButton.Left) return;
        if (_overlay != null)
        {
            System.Windows.Point point = e.GetPosition(_overlay);
            if (_points.Count == 0 || (point - _points[^1]).LengthSquared >= 1) _points.Add(point);
        }
        _isDrawing = false;
        IsComplete = SmartCutGeometry.TryCloseAndValidate(_points, _overlay?.ActualWidth ?? 0, _overlay?.ActualHeight ?? 0, out List<System.Windows.Point> closed);
        if (IsComplete) { _points.Clear(); _points.AddRange(closed); }
    }

    public void OnKeyDown(KeyEventArgs e) { if (e.Key == Key.Escape) Cancel(); }

    public void Cancel() { IsCancelled = true; IsComplete = false; _isDrawing = false; }

    public void Render(DrawingContext dc, double overlayWidth, double overlayHeight)
    {
        if (_points.Count < 2) return;
        dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 0, 0, 0)), null, new Rect(0, 0, overlayWidth, overlayHeight));
        var pen = new MediaPen(System.Windows.Media.Brushes.Magenta, 2) { DashStyle = DashStyles.Dash };
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(_points[0], false, false);
            for (int i = 1; i < _points.Count; i++) context.LineTo(_points[i], true, false);
        }
        geometry.Freeze(); dc.DrawGeometry(null, pen, geometry);
    }

    public Rectangle? GetSelectedRegion()
    {
        if (!IsComplete || _screenBitmap == null) return null;
        Rectangle region = SmartCutGeometry.ComputeClampedRegion(_points, _scaleX, _scaleY, _screenBitmap.Width, _screenBitmap.Height);
        return region.IsEmpty ? null : region;
    }

    public Bitmap ApplyGrabCut(Bitmap croppedBitmap, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(croppedBitmap);
        cancellationToken.ThrowIfCancellationRequested();
        Rectangle? selected = GetSelectedRegion();
        if (!IsComplete || selected is not Rectangle region) return croppedBitmap;
        return ApplyGrabCut(croppedBitmap, GetLocalPolygon(region), cancellationToken);
    }

    public PointF[] GetLocalPolygon(Rectangle region) =>
        SmartCutGeometry.ToLocalPolygon(_points.ToArray(), _scaleX, _scaleY, region);

    public Bitmap ApplyGrabCut(Bitmap croppedBitmap, IReadOnlyList<PointF> localPolygon, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(croppedBitmap);
        ArgumentNullException.ThrowIfNull(localPolygon);
        return SmartCutProcessor.Process(croppedBitmap, localPolygon, cancellationToken);
    }
}
