using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class MonitorWorkAreaServiceTests
{
    [Fact]
    public void ResolvesNativeDpiBeforeVisualFallback()
    {
        Assert.Equal(1.5, MonitorWorkAreaService.ResolveDpiScale(144, 1.75));
        Assert.Equal(1.75, MonitorWorkAreaService.ResolveDpiScale(0, 1.75));
        Assert.Equal(1.0, MonitorWorkAreaService.ResolveDpiScale(0, double.NaN));
        Assert.Equal(1.0, MonitorWorkAreaService.ResolveDpiScale(0, 0));
    }

    [Fact]
    public void ConvertsDipWorkAreaToPhysicalPixelsUsingVisualScale()
    {
        MonitorWorkArea area = MonitorWorkAreaService.ConvertDipWorkAreaToPixels(
            dipLeft: -1280,
            dipTop: 20,
            dipWidth: 1280,
            dipHeight: 700,
            dpiScale: 1.5);

        Assert.Equal(new MonitorWorkArea(-1920, 30, 1920, 1050, 1.5), area);
    }

    [Fact]
    public void ConvertsPhysicalWorkAreaDimensionsToDips()
    {
        MonitorWorkArea area = new(-1920, 0, 1920, 1040, 1.25);

        Assert.Equal(1536, area.DipWidth);
        Assert.Equal(832, area.DipHeight);
    }

    [Fact]
    public void UsesSafeScaleWhenDpiScaleIsInvalid()
    {
        double[] invalidScales =
        [
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            0,
            -1
        ];

        foreach (double invalidScale in invalidScales)
        {
            MonitorWorkArea area = new(0, 0, 1920, 1040, invalidScale);

            Assert.Equal(1.0, area.DpiScale);
            Assert.Equal(1920, area.DipWidth);
            Assert.Equal(1040, area.DipHeight);
        }
    }

    [Fact]
    public void UsesSafeScaleForDefaultValue()
    {
        MonitorWorkArea area = default;

        Assert.Equal(1.0, area.DpiScale);
        Assert.Equal(0, area.DipWidth);
        Assert.Equal(0, area.DipHeight);
    }

    [Fact]
    public void CalculatesCenteredBoundsInPhysicalPixelsOnNegativeOriginMonitor()
    {
        MonitorWorkArea area = new(-1920, 0, 1920, 1040, 1.25);

        WindowPixelBounds bounds = MonitorWorkAreaService.CalculateCenteredBounds(
            area,
            windowDipWidth: 1100,
            windowDipHeight: 750);

        Assert.Equal(-1648, bounds.Left);
        Assert.Equal(51, bounds.Top);
        Assert.Equal(1375, bounds.Width);
        Assert.Equal(938, bounds.Height);
    }

    [Fact]
    public void LimitsCenteredBoundsToWorkAreaWhenDesiredWindowIsLarger()
    {
        MonitorWorkArea area = new(-1920, 0, 1920, 1040, 1.25);

        WindowPixelBounds bounds = MonitorWorkAreaService.CalculateCenteredBounds(
            area,
            windowDipWidth: 3000,
            windowDipHeight: 2000);

        Assert.Equal(new WindowPixelBounds(-1920, 0, 1920, 1040), bounds);
    }

    [Fact]
    public void ClampsBoundsInsideNegativeOriginWorkArea()
    {
        MonitorWorkArea area = new(-1920, 0, 1920, 1040, 1.25);
        WindowPixelBounds outsideRightAndBottom = new(-100, 900, 500, 300);

        WindowPixelBounds bounds = MonitorWorkAreaService.ClampBounds(area, outsideRightAndBottom);

        Assert.Equal(new WindowPixelBounds(-500, 740, 500, 300), bounds);
    }

    [Fact]
    public void ShrinksOversizedBoundsToWorkArea()
    {
        MonitorWorkArea area = new(-1920, 0, 1920, 1040, 1.25);
        WindowPixelBounds oversized = new(-3000, -100, 2500, 1500);

        WindowPixelBounds bounds = MonitorWorkAreaService.ClampBounds(area, oversized);

        Assert.Equal(new WindowPixelBounds(-1920, 0, 1920, 1040), bounds);
    }
}
