using ShinCapture.Editor;
using ShinCapture.Models;

namespace ShinCapture.Tests.Editor;

public class EditorWindowSizingPolicyTests
{
    [Fact]
    public void KeepsValidRememberedSize()
    {
        EditorWindowSize size = EditorWindowSizingPolicy.NormalizeRememberedSize(
            width: 1200,
            height: 800,
            workAreaWidth: 1920,
            workAreaHeight: 1040);

        Assert.Equal(1200, size.Width);
        Assert.Equal(800, size.Height);
        Assert.True(EditorWindowSizingPolicy.IsValidPersistedSize(1200, 800));
    }

    [Fact]
    public void UsesDefaultWidthWhenRememberedWidthIsInvalid()
    {
        double[] invalidWidths =
        [
            double.NaN,
            double.PositiveInfinity,
            0,
            -1
        ];

        foreach (double width in invalidWidths)
        {
            EditorWindowSize size = EditorWindowSizingPolicy.NormalizeRememberedSize(
                width,
                height: 800,
                workAreaWidth: 1920,
                workAreaHeight: 1040);

            Assert.Equal(EditorWindowSizingPolicy.DefaultWidth, size.Width);
            Assert.Equal(800, size.Height);
            Assert.False(EditorWindowSizingPolicy.IsValidPersistedSize(width, 800));
        }
    }

    [Fact]
    public void ClampsRememberedSizeToMinimumAndWorkArea()
    {
        EditorWindowSize belowMinimum = EditorWindowSizingPolicy.NormalizeRememberedSize(
            width: 200,
            height: 100,
            workAreaWidth: 1920,
            workAreaHeight: 1040);
        EditorWindowSize aboveWorkArea = EditorWindowSizingPolicy.NormalizeRememberedSize(
            width: 4000,
            height: 3000,
            workAreaWidth: 1366,
            workAreaHeight: 728);

        Assert.Equal(EditorWindowSizingPolicy.MinimumWidth, belowMinimum.Width);
        Assert.Equal(EditorWindowSizingPolicy.MinimumHeight, belowMinimum.Height);
        Assert.Equal(1366, aboveWorkArea.Width);
        Assert.Equal(728, aboveWorkArea.Height);
    }

    [Theory]
    [InlineData(EditorWindowSizeMode.RememberLast, true, false, false, false)]
    [InlineData(EditorWindowSizeMode.Maximized, false, true, false, false)]
    [InlineData(EditorWindowSizeMode.FitToCapture, false, false, true, true)]
    public void AppliesWindowSizeModeRules(
        EditorWindowSizeMode mode,
        bool shouldKeepOuterSize,
        bool shouldMaximize,
        bool shouldFitToCapture,
        bool shouldGrowForOcr)
    {
        Assert.Equal(shouldKeepOuterSize, EditorWindowSizingPolicy.ShouldKeepOuterSize(mode));
        Assert.Equal(shouldMaximize, EditorWindowSizingPolicy.ShouldMaximize(mode));
        Assert.Equal(shouldFitToCapture, EditorWindowSizingPolicy.ShouldFitToCapture(mode));
        Assert.Equal(shouldGrowForOcr, EditorWindowSizingPolicy.ShouldGrowForOcr(mode));
    }
}
