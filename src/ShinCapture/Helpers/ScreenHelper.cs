using System;
using System.Drawing;

namespace ShinCapture.Helpers;

public static class ScreenHelper
{
    /// <summary>전체 가상 화면의 물리 픽셀 영역 (캐시 없이 OS에서 직접 조회)</summary>
    public static Rectangle GetPhysicalScreenBounds()
    {
        int left = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int top = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);
        return new Rectangle(left, top, width, height);
    }

    /// <summary>시스템 DPI 스케일 (세션 동안 고정값, 캐시 안전)</summary>
    public static double GetSystemDpiScale()
    {
        return NativeMethods.GetDpiForSystem() / 96.0;
    }

    /// <summary>가상 화면을 WPF DIP 좌표로 반환 (SystemParameters 캐시 우회)</summary>
    public static (double Left, double Top, double Width, double Height) GetVirtualScreenDip()
    {
        var bounds = GetPhysicalScreenBounds();
        double scale = GetSystemDpiScale();
        return (bounds.X / scale, bounds.Y / scale, bounds.Width / scale, bounds.Height / scale);
    }

    public static Bitmap CaptureFullScreen()
    {
        var bounds = GetPhysicalScreenBounds();

        var desktopWnd = NativeMethods.GetDesktopWindow();
        var desktopDc = NativeMethods.GetWindowDC(desktopWnd);
        var memDc = NativeMethods.CreateCompatibleDC(desktopDc);
        var hBitmap = NativeMethods.CreateCompatibleBitmap(desktopDc, bounds.Width, bounds.Height);
        var oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

        NativeMethods.BitBlt(memDc, 0, 0, bounds.Width, bounds.Height,
            desktopDc, bounds.Left, bounds.Top, NativeMethods.SRCCOPY);

        NativeMethods.SelectObject(memDc, oldBitmap);
        var bitmap = Image.FromHbitmap(hBitmap);

        NativeMethods.DeleteObject(hBitmap);
        NativeMethods.DeleteDC(memDc);
        NativeMethods.ReleaseDC(desktopWnd, desktopDc);

        return bitmap;
    }

    public static Bitmap CropBitmap(Bitmap source, Rectangle region)
    {
        int x = Math.Max(0, region.X);
        int y = Math.Max(0, region.Y);
        int w = Math.Min(region.Width, source.Width - x);
        int h = Math.Min(region.Height, source.Height - y);
        if (w <= 0 || h <= 0) return new Bitmap(1, 1);
        return source.Clone(new Rectangle(x, y, w, h), source.PixelFormat);
    }

    public static Color GetPixelColor(Bitmap bitmap, int x, int y)
    {
        if (x >= 0 && x < bitmap.Width && y >= 0 && y < bitmap.Height)
            return bitmap.GetPixel(x, y);
        return Color.Transparent;
    }

    public static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
