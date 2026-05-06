using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class EditorZoomPolicyTests
{
    [Fact]
    public void KeepsOneHundredPercentWhenImageFitsViewport()
    {
        double zoom = EditorZoomPolicy.CalculateInitialZoom(
            imagePixelWidth: 600,
            imagePixelHeight: 400,
            viewportWidth: 900,
            viewportHeight: 700,
            padding: 20,
            dpiScale: 1.25);

        Assert.Equal(0.8, zoom, precision: 6);
    }

    [Fact]
    public void ShrinksToFitWhenImageSlightlyExceedsViewport()
    {
        double zoom = EditorZoomPolicy.CalculateInitialZoom(
            imagePixelWidth: 1000,
            imagePixelHeight: 800,
            viewportWidth: 940,
            viewportHeight: 760,
            padding: 20,
            dpiScale: 1.0);

        Assert.Equal(0.9, zoom, precision: 6);
    }

    [Fact]
    public void DoesNotAutoShrinkBelowEightyPercent()
    {
        double zoom = EditorZoomPolicy.CalculateInitialZoom(
            imagePixelWidth: 3000,
            imagePixelHeight: 2000,
            viewportWidth: 1240,
            viewportHeight: 840,
            padding: 20,
            dpiScale: 1.0);

        Assert.Equal(0.8, zoom, precision: 6);
    }
}
