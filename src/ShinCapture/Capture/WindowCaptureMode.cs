using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Helpers;
using MediaPen = System.Windows.Media.Pen;

namespace ShinCapture.Capture;

public class WindowCaptureMode : ICaptureMode
{
    private Bitmap? _screenBitmap;
    private double _scaleX = 1.0;
    private double _scaleY = 1.0;
    private double _vsLeft;  // 가상 스크린 좌상단 (물리 좌표)
    private double _vsTop;

    // 오버레이 표시 전 수집한 윈도우 목록
    private readonly List<(IntPtr hwnd, NativeMethods.RECT rect, string title)> _windows = new();
    private IntPtr _overlayHwnd;

    private NativeMethods.RECT _windowRect;
    private bool _hasWindow;

    public bool IsComplete { get; private set; }
    public bool IsCancelled { get; private set; }

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _screenBitmap = screenBitmap;

        if (overlay.ActualWidth > 0 && screenBitmap.Width > 0)
            _scaleX = screenBitmap.Width / overlay.ActualWidth;
        if (overlay.ActualHeight > 0 && screenBitmap.Height > 0)
            _scaleY = screenBitmap.Height / overlay.ActualHeight;

        // 가상 스크린 오프셋 (멀티모니터 대응)
        _vsLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        _vsTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);

        // 오버레이 핸들
        if (Window.GetWindow(overlay) is Window pw)
            _overlayHwnd = new System.Windows.Interop.WindowInteropHelper(pw).Handle;

        // 현재 보이는 윈도우 목록 수집 (오버레이 제외)
        EnumerateWindows();
    }

    private void EnumerateWindows()
    {
        _windows.Clear();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (hwnd == _overlayHwnd) return true;
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;
            if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return true;
            if (rect.Width <= 0 || rect.Height <= 0) return true;

            var sb = new StringBuilder(256);
            NativeMethods.GetWindowText(hwnd, sb, 256);
            var title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;

            _windows.Add((hwnd, rect, title));
            return true;
        }, IntPtr.Zero);
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && _hasWindow)
            IsComplete = true;
        else if (e.ChangedButton == MouseButton.Right)
            IsCancelled = true;
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        // 실제 커서 위치 (물리 픽셀)
        NativeMethods.GetCursorPos(out var pt);

        // 미리 수집한 윈도우 목록에서 커서 아래 윈도우 찾기 (Z-order: 앞에서부터)
        _hasWindow = false;
        foreach (var (_, rect, _) in _windows)
        {
            if (pt.X >= rect.Left && pt.X <= rect.Left + rect.Width &&
                pt.Y >= rect.Top && pt.Y <= rect.Top + rect.Height)
            {
                _windowRect = rect;
                _hasWindow = true;
                break;
            }
        }
    }

    public void OnMouseUp(MouseButtonEventArgs e) { }

    public void Render(DrawingContext dc, double overlayWidth, double overlayHeight)
    {
        if (!_hasWindow) return;

        double logLeft   = (_windowRect.Left - _vsLeft)   / _scaleX;
        double logTop    = (_windowRect.Top  - _vsTop)   / _scaleY;
        double logRight  = (_windowRect.Left + _windowRect.Width - _vsLeft) / _scaleX;
        double logBottom = (_windowRect.Top + _windowRect.Height - _vsTop)  / _scaleY;

        logLeft   = Math.Max(0, logLeft);
        logTop    = Math.Max(0, logTop);
        logRight  = Math.Min(overlayWidth, logRight);
        logBottom = Math.Min(overlayHeight, logBottom);

        if (logRight <= logLeft || logBottom <= logTop) return;

        var highlightRect = new Rect(logLeft, logTop, logRight - logLeft, logBottom - logTop);

        // 전체 어둡게
        var dimBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 0, 0, 0));
        dc.DrawRectangle(dimBrush, null, new Rect(0, 0, overlayWidth, overlayHeight));

        // 선택 윈도우 하이라이트
        var clearBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x4D, 0x00, 0x78, 0xD4));
        var borderPen = new MediaPen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4)), 2.0);
        dc.DrawRectangle(clearBrush, borderPen, highlightRect);
    }

    public Rectangle? GetSelectedRegion()
    {
        if (!IsComplete) return null;

        // 물리 좌표 → 비트맵 좌표 (가상 스크린 오프셋 제거)
        int left   = Math.Max(0, _windowRect.Left - (int)_vsLeft);
        int top    = Math.Max(0, _windowRect.Top  - (int)_vsTop);
        int right  = _windowRect.Left + _windowRect.Width - (int)_vsLeft;
        int bottom = _windowRect.Top + _windowRect.Height - (int)_vsTop;

        if (_screenBitmap != null)
        {
            right  = Math.Min(_screenBitmap.Width, right);
            bottom = Math.Min(_screenBitmap.Height, bottom);
        }

        return new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
