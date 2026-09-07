using System.Windows;
using ShinCapture.Capture;

namespace ShinCapture.Tests.Capture;

public sealed class SmartCutGeometryTests
{
    [Fact]
    public void ClosesReasonablyOpenFreehandPath()
    {
        var input = new[] { new Point(10, 10), new Point(110, 10), new Point(110, 80), new Point(12, 12) };
        Assert.True(SmartCutGeometry.TryCloseAndValidate(input, 200, 120, out List<Point> closed));
        Assert.Equal(closed[0], closed[^1]);
    }

    [Fact]
    public void RejectsPathWithExcessiveClosingGap()
    {
        var input = new[] { new Point(10, 10), new Point(110, 10), new Point(110, 80), new Point(80, 80) };
        Assert.False(SmartCutGeometry.TryCloseAndValidate(input, 200, 120, out _));
    }

    [Fact]
    public void AddsPaddingAndClampsRegionToBitmap()
    {
        var points = new[] { new Point(0, 0), new Point(20, 0), new Point(20, 20), new Point(0, 20) };
        var region = SmartCutGeometry.ComputeClampedRegion(points, 2, 2, 40, 40);
        Assert.Equal(new System.Drawing.Rectangle(0, 0, 40, 40), region);
        var local = SmartCutGeometry.ToLocalPolygon(points, 2, 2, region);
        Assert.Equal(0, local[0].X);
        Assert.Equal(0, local[0].Y);
    }

    [Fact]
    public void SmallValidShapeIsIndependentOfScreenSize()
    {
        var input = new[] { new Point(100, 100), new Point(130, 100), new Point(130, 125), new Point(101, 102) };
        Assert.True(SmartCutGeometry.TryCloseAndValidate(input, 7680, 4320, out _));
    }

    [Fact]
    public void InvalidLassoCanBeRetriedWithValidPath()
    {
        var invalid = new[] { new Point(10, 10), new Point(40, 10), new Point(40, 40) };
        var valid = new[] { new Point(10, 10), new Point(90, 10), new Point(90, 70), new Point(12, 12) };
        Assert.False(SmartCutGeometry.TryCloseAndValidate(invalid, 200, 120, out _));
        Assert.True(SmartCutGeometry.TryCloseAndValidate(valid, 200, 120, out _));
    }
}
