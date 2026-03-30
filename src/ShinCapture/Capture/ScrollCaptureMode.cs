using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using ShinCapture.Helpers;

namespace ShinCapture.Capture;

/// <summary>
/// Phase 1: delegates to RegionCaptureMode for initial area selection.
/// Phase 2: once region is selected, auto-scrolls the window under the region,
///           captures frames, compares consecutive frames, stitches them vertically.
/// </summary>
public class ScrollCaptureMode : ICaptureMode
{
    private readonly RegionCaptureMode _regionMode = new();
    private Bitmap? _screenBitmap;
    private FrameworkElement? _overlay;

    private Bitmap? _stitchedBitmap;
    private bool _scrollDone = false;

    public bool IsComplete => _scrollDone || (_regionMode.IsCancelled);
    public bool IsCancelled => _regionMode.IsCancelled;

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _screenBitmap = screenBitmap;
        _overlay = overlay;
        _regionMode.Initialize(screenBitmap, overlay);
    }

    public void OnMouseDown(MouseButtonEventArgs e) => _regionMode.OnMouseDown(e);
    public void OnMouseMove(MouseEventArgs e) => _regionMode.OnMouseMove(e);

    public void OnMouseUp(MouseButtonEventArgs e)
    {
        _regionMode.OnMouseUp(e);

        if (_regionMode.IsComplete && !_scrollDone)
        {
            var region = _regionMode.GetSelectedRegion();
            if (region != null && region.Value.Width > 0 && region.Value.Height > 0)
            {
                PerformScrollCapture(region.Value);
            }
            _scrollDone = true;
        }
    }

    public void Render(System.Windows.Media.DrawingContext dc, double overlayWidth, double overlayHeight)
        => _regionMode.Render(dc, overlayWidth, overlayHeight);

    public Rectangle? GetSelectedRegion()
    {
        if (_stitchedBitmap != null)
            return new Rectangle(0, 0, _stitchedBitmap.Width, _stitchedBitmap.Height);
        return _regionMode.GetSelectedRegion();
    }

    /// <summary>Returns the stitched tall bitmap, or null if unavailable.</summary>
    public Bitmap? GetStitchedBitmap() => _stitchedBitmap;

    // ── Scroll capture logic ─────────────────────────────────────────

    private void PerformScrollCapture(Rectangle region)
    {
        const int maxScrolls = 50;

        // Find the window under the centre of the selected region
        var centre = new NativeMethods.POINT(
            region.Left + region.Width  / 2,
            region.Top  + region.Height / 2);
        var hwnd = NativeMethods.WindowFromPoint(centre);
        if (hwnd == IntPtr.Zero) hwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);

        var frames = new List<Bitmap>();

        // Capture initial frame
        var first = ScreenHelper.CaptureFullScreen();
        frames.Add(ScreenHelper.CropBitmap(first, region));
        first.Dispose();

        for (int i = 0; i < maxScrolls; i++)
        {
            // Scroll page down
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_VSCROLL,
                new IntPtr(NativeMethods.SB_PAGEDOWN), IntPtr.Zero);

            // Wait for the scroll to settle
            Thread.Sleep(150);

            // Capture new frame
            var full = ScreenHelper.CaptureFullScreen();
            var frame = ScreenHelper.CropBitmap(full, region);
            full.Dispose();

            // Compare with last captured frame (sample every 10th pixel)
            if (FramesIdentical(frames[frames.Count - 1], frame))
            {
                frame.Dispose();
                break;  // reached bottom
            }

            frames.Add(frame);
        }

        _stitchedBitmap = StitchVertically(frames);

        // Dispose intermediates
        foreach (var f in frames) f.Dispose();
    }

    private static bool FramesIdentical(Bitmap a, Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return false;

        for (int y = 0; y < a.Height; y += 10)
        {
            for (int x = 0; x < a.Width; x += 10)
            {
                if (a.GetPixel(x, y) != b.GetPixel(x, y))
                    return false;
            }
        }
        return true;
    }

    private static Bitmap StitchVertically(List<Bitmap> frames)
    {
        if (frames.Count == 0) return new Bitmap(1, 1);

        int width  = frames[0].Width;
        int totalH = 0;
        foreach (var f in frames) totalH += f.Height;

        var result = new Bitmap(width, totalH, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);

        int yOffset = 0;
        foreach (var f in frames)
        {
            g.DrawImage(f, 0, yOffset);
            yOffset += f.Height;
        }
        return result;
    }
}
