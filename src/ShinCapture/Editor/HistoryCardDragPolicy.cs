using System;

namespace ShinCapture.Editor;

public static class HistoryCardDragPolicy
{
    public static bool ShouldStart(
        double deltaX,
        double deltaY,
        double minimumHorizontal,
        double minimumVertical) =>
        Math.Abs(deltaX) >= minimumHorizontal ||
        Math.Abs(deltaY) >= minimumVertical;
}
