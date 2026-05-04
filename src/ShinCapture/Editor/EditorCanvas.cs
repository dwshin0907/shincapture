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
    private List<EditorObject> _objects = new();
    private ITool? _currentTool;
    private bool _isInteracting;

    private double _zoom = 1.0;
    private const double Padding = 20;

    // 중클릭 팬 (ScrollViewer 오프셋 조절)
    private Point _panStart;
    private bool _isPanning;

    // 개체 드래그 이동
    private EditorObject? _draggingObject;
    private Point _dragLastPos;

    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 0.1, 8.0);
            UpdateCanvasSize();
            InvalidateVisual();
            var dpiScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            ZoomChanged?.Invoke(this, _zoom * dpiScale);
        }
    }

    public event EventHandler<double>? ZoomChanged;
    public event EventHandler<IEditorCommand>? CommandRequested;

    public BitmapSource? BackgroundImage
    {
        get => _backgroundImage;
        set
        {
            _backgroundImage = value;
            // Layout pass 직후 우선순위(Loaded)로 FitToView 호출 — viewport 갱신은
            // 호출자가 UpdateLayout + CanvasScroller.UpdateLayout으로 보장.
            Dispatcher.BeginInvoke(new Action(FitToView),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    public List<EditorObject> Objects { get => _objects; set => _objects = value; }
    public void SetTool(ITool? tool) => _currentTool = tool;

    public void SubmitExternalCommand(IEditorCommand cmd)
    {
        CommandRequested?.Invoke(this, cmd);
        InvalidateVisual();
    }

    private ScrollViewer? GetScrollViewer()
    {
        var parent = VisualTreeHelper.GetParent(this);
        while (parent != null)
        {
            if (parent is ScrollViewer sv) return sv;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private void UpdateCanvasSize()
    {
        if (_backgroundImage == null) return;
        Width = _backgroundImage.PixelWidth * _zoom + Padding * 2;
        Height = _backgroundImage.PixelHeight * _zoom + Padding * 2;
    }

    public void FitToView()
    {
        if (_backgroundImage == null) return;

        var dpi = VisualTreeHelper.GetDpi(this);
        double dpiScale = dpi.PixelsPerDip;

        var sv = GetScrollViewer();
        double viewW = sv?.ViewportWidth ?? ActualWidth;
        double viewH = sv?.ViewportHeight ?? ActualHeight;
        if (viewW <= 0 || viewH <= 0)
        {
            // 아직 레이아웃 전 — 기본 100%
            _zoom = 1.0 / dpiScale;
            UpdateCanvasSize();
            InvalidateVisual();
            ZoomChanged?.Invoke(this, _zoom * dpiScale);
            return;
        }

        // 뷰포트에 맞는 줌 계산
        double zoomW = (viewW - Padding * 2) / _backgroundImage.PixelWidth;
        double zoomH = (viewH - Padding * 2) / _backgroundImage.PixelHeight;
        double fitZoom = Math.Min(zoomW, zoomH);

        // 100% 이하로만 축소 (100%보다 작은 이미지는 100%로)
        double zoom100 = 1.0 / dpiScale;
        _zoom = Math.Min(fitZoom, zoom100);

        UpdateCanvasSize();
        InvalidateVisual();
        ZoomChanged?.Invoke(this, _zoom * dpiScale);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var bgBrush = TryFindResource("BackgroundCanvasBrush") as Brush ?? Brushes.LightGray;
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        dc.PushTransform(new TranslateTransform(Padding, Padding));
        dc.PushTransform(new ScaleTransform(_zoom, _zoom));

        if (_backgroundImage != null)
            dc.DrawImage(_backgroundImage, new Rect(0, 0, _backgroundImage.PixelWidth, _backgroundImage.PixelHeight));

        foreach (var obj in _objects.Where(o => o.IsVisible))
            obj.RenderWithTransform(dc);

        _currentTool?.RenderPreview(dc);

        dc.Pop();
        dc.Pop();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var imgPos = ScreenToImage(e.GetPosition(this));

        // 중클릭: ScrollViewer 팬
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _isPanning = true;
            _panStart = e.GetPosition(GetScrollViewer() ?? (IInputElement)this);
            CaptureMouse();
            return;
        }

        // 우클릭: 개체 드래그 이동
        if (e.RightButton == MouseButtonState.Pressed)
        {
            var hit = HitTestObjects(imgPos);
            if (hit != null)
            {
                _draggingObject = hit;
                _dragLastPos = imgPos;
                CaptureMouse();
                e.Handled = true;
                return;
            }
        }

        // 좌클릭: 도구 동작
        _isInteracting = true;
        _currentTool?.OnMouseDown(imgPos, e);
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var imgPos = ScreenToImage(e.GetPosition(this));

        if (_isPanning)
        {
            var sv = GetScrollViewer();
            if (sv != null)
            {
                var current = e.GetPosition(sv);
                var delta = current - _panStart;
                sv.ScrollToHorizontalOffset(sv.HorizontalOffset - delta.X);
                sv.ScrollToVerticalOffset(sv.VerticalOffset - delta.Y);
                _panStart = current;
            }
            return;
        }

        if (_draggingObject != null)
        {
            var delta = imgPos - _dragLastPos;
            _draggingObject.Move(delta);
            _dragLastPos = imgPos;
            InvalidateVisual();
            return;
        }

        _currentTool?.OnMouseMove(imgPos, e);
        if (_isInteracting)
            InvalidateVisual();

        Cursor = _currentTool?.RequestedCursor;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isPanning) { _isPanning = false; ReleaseMouseCapture(); return; }

        if (_draggingObject != null)
        {
            _draggingObject = null;
            ReleaseMouseCapture();
            InvalidateVisual();
            return;
        }

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
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;

        double newZoom = _zoom * (e.Delta > 0 ? 1.1 : 0.9);

        var dpiScale = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double zoom100 = 1.0 / dpiScale;
        double ratio = newZoom / zoom100;
        if (ratio > 0.98 && ratio < 1.02)
            newZoom = zoom100;

        Zoom = newZoom;
        e.Handled = true;
    }

    private EditorObject? HitTestObjects(Point imagePos)
    {
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i].IsVisible && _objects[i].HitTestWithTransform(imagePos))
                return _objects[i];
        }
        return null;
    }

    private Point ScreenToImage(Point screenPoint) =>
        new((screenPoint.X - Padding) / _zoom, (screenPoint.Y - Padding) / _zoom);

    public Point ImageToScreen(Point imagePoint) =>
        new(imagePoint.X * _zoom + Padding, imagePoint.Y * _zoom + Padding);
}
