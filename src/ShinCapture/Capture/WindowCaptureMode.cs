using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Helpers;
using MediaPen = System.Windows.Media.Pen;

namespace ShinCapture.Capture;

public class WindowCaptureMode : ICaptureMode
{
    private Bitmap? _screenBitmap;
    private FrameworkElement? _overlay;

    private double _scaleX = 1.0;
    private double _scaleY = 1.0;

    private NativeMethods.RECT _windowRect;
    private bool _hasWindow = false;

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
        if (e.ChangedButton == MouseButton.Left && _hasWindow)
        {
            IsComplete = true;
        }
        else if (e.ChangedButton == MouseButton.Right)
        {
            IsCancelled = true;
        }
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        NativeMethods.GetCursorPos(out var cursorPoint);
        var hwnd = NativeMethods.WindowFromPoint(cursorPoint);
        if (hwnd != IntPtr.Zero)
        {
            var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            if (root == IntPtr.Zero) root = hwnd;
            if (NativeMethods.GetWindowRect(root, out var rect))
            {
                _windowRect = rect;
                _hasWindow = true;
                return;
            }
        }
        _hasWindow = false;
    }

    public void OnMouseUp(MouseButtonEventArgs e) { }

    public void Render(System.Windows.Media.DrawingContext dc, double overlayWidth, double overlayHeight)
    {
        if (!_hasWindow) return;

        // Convert screen physical pixels to overlay logical coords
        double logLeft   = _windowRect.Left   / _scaleX;
        double logTop    = _windowRect.Top     / _scaleY;
        double logRight  = _windowRect.Right   / _scaleX;
        double logBottom = _windowRect.Bottom  / _scaleY;

        // Clamp to overlay bounds
        logLeft   = Math.Max(0, logLeft);
        logTop    = Math.Max(0, logTop);
        logRight  = Math.Min(overlayWidth, logRight);
        logBottom = Math.Min(overlayHeight, logBottom);

        if (logRight <= logLeft || logBottom <= logTop) return;

        var highlightRect = new Rect(logLeft, logTop, logRight - logLeft, logBottom - logTop);

        // Dim entire screen
        var dimBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 0, 0, 0));
        dc.DrawRectangle(dimBrush, null, new Rect(0, 0, overlayWidth, overlayHeight));

        // Blue 30% fill highlight over detected window
        var fillBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x4D, 0x00, 0x78, 0xD4));
        var borderPen = new MediaPen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4)), 2.0);
        dc.DrawRectangle(fillBrush, borderPen, highlightRect);
    }

    public Rectangle? GetSelectedRegion()
    {
        if (!IsComplete) return null;

        // Clamp to screen
        int left   = Math.Max(0, _windowRect.Left);
        int top    = Math.Max(0, _windowRect.Top);
        int right  = _screenBitmap != null ? Math.Min(_screenBitmap.Width,  _windowRect.Right)  : _windowRect.Right;
        int bottom = _screenBitmap != null ? Math.Min(_screenBitmap.Height, _windowRect.Bottom) : _windowRect.Bottom;

        return new Rectangle(left, top, right - left, bottom - top);
    }
}
