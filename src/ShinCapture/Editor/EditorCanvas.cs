using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor;

public class EditorCanvas : Canvas
{
    private BitmapSource? _backgroundImage;
    private readonly List<EditorObject> _objects = new();
    private ITool? _currentTool;
    private bool _isInteracting;

    private double _zoom = 1.0;
    private Vector _pan;
    private Point _panStart;
    private bool _isPanning;

    public double Zoom
    {
        get => _zoom;
        set { _zoom = Math.Clamp(value, 0.25, 4.0); InvalidateVisual(); ZoomChanged?.Invoke(this, _zoom); }
    }

    public event EventHandler<double>? ZoomChanged;
    public event EventHandler<IEditorCommand>? CommandRequested;

    public BitmapSource? BackgroundImage
    {
        get => _backgroundImage;
        set { _backgroundImage = value; FitToView(); InvalidateVisual(); }
    }

    public List<EditorObject> Objects => _objects;
    public void SetTool(ITool? tool) => _currentTool = tool;

    public void FitToView()
    {
        if (_backgroundImage == null || ActualWidth == 0) return;
        var zoomW = ActualWidth / _backgroundImage.PixelWidth;
        var zoomH = ActualHeight / _backgroundImage.PixelHeight;
        _zoom = Math.Min(zoomW, zoomH) * 0.9;
        _pan = new Vector(
            (ActualWidth - _backgroundImage.PixelWidth * _zoom) / 2,
            (ActualHeight - _backgroundImage.PixelHeight * _zoom) / 2);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        // Draw canvas background using theme resource
        var bgBrush = TryFindResource("BackgroundCanvasBrush") as Brush ?? Brushes.LightGray;
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        dc.PushTransform(new TranslateTransform(_pan.X, _pan.Y));
        dc.PushTransform(new ScaleTransform(_zoom, _zoom));

        if (_backgroundImage != null)
            dc.DrawImage(_backgroundImage, new Rect(0, 0, _backgroundImage.PixelWidth, _backgroundImage.PixelHeight));

        foreach (var obj in _objects.Where(o => o.IsVisible))
            obj.Render(dc);

        _currentTool?.RenderPreview(dc);

        dc.Pop();
        dc.Pop();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _isPanning = true; _panStart = e.GetPosition(this); CaptureMouse(); return;
        }
        _isInteracting = true;
        _currentTool?.OnMouseDown(ScreenToImage(e.GetPosition(this)), e);
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_isPanning)
        {
            var current = e.GetPosition(this);
            _pan += current - _panStart; _panStart = current; InvalidateVisual(); return;
        }
        if (_isInteracting)
        {
            _currentTool?.OnMouseMove(ScreenToImage(e.GetPosition(this)), e);
            InvalidateVisual();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (_isPanning) { _isPanning = false; ReleaseMouseCapture(); return; }
        if (_isInteracting)
        {
            _isInteracting = false;
            _currentTool?.OnMouseUp(ScreenToImage(e.GetPosition(this)), e);
            var cmd = _currentTool?.GetCommand();
            if (cmd != null) CommandRequested?.Invoke(this, cmd);
            InvalidateVisual();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        Zoom *= e.Delta > 0 ? 1.1 : 0.9;
    }

    private Point ScreenToImage(Point screenPoint) =>
        new((screenPoint.X - _pan.X) / _zoom, (screenPoint.Y - _pan.Y) / _zoom);
}
