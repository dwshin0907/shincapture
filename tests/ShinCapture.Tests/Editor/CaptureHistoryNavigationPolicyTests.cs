using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class CaptureHistoryNavigationPolicyTests
{
    [Theory]
    [InlineData(2, 5, CaptureHistoryDirection.Up, 1)]
    [InlineData(2, 5, CaptureHistoryDirection.Down, 3)]
    public void MovesOneItemInRequestedDirection(
        int currentIndex,
        int itemCount,
        CaptureHistoryDirection direction,
        int expected)
    {
        int target = CaptureHistoryNavigationPolicy.GetTargetIndex(
            currentIndex,
            itemCount,
            direction);

        Assert.Equal(expected, target);
    }

    [Theory]
    [InlineData(0, 5, CaptureHistoryDirection.Up, 0)]
    [InlineData(4, 5, CaptureHistoryDirection.Down, 4)]
    public void ClampsAtHistoryEdges(
        int currentIndex,
        int itemCount,
        CaptureHistoryDirection direction,
        int expected)
    {
        int target = CaptureHistoryNavigationPolicy.GetTargetIndex(
            currentIndex,
            itemCount,
            direction);

        Assert.Equal(expected, target);
    }

    [Fact]
    public void ReturnsMinusOneWhenHistoryIsEmpty()
    {
        int target = CaptureHistoryNavigationPolicy.GetTargetIndex(
            currentIndex: 0,
            itemCount: 0,
            CaptureHistoryDirection.Down);

        Assert.Equal(-1, target);
    }

    [Theory]
    [InlineData(-1, 5, CaptureHistoryDirection.Down, 0)]
    [InlineData(5, 5, CaptureHistoryDirection.Down, 0)]
    [InlineData(-1, 5, CaptureHistoryDirection.Up, 4)]
    [InlineData(5, 5, CaptureHistoryDirection.Up, 4)]
    public void InvalidCurrentIndexSelectsDirectionalEdge(
        int currentIndex,
        int itemCount,
        CaptureHistoryDirection direction,
        int expected)
    {
        int target = CaptureHistoryNavigationPolicy.GetTargetIndex(
            currentIndex,
            itemCount,
            direction);

        Assert.Equal(expected, target);
    }
}
