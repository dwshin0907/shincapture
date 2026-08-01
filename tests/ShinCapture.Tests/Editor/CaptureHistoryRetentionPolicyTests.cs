using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class CaptureHistoryRetentionPolicyTests
{
    [Fact]
    public void EstimateBytes_UsesPixelDimensionsAndFormatDepth()
    {
        long bytes = CaptureHistoryRetentionPolicy.EstimateBytes(3840, 2160, 32);

        Assert.Equal(3840L * 2160 * 4, bytes);
    }

    [Fact]
    public void GetRetainedCount_RespectsConfiguredCount()
    {
        int retained = CaptureHistoryRetentionPolicy.GetRetainedCount(
            [10, 10, 10, 10],
            maxCount: 3,
            maxBytes: 100);

        Assert.Equal(3, retained);
    }

    [Fact]
    public void GetRetainedCount_StopsBeforeExceedingByteBudget()
    {
        int retained = CaptureHistoryRetentionPolicy.GetRetainedCount(
            [40, 40, 40],
            maxCount: 10,
            maxBytes: 100);

        Assert.Equal(2, retained);
    }

    [Fact]
    public void GetRetainedCount_KeepsNewestImageEvenWhenItExceedsBudget()
    {
        int retained = CaptureHistoryRetentionPolicy.GetRetainedCount(
            [200, 1],
            maxCount: 10,
            maxBytes: 100);

        Assert.Equal(1, retained);
    }

    [Fact]
    public void GetRetainedCount_DoesNotOverflowWhenAnEstimateIsHuge()
    {
        int retained = CaptureHistoryRetentionPolicy.GetRetainedCount(
            [long.MaxValue, long.MaxValue],
            maxCount: 10,
            maxBytes: long.MaxValue - 1);

        Assert.Equal(1, retained);
    }
}
