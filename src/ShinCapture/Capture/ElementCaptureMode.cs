using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Helpers;
using MediaPen = System.Windows.Media.Pen;

namespace ShinCapture.Capture;

public class ElementCaptureMode : ICaptureMode
{
    private Bitmap? _screenBitmap;
    private FrameworkElement? _overlay;

    private double _scaleX = 1.0;
    private double _scaleY = 1.0;

    private System.Windows.Rect _elementBounds;
    private bool _hasElement = false;
    private long _lastLookupTimestamp;
    private int _lookupInProgress;
    private static readonly long LookupIntervalTicks = Stopwatch.Frequency / 20;

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
        if (e.ChangedButton == MouseButton.Left && _hasElement)
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
        NativeMethods.GetCursorPos(out var cursorPt);
        long now = Stopwatch.GetTimestamp();
        if (now - _lastLookupTimestamp < LookupIntervalTicks) return;
        _lastLookupTimestamp = now;
        if (Interlocked.CompareExchange(ref _lookupInProgress, 1, 0) != 0) return;
        LookupElementAsync(cursorPt);
    }

    private async void LookupElementAsync(NativeMethods.POINT cursorPoint)
    {
        try
        {
            System.Windows.Rect? bounds = await Task.Run(() => FindElementBounds(cursorPoint));
            if (bounds is { } found)
            {
                _elementBounds = found;
                _hasElement = true;
            }
            else
            {
                _hasElement = false;
            }
        }
        catch
        {
            _hasElement = false;
        }
        finally
        {
            Interlocked.Exchange(ref _lookupInProgress, 0);
        }
    }

    private static System.Windows.Rect? FindElementBounds(NativeMethods.POINT cursorPoint)
    {
        try
        {
            var element = AutomationElement.FromPoint(
                new System.Windows.Point(cursorPoint.X, cursorPoint.Y));
            if (element == null) return null;

            System.Windows.Rect bounds = element.Current.BoundingRectangle;
            return !bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0
                ? bounds
                : null;
        }
        catch
        {
            return null;
        }
    }

    public void OnMouseUp(MouseButtonEventArgs e) { }

    public void Render(System.Windows.Media.DrawingContext dc, double overlayWidth, double overlayHeight)
    {
        if (!_hasElement) return;

        // Convert screen physical pixel coords to overlay logical coords
        double logLeft   = _elementBounds.Left   / _scaleX;
        double logTop    = _elementBounds.Top     / _scaleY;
        double logWidth  = _elementBounds.Width   / _scaleX;
        double logHeight = _elementBounds.Height  / _scaleY;

        // Clamp
        logLeft  = Math.Max(0, logLeft);
        logTop   = Math.Max(0, logTop);
        logWidth  = Math.Min(overlayWidth  - logLeft, logWidth);
        logHeight = Math.Min(overlayHeight - logTop,  logHeight);

        if (logWidth <= 0 || logHeight <= 0) return;

        var highlightRect = new Rect(logLeft, logTop, logWidth, logHeight);

        // Dim entire screen
        var dimBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 0, 0, 0));
        dc.DrawRectangle(dimBrush, null, new Rect(0, 0, overlayWidth, overlayHeight));

        // Blue 30% fill highlight over detected element
        var fillBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x4D, 0x00, 0x78, 0xD4));
        var borderPen = new MediaPen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4)), 2.0);
        dc.DrawRectangle(fillBrush, borderPen, highlightRect);
    }

    public Rectangle? GetSelectedRegion()
    {
        if (!IsComplete) return null;

        int left   = (int)Math.Max(0, _elementBounds.Left);
        int top    = (int)Math.Max(0, _elementBounds.Top);
        int right  = _screenBitmap != null
            ? (int)Math.Min(_screenBitmap.Width,  _elementBounds.Right)
            : (int)_elementBounds.Right;
        int bottom = _screenBitmap != null
            ? (int)Math.Min(_screenBitmap.Height, _elementBounds.Bottom)
            : (int)_elementBounds.Bottom;

        return new Rectangle(left, top, right - left, bottom - top);
    }
}
