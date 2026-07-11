using System;

namespace ShinCapture.Editor;

public enum CaptureHistoryDirection
{
    Up = -1,
    Down = 1
}

public static class CaptureHistoryNavigationPolicy
{
    public static int GetTargetIndex(
        int currentIndex,
        int itemCount,
        CaptureHistoryDirection direction)
    {
        if (itemCount <= 0)
            return -1;

        if (currentIndex < 0 || currentIndex >= itemCount)
        {
            return direction == CaptureHistoryDirection.Down
                ? 0
                : itemCount - 1;
        }

        return Math.Clamp(currentIndex + (int)direction, 0, itemCount - 1);
    }
}
