using System;
using System.Collections.Generic;

namespace ShinCapture.Editor;

public static class CaptureHistoryRetentionPolicy
{
    public const long DefaultMaxBytes = 256L * 1024 * 1024;

    public static long EstimateBytes(int width, int height, int bitsPerPixel)
    {
        if (width <= 0 || height <= 0) return 0;
        int normalizedBitsPerPixel = Math.Max(8, bitsPerPixel);
        return checked((long)width * height * normalizedBitsPerPixel / 8);
    }

    public static int GetRetainedCount(
        IEnumerable<long> newestFirstImageBytes,
        int maxCount,
        long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(newestFirstImageBytes);
        if (maxCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));

        int retained = 0;
        long total = 0;
        foreach (long rawBytes in newestFirstImageBytes)
        {
            long bytes = Math.Max(0, rawBytes);
            if (retained > 0 &&
                (retained >= maxCount ||
                 total >= maxBytes ||
                 bytes > maxBytes - total))
            {
                break;
            }

            retained++;
            total = checked(total + bytes);
        }

        return retained;
    }
}
