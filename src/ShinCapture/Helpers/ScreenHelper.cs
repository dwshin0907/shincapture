using System;
using System.Drawing;
using System.Windows;

namespace ShinCapture.Helpers;

public static class ScreenHelper
{
    public static Bitmap CaptureFullScreen()
    {
        var virtualLeft = (int)SystemParameters.VirtualScreenLeft;
        var virtualTop = (int)SystemParameters.VirtualScreenTop;
        var virtualWidth = (int)SystemParameters.VirtualScreenWidth;
        var virtualHeight = (int)SystemParameters.VirtualScreenHeight;

        var desktopWnd = NativeMethods.GetDesktopWindow();
        var desktopDc = NativeMethods.GetWindowDC(desktopWnd);
        var memDc = NativeMethods.CreateCompatibleDC(desktopDc);
        var hBitmap = NativeMethods.CreateCompatibleBitmap(desktopDc, virtualWidth, virtualHeight);
        var oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

        NativeMethods.BitBlt(memDc, 0, 0, virtualWidth, virtualHeight,
            desktopDc, virtualLeft, virtualTop, NativeMethods.SRCCOPY);

        NativeMethods.SelectObject(memDc, oldBitmap);
        var bitmap = Image.FromHbitmap(hBitmap);

        NativeMethods.DeleteObject(hBitmap);
        NativeMethods.DeleteDC(memDc);
        NativeMethods.ReleaseDC(desktopWnd, desktopDc);

        return bitmap;
    }

    public static Bitmap CropBitmap(Bitmap source, Rectangle region)
    {
        return source.Clone(region, source.PixelFormat);
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
