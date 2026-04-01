using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Capture;
using ShinCapture.Helpers;
using ShinCapture.Models;

namespace ShinCapture.Views.Overlay;

public partial class CaptureOverlay : Window
{
    private ICaptureMode? _mode;
    private Bitmap? _screenBitmap;
    private readonly CaptureSettings _settings;

    // Custom render host
    private readonly RenderDrawingVisual _renderVisual = new();

    public CaptureResult? Result { get; private set; }

    public CaptureOverlay(CaptureSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        // Add render visual to DrawCanvas
        DrawCanvas.Children.Add(_renderVisual);

        KeyDown += OnKeyDown;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        SizeChanged += (_, _) => Redraw();
    }

    // ── Public API ──────────────────────────────────────────────────

    public void Start(ICaptureMode mode)
    {
        _mode = mode;

        // 1. Capture the full virtual screen
        _screenBitmap = ScreenHelper.CaptureFullScreen();

        // 2. Position the overlay to cover all monitors
        var vl = SystemParameters.VirtualScreenLeft;
        var vt = SystemParameters.VirtualScreenTop;
        var vw = SystemParameters.VirtualScreenWidth;
        var vh = SystemParameters.VirtualScreenHeight;

        Left = vl;
        Top = vt;
        Width = vw;
        Height = vh;

        // 3. Show the frozen screen
        ScreenImage.Source = BitmapHelper.ToBitmapSource(_screenBitmap);

        // 4. Show and activate
        Show();
        Activate();
        Focus();

        // 5. Initialize mode (after layout so ActualWidth/Height are valid)
        Dispatcher.InvokeAsync(() =>
        {
            _mode.Initialize(_screenBitmap, RootGrid);

            // FullscreenCaptureMode is immediately complete
            if (_mode.IsComplete)
                FinishCapture();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ── Mouse events ────────────────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _mode?.OnMouseDown(e);
        Redraw();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        _mode?.OnMouseMove(e);
        Redraw();

        // 모드가 요청하는 커서 적용 (없으면 기본 Cross)
        Cursor = _mode?.RequestedCursor ?? Cursors.Cross;

        if (_settings.ShowCrosshair)
            UpdateCrosshair(e.GetPosition(this));
        if (_settings.ShowColorCode || true)
            UpdateMagnifier(e.GetPosition(this));

        if (_mode?.IsComplete == true)
            FinishCapture();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _mode?.OnMouseUp(e);
        Redraw();

        if (_mode?.IsComplete == true)
            FinishCapture();
    }

    // ── Keyboard ────────────────────────────────────────────────────

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = null;
            Close();
            return;
        }

        // 모드에 키 이벤트 전달 (엔터로 캡쳐 등)
        _mode?.OnKeyDown(e);

        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            if (_mode?.IsComplete == true)
                FinishCapture();
        }
    }

    // ── Rendering ───────────────────────────────────────────────────

    private void Redraw()
    {
        if (_mode == null) return;

        using var dc = _renderVisual.RenderOpen();

        // Clear any previous dim overlay by covering it transparently
        // The dim overlay rectangle (DimOverlay) shows global dim.
        // Mode renders its own dim/selection on top via dc.
        _mode.Render(dc, ActualWidth, ActualHeight);
    }

    // ── Magnifier ───────────────────────────────────────────────────

    private void UpdateMagnifier(System.Windows.Point pos)
    {
        if (_screenBitmap == null) return;

        MagnifierBorder.Visibility = Visibility.Visible;

        // Compute scale from overlay logical to screen pixels
        double scaleX = _screenBitmap.Width / ActualWidth;
        double scaleY = _screenBitmap.Height / ActualHeight;

        int px = (int)(pos.X * scaleX);
        int py = (int)(pos.Y * scaleY);

        // Clamp
        px = Math.Clamp(px, 0, _screenBitmap.Width - 1);
        py = Math.Clamp(py, 0, _screenBitmap.Height - 1);

        // Coordinates label
        MagCoordText.Text = $"X: {px}  Y: {py}";

        // Color under cursor
        var color = ScreenHelper.GetPixelColor(_screenBitmap, px, py);
        var hex = ScreenHelper.ColorToHex(color);
        MagColorText.Text = hex;
        MagColorText.Background = new SolidColorBrush(
            System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
        MagColorText.Foreground = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) > 128
            ? System.Windows.Media.Brushes.Black
            : System.Windows.Media.Brushes.White;

        // Zoom region: MagnifierZoom * 100px area → 200px display
        int zoom = Math.Max(1, _settings.MagnifierZoom);
        int halfW = 100 / zoom;   // half-width in screen pixels to sample
        int halfH = 100 / zoom;

        int srcX = Math.Max(0, px - halfW);
        int srcY = Math.Max(0, py - halfH);
        int srcW = Math.Min(_screenBitmap.Width - srcX, halfW * 2);
        int srcH = Math.Min(_screenBitmap.Height - srcY, halfH * 2);

        if (srcW > 0 && srcH > 0)
        {
            using var cropped = ScreenHelper.CropBitmap(_screenBitmap,
                new Rectangle(srcX, srcY, srcW, srcH));
            MagnifierImage.Source = BitmapHelper.ToBitmapSource(cropped);
        }

        // Position magnifier near cursor (offset so it doesn't cover the cursor)
        const double offsetX = 20;
        const double offsetY = 20;
        double mx = pos.X + offsetX;
        double my = pos.Y + offsetY;

        if (mx + 200 > ActualWidth) mx = pos.X - 220;
        if (my + 200 > ActualHeight) my = pos.Y - 220;

        System.Windows.Controls.Canvas.SetLeft(MagnifierBorder, mx);
        System.Windows.Controls.Canvas.SetTop(MagnifierBorder, my);

        // MagnifierBorder is in RootGrid so use Margin instead
        MagnifierBorder.HorizontalAlignment = HorizontalAlignment.Left;
        MagnifierBorder.VerticalAlignment = VerticalAlignment.Top;
        MagnifierBorder.Margin = new Thickness(mx, my, 0, 0);
    }

    // ── Crosshair ───────────────────────────────────────────────────

    private void UpdateCrosshair(System.Windows.Point pos)
    {
        CrosshairH.Visibility = Visibility.Visible;
        CrosshairV.Visibility = Visibility.Visible;

        // Horizontal line across full width
        CrosshairH.X1 = 0;
        CrosshairH.Y1 = pos.Y;
        CrosshairH.X2 = ActualWidth;
        CrosshairH.Y2 = pos.Y;

        // Vertical line across full height
        CrosshairV.X1 = pos.X;
        CrosshairV.Y1 = 0;
        CrosshairV.X2 = pos.X;
        CrosshairV.Y2 = ActualHeight;
    }

    // ── Finish ──────────────────────────────────────────────────────

    private void FinishCapture()
    {
        if (_screenBitmap == null || _mode == null) return;

        var region = _mode.GetSelectedRegion();
        if (region == null || region.Value.Width <= 0 || region.Value.Height <= 0)
        {
            Result = null;
            Close();
            return;
        }

        var cropped = ScreenHelper.CropBitmap(_screenBitmap, region.Value);
        Result = new CaptureResult
        {
            Image = cropped,
            Region = region.Value
        };

        Close();
    }
}

// ── Helpers ─────────────────────────────────────────────────────────

/// <summary>A DrawingVisual that can be hosted in a WPF Canvas.</summary>
internal sealed class RenderDrawingVisual : System.Windows.UIElement
{
    private readonly DrawingVisual _visual = new();

    public RenderDrawingVisual()
    {
        AddVisualChild(_visual);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    public DrawingContext RenderOpen() => _visual.RenderOpen();

    protected override System.Windows.Size MeasureCore(System.Windows.Size availableSize)
        => new System.Windows.Size(
            double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
}
