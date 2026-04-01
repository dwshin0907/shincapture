using System.Drawing;
using System.Windows;
using System.Windows.Input;

namespace ShinCapture.Capture;

public interface ICaptureMode
{
    void Initialize(Bitmap screenBitmap, FrameworkElement overlay);
    void OnMouseDown(MouseButtonEventArgs e);
    void OnMouseMove(MouseEventArgs e);
    void OnMouseUp(MouseButtonEventArgs e);
    void OnKeyDown(KeyEventArgs e) { }
    void Render(System.Windows.Media.DrawingContext dc, double overlayWidth, double overlayHeight);
    Rectangle? GetSelectedRegion();
    bool IsComplete { get; }
    bool IsCancelled { get; }
    Cursor? RequestedCursor => null;
}
