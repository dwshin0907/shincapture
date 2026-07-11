using ShinCapture.Editor;
using ShinCapture.Models;

namespace ShinCapture.Tests.Editor;

public class EditorWindowSizingPolicyTests
{
    private static readonly double[] InvalidDimensionValues =
    [
        double.NaN,
        double.PositiveInfinity,
        double.NegativeInfinity,
        0,
        -1
    ];

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
        foreach (double width in InvalidDimensionValues)
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
    public void UsesDefaultHeightWhenRememberedHeightIsInvalid()
    {
        foreach (double height in InvalidDimensionValues)
        {
            EditorWindowSize size = EditorWindowSizingPolicy.NormalizeRememberedSize(
                width: 1200,
                height,
                workAreaWidth: 1920,
                workAreaHeight: 1040);

            Assert.Equal(1200, size.Width);
            Assert.Equal(EditorWindowSizingPolicy.DefaultHeight, size.Height);
            Assert.False(EditorWindowSizingPolicy.IsValidPersistedSize(1200, height));
        }
    }

    [Fact]
    public void UsesFiniteDefaultBoundsWhenWorkAreaDimensionsAreInvalid()
    {
        foreach (double workAreaDimension in InvalidDimensionValues)
        {
            EditorWindowSize widthFallback = EditorWindowSizingPolicy.NormalizeRememberedSize(
                width: 4000,
                height: 800,
                workAreaWidth: workAreaDimension,
                workAreaHeight: 1040);
            EditorWindowSize heightFallback = EditorWindowSizingPolicy.NormalizeRememberedSize(
                width: 1200,
                height: 3000,
                workAreaWidth: 1920,
                workAreaHeight: workAreaDimension);

            Assert.True(double.IsFinite(widthFallback.Width));
            Assert.Equal(1100, widthFallback.Width);
            Assert.Equal(800, widthFallback.Height);
            Assert.True(double.IsFinite(heightFallback.Height));
            Assert.Equal(1200, heightFallback.Width);
            Assert.Equal(750, heightFallback.Height);
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

    [Fact]
    public void ClampsAllRememberedSizesToWorkAreaBelowMinimumSize()
    {
        EditorWindowSize belowMinimum = EditorWindowSizingPolicy.NormalizeRememberedSize(
            width: 200,
            height: 100,
            workAreaWidth: 640,
            workAreaHeight: 480);
        EditorWindowSize aboveWorkArea = EditorWindowSizingPolicy.NormalizeRememberedSize(
            width: 4000,
            height: 3000,
            workAreaWidth: 640,
            workAreaHeight: 480);

        Assert.Equal(640, belowMinimum.Width);
        Assert.Equal(480, belowMinimum.Height);
        Assert.Equal(640, aboveWorkArea.Width);
        Assert.Equal(480, aboveWorkArea.Height);
    }

    [Fact]
    public void KeepsEditorSettingsDefaultContract()
    {
        EditorSettings settings = new();

        Assert.Equal(EditorWindowSizeMode.RememberLast, settings.WindowSizeMode);
        Assert.Equal(1100, settings.WindowWidth);
        Assert.Equal(750, settings.WindowHeight);
    }

    [Fact]
    public void KeepsSizingPolicyConstantContract()
    {
        Assert.Equal(1100, EditorWindowSizingPolicy.DefaultWidth);
        Assert.Equal(750, EditorWindowSizingPolicy.DefaultHeight);
        Assert.Equal(760, EditorWindowSizingPolicy.MinimumWidth);
        Assert.Equal(520, EditorWindowSizingPolicy.MinimumHeight);
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

    [Theory]
    [InlineData(900, 240, 660)]
    [InlineData(900, 0, 900)]
    [InlineData(900, -20, 900)]
    [InlineData(900, double.NaN, 900)]
    [InlineData(900, double.PositiveInfinity, 900)]
    [InlineData(900, 900, 1)]
    [InlineData(900, 1200, 1)]
    [InlineData(double.NaN, 240, 1)]
    [InlineData(double.PositiveInfinity, 240, 1)]
    [InlineData(0, 240, 1)]
    [InlineData(-20, 240, 1)]
    public void CalculatesSafeHeightBeforeOcr(
        double currentWindowHeight,
        double panelHeight,
        double expected)
    {
        Assert.Equal(
            expected,
            EditorWindowSizingPolicy.CalculateHeightBeforeOcr(
                currentWindowHeight,
                panelHeight));
    }

    [Theory]
    [InlineData(null, EditorWindowSizeMode.RememberLast, true)]
    [InlineData(EditorWindowSizeMode.Maximized, EditorWindowSizeMode.RememberLast, true)]
    [InlineData(EditorWindowSizeMode.FitToCapture, EditorWindowSizeMode.RememberLast, true)]
    [InlineData(EditorWindowSizeMode.RememberLast, EditorWindowSizeMode.RememberLast, false)]
    [InlineData(null, EditorWindowSizeMode.Maximized, false)]
    [InlineData(EditorWindowSizeMode.RememberLast, EditorWindowSizeMode.Maximized, false)]
    [InlineData(null, EditorWindowSizeMode.FitToCapture, false)]
    [InlineData(EditorWindowSizeMode.RememberLast, EditorWindowSizeMode.FitToCapture, false)]
    public void AppliesRememberedSizeOnlyWhenEnteringRememberLast(
        EditorWindowSizeMode? previous,
        EditorWindowSizeMode current,
        bool expected)
    {
        Assert.Equal(
            expected,
            EditorWindowSizingPolicy.ShouldApplyRememberedSize(previous, current));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void DefersRefreshUntilWindowIsLoadedAndVisible(
        bool isLoaded,
        bool isVisible,
        bool expected)
    {
        Assert.Equal(
            expected,
            EditorWindowSizingPolicy.ShouldDeferRefresh(isLoaded, isVisible));
    }
}
