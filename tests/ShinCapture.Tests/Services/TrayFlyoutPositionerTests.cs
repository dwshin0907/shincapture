using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class TrayFlyoutPositionerTests
{
    [Fact]
    public void OpensAboveCursorNearBottomRightAndStaysInsideMargin()
    {
        MonitorWorkArea workArea = new(0, 0, 1920, 1040, 1.0);
        PixelPoint cursor = new(1880, 1030);

        WindowPixelBounds bounds = TrayFlyoutPositioner.Calculate(
            cursor,
            workArea,
            new PixelSize(380, 560));

        Assert.Equal(380, bounds.Width);
        Assert.Equal(560, bounds.Height);
        Assert.True(bounds.Left >= TrayFlyoutPositioner.Margin);
        Assert.True(bounds.Top >= TrayFlyoutPositioner.Margin);
        Assert.True(bounds.Right <= 1920 - TrayFlyoutPositioner.Margin);
        Assert.True(bounds.Bottom <= 1040 - TrayFlyoutPositioner.Margin);
        Assert.True(bounds.Top < cursor.Y);
    }

    [Fact]
    public void OpensBelowCursorNearTopAndStaysInsideMargin()
    {
        MonitorWorkArea workArea = new(0, 0, 1920, 1040, 1.0);
        PixelPoint cursor = new(1800, 10);

        WindowPixelBounds bounds = TrayFlyoutPositioner.Calculate(
            cursor,
            workArea,
            new PixelSize(380, 560));

        Assert.True(bounds.Top > cursor.Y);
        Assert.True(bounds.Left >= TrayFlyoutPositioner.Margin);
        Assert.True(bounds.Top >= TrayFlyoutPositioner.Margin);
        Assert.True(bounds.Right <= 1920 - TrayFlyoutPositioner.Margin);
        Assert.True(bounds.Bottom <= 1040 - TrayFlyoutPositioner.Margin);
    }

    [Fact]
    public void StaysInsideNegativeOriginMonitorMargins()
    {
        MonitorWorkArea workArea = new(-2560, -200, 2560, 1440, 1.5);

        WindowPixelBounds bounds = TrayFlyoutPositioner.Calculate(
            new PixelPoint(-2500, 1100),
            workArea,
            new PixelSize(380, 560));

        Assert.True(bounds.Left >= -2560 + TrayFlyoutPositioner.Margin);
        Assert.True(bounds.Top >= -200 + TrayFlyoutPositioner.Margin);
        Assert.True(bounds.Right <= -2560 + 2560 - TrayFlyoutPositioner.Margin);
        Assert.True(bounds.Bottom <= -200 + 1440 - TrayFlyoutPositioner.Margin);
    }

    [Fact]
    public void CapsOversizedFlyoutToWorkAreaInsideMargins()
    {
        MonitorWorkArea workArea = new(100, 200, 120, 90, 1.0);

        WindowPixelBounds bounds = TrayFlyoutPositioner.Calculate(
            new PixelPoint(150, 250),
            workArea,
            new PixelSize(500, 400));

        Assert.Equal(new WindowPixelBounds(108, 208, 104, 74), bounds);
    }

    [Fact]
    public void SanitizesInvalidFlyoutAndSmallWorkAreasWithoutThrowing()
    {
        MonitorWorkArea[] workAreas =
        [
            new MonitorWorkArea(10, 20, 0, 0, 1.0),
            new MonitorWorkArea(-20, -30, -10, -5, 1.0),
            new MonitorWorkArea(5, 6, 8, 12, 1.0)
        ];

        foreach (MonitorWorkArea workArea in workAreas)
        {
            WindowPixelBounds bounds = TrayFlyoutPositioner.Calculate(
                new PixelPoint(int.MinValue, int.MaxValue),
                workArea,
                new PixelSize(0, -20));
            int workWidth = Math.Max(1, workArea.PixelWidth);
            int workHeight = Math.Max(1, workArea.PixelHeight);

            Assert.True(bounds.Width >= 1);
            Assert.True(bounds.Height >= 1);
            Assert.True(bounds.Left >= workArea.PixelLeft);
            Assert.True(bounds.Top >= workArea.PixelTop);
            Assert.True(bounds.Right <= workArea.PixelLeft + workWidth);
            Assert.True(bounds.Bottom <= workArea.PixelTop + workHeight);
        }
    }

    [Fact]
    public void SaturatesCoordinatesForExtremeWorkAreaOrigins()
    {
        (MonitorWorkArea WorkArea, PixelPoint Cursor, bool ExpectNonNegative)[] cases =
        [
            (
                new MonitorWorkArea(int.MaxValue - 2, int.MaxValue - 3, 100, 120, 1.0),
                new PixelPoint(int.MaxValue, int.MaxValue),
                true),
            (
                new MonitorWorkArea(int.MinValue, int.MinValue, 100, 120, 1.0),
                new PixelPoint(int.MinValue, int.MinValue),
                false)
        ];

        foreach ((MonitorWorkArea workArea, PixelPoint cursor, bool expectNonNegative) in cases)
        {
            WindowPixelBounds bounds = TrayFlyoutPositioner.Calculate(
                cursor,
                workArea,
                new PixelSize(20, 30));

            Assert.InRange((long)bounds.Left, int.MinValue, int.MaxValue - (long)bounds.Width);
            Assert.InRange((long)bounds.Top, int.MinValue, int.MaxValue - (long)bounds.Height);
            Assert.True((long)bounds.Left + bounds.Width <= int.MaxValue);
            Assert.True((long)bounds.Top + bounds.Height <= int.MaxValue);
            Assert.True((long)bounds.Left >= int.MinValue);
            Assert.True((long)bounds.Top >= int.MinValue);
            if (expectNonNegative)
            {
                Assert.True(bounds.Left >= 0);
                Assert.True(bounds.Top >= 0);
            }
        }
    }

    [Fact]
    public void AnchorsFlyoutUsingCursorHorizontalOffsetWhenClampIsNotNeeded()
    {
        PixelPoint cursor = new(1000, 100);
        PixelSize flyout = new(380, 200);

        WindowPixelBounds bounds = TrayFlyoutPositioner.Calculate(
            cursor,
            new MonitorWorkArea(0, 0, 1920, 1040, 1.0),
            flyout);

        Assert.Equal(
            cursor.X - flyout.Width + TrayFlyoutPositioner.CursorHorizontalOffset,
            bounds.Left);
    }
}
