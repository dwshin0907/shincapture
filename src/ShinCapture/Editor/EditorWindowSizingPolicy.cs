using System;
using ShinCapture.Models;

namespace ShinCapture.Editor;

public readonly record struct EditorWindowSize(double Width, double Height);

public static class EditorWindowSizingPolicy
{
    public const double DefaultWidth = 1100;
    public const double DefaultHeight = 750;
    public const double MinimumWidth = 760;
    public const double MinimumHeight = 520;

    public static EditorWindowSize NormalizeRememberedSize(
        double width,
        double height,
        double workAreaWidth,
        double workAreaHeight)
    {
        double safeWorkAreaWidth = IsValidDimension(workAreaWidth) ? workAreaWidth : DefaultWidth;
        double safeWorkAreaHeight = IsValidDimension(workAreaHeight) ? workAreaHeight : DefaultHeight;
        double safeWidth = IsValidDimension(width) ? width : DefaultWidth;
        double safeHeight = IsValidDimension(height) ? height : DefaultHeight;
        double minimumWidth = Math.Min(MinimumWidth, safeWorkAreaWidth);
        double minimumHeight = Math.Min(MinimumHeight, safeWorkAreaHeight);

        return new EditorWindowSize(
            Math.Clamp(safeWidth, minimumWidth, safeWorkAreaWidth),
            Math.Clamp(safeHeight, minimumHeight, safeWorkAreaHeight));
    }

    public static bool IsValidPersistedSize(double width, double height) =>
        IsValidDimension(width) && IsValidDimension(height);

    public static bool ShouldKeepOuterSize(EditorWindowSizeMode mode) =>
        mode == EditorWindowSizeMode.RememberLast;

    public static bool ShouldMaximize(EditorWindowSizeMode mode) =>
        mode == EditorWindowSizeMode.Maximized;

    public static bool ShouldFitToCapture(EditorWindowSizeMode mode) =>
        mode == EditorWindowSizeMode.FitToCapture;

    public static bool ShouldGrowForOcr(EditorWindowSizeMode mode) =>
        mode == EditorWindowSizeMode.FitToCapture;

    public static double CalculateHeightBeforeOcr(
        double currentWindowHeight,
        double panelHeight)
    {
        if (!IsValidDimension(currentWindowHeight))
            return 1;
        if (!IsValidDimension(panelHeight))
            return currentWindowHeight;

        return Math.Max(1, currentWindowHeight - panelHeight);
    }

    public static bool ShouldApplyRememberedSize(
        EditorWindowSizeMode? previous,
        EditorWindowSizeMode current) =>
        current == EditorWindowSizeMode.RememberLast &&
        previous != EditorWindowSizeMode.RememberLast;

    public static bool ShouldDeferRefresh(bool isLoaded, bool isVisible) =>
        !isLoaded || !isVisible;

    private static bool IsValidDimension(double value) =>
        double.IsFinite(value) && value > 0;
}
