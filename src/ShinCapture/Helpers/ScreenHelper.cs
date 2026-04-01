using System;
using System.Drawing;
using System.Linq;

namespace ShinCapture.Helpers;

public static class ScreenHelper
{
    /// <summary>전체 가상 화면의 물리 픽셀 영역</summary>
    public static Rectangle GetPhysicalScreenBounds()
    {
        // WinForms Screen.Bounds는 System DPI aware 프로세스에서 물리 픽셀 반환
        var allScreens = System.Windows.Forms.Screen.AllScreens;
        int left = allScreens.Min(s => s.Bounds.Left);
        int top = allScreens.Min(s => s.Bounds.Top);
        int right = allScreens.Max(s => s.Bounds.Right);
        int bottom = allScreens.Max(s => s.Bounds.Bottom);
        return new Rectangle(left, top, right - left, bottom - top);
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
