using System;

namespace ShinCapture.Services;

public readonly record struct PixelPoint(int X, int Y);

public readonly record struct PixelSize(int Width, int Height);

public static class TrayFlyoutPositioner
{
    public const int Margin = 8;
    public const int CursorGap = 10;
    public const int CursorHorizontalOffset = 20;

    public static WindowPixelBounds CalculateFromDips(
        PixelPoint cursor,
        MonitorWorkArea workArea,
        double widthDip,
        double heightDip)
    {
        return Calculate(
            cursor,
            workArea,
            new PixelSize(
                ToPhysicalSize(widthDip, workArea.DpiScale),
                ToPhysicalSize(heightDip, workArea.DpiScale)));
    }

    public static WindowPixelBounds Calculate(
        PixelPoint cursor,
        MonitorWorkArea workArea,
        PixelSize flyoutSize)
    {
        int workWidth = Math.Max(1, workArea.PixelWidth);
        int workHeight = Math.Max(1, workArea.PixelHeight);
        int horizontalMargin = workWidth > Margin * 2 ? Margin : 0;
        int verticalMargin = workHeight > Margin * 2 ? Margin : 0;
        int availableWidth = workWidth - (horizontalMargin * 2);
        int availableHeight = workHeight - (verticalMargin * 2);
        int width = Math.Min(Math.Max(1, flyoutSize.Width), availableWidth);
        int height = Math.Min(Math.Max(1, flyoutSize.Height), availableHeight);

        long usableLeft = (long)workArea.PixelLeft + horizontalMargin;
        long usableTop = (long)workArea.PixelTop + verticalMargin;
        long maxLeft = usableLeft + availableWidth - width;
        long maxTop = usableTop + availableHeight - height;
        long left = Math.Clamp(
            (long)cursor.X - width + CursorHorizontalOffset,
            usableLeft,
            maxLeft);
        long top = (long)cursor.Y + CursorGap;

        if (top + height > usableTop + availableHeight)
            top = (long)cursor.Y - CursorGap - height;

        top = Math.Clamp(top, usableTop, maxTop);
        left = Math.Clamp(left, int.MinValue, (long)int.MaxValue - width);
        top = Math.Clamp(top, int.MinValue, (long)int.MaxValue - height);
        return new WindowPixelBounds((int)left, (int)top, width, height);
    }

    private static int ToPhysicalSize(double dipSize, double dpiScale)
    {
        double safeDipSize = double.IsFinite(dipSize) && dipSize > 0 ? dipSize : 1.0;
        double physicalSize = Math.Ceiling(safeDipSize * dpiScale);
        if (!double.IsFinite(physicalSize) || physicalSize >= int.MaxValue)
            return int.MaxValue;

        return Math.Max(1, (int)physicalSize);
    }
}
