using System;

namespace ShinCapture.Editor;

public static class EditorZoomPolicy
{
    public const double MinimumAutoFitRatio = 0.8;

    public static double CalculateInitialZoom(
        int imagePixelWidth,
        int imagePixelHeight,
        double viewportWidth,
        double viewportHeight,
        double padding,
        double dpiScale)
    {
        if (imagePixelWidth <= 0 || imagePixelHeight <= 0 || dpiScale <= 0)
            return 1.0;

        double zoom100 = 1.0 / dpiScale;

        if (viewportWidth <= 0 || viewportHeight <= 0)
            return zoom100;

        double availableWidth = viewportWidth - padding * 2;
        double availableHeight = viewportHeight - padding * 2;
        if (availableWidth <= 0 || availableHeight <= 0)
            return zoom100;

        double imageWidthAt100 = imagePixelWidth * zoom100;
        double imageHeightAt100 = imagePixelHeight * zoom100;
        if (imageWidthAt100 <= availableWidth && imageHeightAt100 <= availableHeight)
            return zoom100;

        double fitZoom = Math.Min(
            availableWidth / imagePixelWidth,
            availableHeight / imagePixelHeight);

        double minimumAutoFitZoom = MinimumAutoFitRatio * zoom100;
        return Math.Max(fitZoom, minimumAutoFitZoom);
    }
}
