using System.Drawing;
using System.Windows;
using System.Windows.Input;

namespace ShinCapture.Capture;

public class FullscreenCaptureMode : ICaptureMode
{
    private Bitmap? _screenBitmap;

    public bool IsComplete { get; private set; } = false;
    public bool IsCancelled { get; private set; } = false;

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _screenBitmap = screenBitmap;
        IsComplete = true;
    }

    public void OnMouseDown(MouseButtonEventArgs e) { }
    public void OnMouseMove(MouseEventArgs e) { }
    public void OnMouseUp(MouseButtonEventArgs e) { }

    public void Render(System.Windows.Media.DrawingContext dc, double overlayWidth, double overlayHeight) { }

    public Rectangle? GetSelectedRegion()
    {
        if (_screenBitmap == null) return null;
        return new Rectangle(0, 0, _screenBitmap.Width, _screenBitmap.Height);
    }
}
