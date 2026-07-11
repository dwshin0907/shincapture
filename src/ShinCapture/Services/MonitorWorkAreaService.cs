using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ShinCapture.Helpers;

namespace ShinCapture.Services;

public readonly record struct MonitorWorkArea
{
    private readonly double _dpiScale;

    public MonitorWorkArea(
        int pixelLeft,
        int pixelTop,
        int pixelWidth,
        int pixelHeight,
        double dpiScale)
    {
        PixelLeft = pixelLeft;
        PixelTop = pixelTop;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        _dpiScale = double.IsFinite(dpiScale) && dpiScale > 0 ? dpiScale : 1.0;
    }

    public int PixelLeft { get; }
    public int PixelTop { get; }
    public int PixelWidth { get; }
    public int PixelHeight { get; }
    public double DpiScale => double.IsFinite(_dpiScale) && _dpiScale > 0 ? _dpiScale : 1.0;
    public int PixelRight => PixelLeft + PixelWidth;
    public int PixelBottom => PixelTop + PixelHeight;
    public double DipWidth => PixelWidth / DpiScale;
    public double DipHeight => PixelHeight / DpiScale;
}

public readonly record struct WindowPixelBounds(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

public static class MonitorWorkAreaService
{
    public static double ResolveDpiScale(uint nativeDpi, double visualDpiScale)
    {
        if (nativeDpi > 0)
            return nativeDpi / 96.0;

        return double.IsFinite(visualDpiScale) && visualDpiScale > 0
            ? visualDpiScale
            : 1.0;
    }

    public static MonitorWorkArea ConvertDipWorkAreaToPixels(
        double dipLeft,
        double dipTop,
        double dipWidth,
        double dipHeight,
        double dpiScale)
    {
        double safeScale = ResolveDpiScale(0, dpiScale);
        return new MonitorWorkArea(
            ToScaledCoordinate(dipLeft, safeScale),
            ToScaledCoordinate(dipTop, safeScale),
            ToScaledSize(dipWidth, safeScale),
            ToScaledSize(dipHeight, safeScale),
            safeScale);
    }

    public static WindowPixelBounds CalculateCenteredBounds(
        MonitorWorkArea area,
        double windowDipWidth,
        double windowDipHeight)
    {
        int workWidth = Math.Max(1, area.PixelWidth);
        int workHeight = Math.Max(1, area.PixelHeight);
        int width = ToPhysicalSize(windowDipWidth, area.DpiScale, workWidth);
        int height = ToPhysicalSize(windowDipHeight, area.DpiScale, workHeight);
        int left = area.PixelLeft + ((workWidth - width) / 2);
        int top = area.PixelTop + ((workHeight - height) / 2);

        return new WindowPixelBounds(left, top, width, height);
    }

    public static WindowPixelBounds ClampBounds(MonitorWorkArea area, WindowPixelBounds bounds)
    {
        int workWidth = Math.Max(1, area.PixelWidth);
        int workHeight = Math.Max(1, area.PixelHeight);
        int width = Math.Min(Math.Max(1, bounds.Width), workWidth);
        int height = Math.Min(Math.Max(1, bounds.Height), workHeight);
        int maxLeft = area.PixelLeft + workWidth - width;
        int maxTop = area.PixelTop + workHeight - height;
        int left = Math.Clamp(bounds.Left, area.PixelLeft, maxLeft);
        int top = Math.Clamp(bounds.Top, area.PixelTop, maxTop);

        return new WindowPixelBounds(left, top, width, height);
    }

    public static MonitorWorkArea GetForWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        double visualDpiScale = VisualTreeHelper.GetDpi(window).DpiScaleX;
        double dpiScale = ResolveDpiScale(0, visualDpiScale);
        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            try
            {
                dpiScale = ResolveDpiScale(NativeMethods.GetDpiForWindow(handle), visualDpiScale);
                IntPtr monitor = NativeMethods.MonitorFromWindow(
                    handle,
                    NativeMethods.MONITOR_DEFAULTTONEAREST);
                NativeMethods.MONITORINFO monitorInfo = new()
                {
                    cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
                };

                if (monitor != IntPtr.Zero &&
                    NativeMethods.GetMonitorInfo(monitor, ref monitorInfo) &&
                    monitorInfo.rcWork.Width > 0 &&
                    monitorInfo.rcWork.Height > 0)
                {
                    return new MonitorWorkArea(
                        monitorInfo.rcWork.Left,
                        monitorInfo.rcWork.Top,
                        monitorInfo.rcWork.Width,
                        monitorInfo.rcWork.Height,
                        dpiScale);
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        Rect fallback = SystemParameters.WorkArea;
        return ConvertDipWorkAreaToPixels(
            fallback.Left,
            fallback.Top,
            fallback.Width,
            fallback.Height,
            dpiScale);
    }

    public static bool CenterWindow(Window window, MonitorWorkArea? area = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.UpdateLayout();
        double width = SelectWindowDimension(window.ActualWidth, window.Width);
        double height = SelectWindowDimension(window.ActualHeight, window.Height);
        WindowPixelBounds bounds = CalculateCenteredBounds(
            area ?? GetForWindow(window),
            width,
            height);
        IntPtr handle = new WindowInteropHelper(window).Handle;

        if (TrySetWindowPosition(handle, bounds))
            return true;

        ApplyCenteredWpfFallback(window, width, height);
        return false;
    }

    public static bool ClampWindowToWorkArea(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        IntPtr handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            try
            {
                if (NativeMethods.GetWindowRect(handle, out NativeMethods.RECT rect))
                {
                    WindowPixelBounds bounds = ClampBounds(
                        GetForWindow(window),
                        new WindowPixelBounds(rect.Left, rect.Top, rect.Width, rect.Height));

                    if (TrySetWindowPosition(handle, bounds))
                        return true;
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        ApplyClampedWpfFallback(window);
        return false;
    }

    private static bool TrySetWindowPosition(IntPtr handle, WindowPixelBounds bounds)
    {
        if (handle == IntPtr.Zero)
            return false;

        try
        {
            return NativeMethods.SetWindowPos(
                handle,
                IntPtr.Zero,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    private static void ApplyCenteredWpfFallback(Window window, double width, double height)
    {
        Rect workArea = GetSafeDipWorkArea(width, height);
        double safeWidth = Math.Min(SelectWindowDimension(width, width), workArea.Width);
        double safeHeight = Math.Min(SelectWindowDimension(height, height), workArea.Height);

        ApplyWpfBounds(
            window,
            workArea.Left + ((workArea.Width - safeWidth) / 2),
            workArea.Top + ((workArea.Height - safeHeight) / 2),
            safeWidth,
            safeHeight);
    }

    private static void ApplyClampedWpfFallback(Window window)
    {
        window.UpdateLayout();
        double width = SelectWindowDimension(window.ActualWidth, window.Width);
        double height = SelectWindowDimension(window.ActualHeight, window.Height);
        Rect workArea = GetSafeDipWorkArea(width, height);
        double safeWidth = Math.Min(width, workArea.Width);
        double safeHeight = Math.Min(height, workArea.Height);
        double left = double.IsFinite(window.Left) ? window.Left : workArea.Left;
        double top = double.IsFinite(window.Top) ? window.Top : workArea.Top;

        ApplyWpfBounds(
            window,
            Math.Clamp(left, workArea.Left, workArea.Right - safeWidth),
            Math.Clamp(top, workArea.Top, workArea.Bottom - safeHeight),
            safeWidth,
            safeHeight);
    }

    private static void ApplyWpfBounds(
        Window window,
        double left,
        double top,
        double width,
        double height)
    {
        window.Width = width;
        window.Height = height;
        window.Left = left;
        window.Top = top;
    }

    private static Rect GetSafeDipWorkArea(double fallbackWidth, double fallbackHeight)
    {
        Rect workArea = SystemParameters.WorkArea;
        double left = double.IsFinite(workArea.Left) ? workArea.Left : 0;
        double top = double.IsFinite(workArea.Top) ? workArea.Top : 0;
        double width = double.IsFinite(workArea.Width) && workArea.Width > 0
            ? workArea.Width
            : SelectWindowDimension(fallbackWidth, fallbackWidth);
        double height = double.IsFinite(workArea.Height) && workArea.Height > 0
            ? workArea.Height
            : SelectWindowDimension(fallbackHeight, fallbackHeight);

        return new Rect(left, top, width, height);
    }

    private static int ToPhysicalSize(double dipSize, double dpiScale, int maximum)
    {
        double safeDipSize = double.IsFinite(dipSize) && dipSize > 0 ? dipSize : 1.0;
        double physicalSize = Math.Ceiling(safeDipSize * dpiScale);
        return (int)Math.Min(maximum, Math.Max(1.0, physicalSize));
    }

    private static double SelectWindowDimension(double actual, double requested)
    {
        if (double.IsFinite(actual) && actual > 0)
            return actual;

        return double.IsFinite(requested) && requested > 0 ? requested : 1.0;
    }

    private static int ToScaledCoordinate(double dipValue, double dpiScale) =>
        ToRoundedInt(dipValue * dpiScale, fallback: 0);

    private static int ToScaledSize(double dipValue, double dpiScale) =>
        Math.Max(1, ToRoundedInt(dipValue * dpiScale, fallback: 1));

    private static int ToRoundedInt(double value, int fallback)
    {
        if (!double.IsFinite(value))
            return fallback;

        double rounded = Math.Round(value);
        if (rounded >= int.MaxValue)
            return int.MaxValue;
        if (rounded <= int.MinValue)
            return int.MinValue;

        return (int)rounded;
    }
}
