using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class HistoryCardDragPolicyTests
{
    [Theory]
    [InlineData(3, 3, 4, 4, false)]
    [InlineData(4, 0, 4, 4, true)]
    [InlineData(-4, 0, 4, 4, true)]
    [InlineData(0, -5, 4, 4, true)]
    [InlineData(0, 4, 4, 4, true)]
    public void StartsOnlyAfterSystemDragThreshold(
        double deltaX,
        double deltaY,
        double horizontal,
        double vertical,
        bool expected)
    {
        Assert.Equal(
            expected,
            HistoryCardDragPolicy.ShouldStart(
                deltaX, deltaY, horizontal, vertical));
    }
}
