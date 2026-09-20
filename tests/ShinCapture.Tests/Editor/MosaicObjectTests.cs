using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Editor.Objects;
using ShinCapture.Helpers;

namespace ShinCapture.Tests.Editor;

public class MosaicObjectTests
{
    [Theory]
    [InlineData("Bgr24")]
    [InlineData("Indexed8")]
    [InlineData("Pbgra32")]
    public void PreservesSampledColorAndAlphaForImportedPixelFormats(string format)
    {
        RunInSta(() =>
        {
            var pixels = new byte[20 * 10 * 4];
            PixelFormat pixelFormat;
            BitmapPalette? palette = null;
            int stride;
            if (format == "Bgr24")
            {
                pixelFormat = PixelFormats.Bgr24;
                stride = 20 * 3;
                for (int i = 2; i < stride * 10; i += 3) pixels[i] = 255;
            }
            else if (format == "Indexed8")
            {
                pixelFormat = PixelFormats.Indexed8;
                palette = new BitmapPalette([Colors.Red]);
                stride = 20;
            }
            else
            {
                pixelFormat = PixelFormats.Pbgra32;
                stride = 20 * 4;
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i + 2] = 64;
                    pixels[i + 3] = 128;
                }
            }
            var source = BitmapSource.Create(20, 10, 96, 96, pixelFormat, palette, pixels, stride);
            source.Freeze();
            var mosaic = new MosaicObject { SourceImage = source, Region = new Rect(0, 0, 20, 10) };
            using var result = BitmapHelper.ToBitmap(Render(mosaic));
            var sample = result.GetPixel(2, 2);
            Assert.Equal(format == "Pbgra32" ? 128 : 255, sample.A);
            Assert.InRange(sample.R, format == "Pbgra32" ? 126 : 254, format == "Pbgra32" ? 130 : 255);
            Assert.Equal(0, sample.G);
            Assert.Equal(0, sample.B);
        });
    }

    [Fact]
    public void RerenderReflectsRegionSizeAndSourceChanges()
    {
        RunInSta(() =>
        {
            var source = CreateTwoColorSource();
            var mosaic = new MosaicObject { SourceImage = source, Region = new Rect(0, 0, 10, 10) };
            AssertPixel(mosaic, 2, 2, 255, 0);
            mosaic.Move(new Vector(10, 0));
            AssertPixel(mosaic, 12, 2, 0, 255);
            mosaic.Region = new Rect(0, 0, 20, 10);
            mosaic.MosaicSize = MosaicSize.Large;
            AssertPixel(mosaic, 2, 2, 0, 255);
            mosaic.MosaicSize = MosaicSize.Small;
            AssertPixel(mosaic, 2, 2, 255, 0);
            var replacement = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
            replacement.Freeze();
            mosaic.SourceImage = replacement;
            using var result = BitmapHelper.ToBitmap(Render(mosaic));
            var sample = result.GetPixel(2, 2);
            Assert.Equal(sample.R, sample.G);
            Assert.Equal(sample.G, sample.B);
        });
    }

    [Fact]
    public void MutableSourceChangesRemainVisible()
    {
        RunInSta(() =>
        {
            var source = new WriteableBitmap(CreateTwoColorSource());
            var mosaic = new MosaicObject { SourceImage = source, Region = new Rect(0, 0, 10, 10) };
            AssertPixel(mosaic, 2, 2, 255, 0);
            var green = new byte[20 * 10 * 4];
            for (int i = 0; i < green.Length; i += 4) { green[i + 1] = 255; green[i + 3] = 255; }
            source.WritePixels(new Int32Rect(0, 0, 20, 10), green, 80, 0);
            AssertPixel(mosaic, 2, 2, 0, 255);
        });
    }

    [Theory]
    [InlineData(-8, 10, 1, 255, 0)]
    [InlineData(8, 4, 9, 0, 255)]
    [InlineData(18, 8, 19, 0, 255)]
    public void SamplesPartialAndOutOfBoundsBlocksWithoutChangingTheGrid(
        int left, int width, int sampleX, int red, int green)
    {
        RunInSta(() =>
        {
            var mosaic = new MosaicObject
            {
                SourceImage = CreateTwoColorSource(),
                Region = new Rect(left, 0, width, 10)
            };
            AssertPixel(mosaic, sampleX, 2, red, green);
        });
    }

    [Fact]
    public void MovingRenderedCloneDoesNotChangeOriginal()
    {
        RunInSta(() =>
        {
            var original = new MosaicObject
            {
                SourceImage = CreateTwoColorSource(), Region = new Rect(0, 0, 10, 10)
            };
            AssertPixel(original, 2, 2, 255, 0);
            var clone = (MosaicObject)original.Clone();
            clone.Move(new Vector(10, 0));
            AssertPixel(clone, 12, 2, 0, 255);
            AssertPixel(original, 2, 2, 255, 0);
        });
    }

    [Fact]
    public void SmallRegionDoesNotAllocateFullFourKImageOnRepaint()
    {
        RunInSta(() =>
        {
            var source = BitmapSource.Create(3840, 2160, 96, 96, PixelFormats.Bgra32, null,
                new byte[3840 * 2160 * 4], 3840 * 4);
            source.Freeze();
            var mosaic = new MosaicObject { SourceImage = source, Region = new Rect(2, 2, 16, 8) };
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 5; i++) Render(mosaic);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated < 1_000_000, $"Small mosaic allocated {allocated:N0} bytes.");
        });
    }

    private static BitmapSource CreateTwoColorSource()
    {
        var pixels = new byte[20 * 10 * 4];
        for (int y = 0; y < 10; y++)
            for (int x = 0; x < 20; x++)
            {
                int offset = (y * 20 + x) * 4;
                pixels[offset + (x < 10 ? 2 : 1)] = 255;
                pixels[offset + 3] = 255;
            }
        var result = BitmapSource.Create(20, 10, 96, 96, PixelFormats.Bgra32, null, pixels, 80);
        result.Freeze();
        return result;
    }

    private static BitmapSource Render(MosaicObject mosaic)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) mosaic.Render(dc);
        var result = new RenderTargetBitmap(20, 10, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    private static void AssertPixel(MosaicObject mosaic, int x, int y, int red, int green)
    {
        using var result = BitmapHelper.ToBitmap(Render(mosaic));
        var sample = result.GetPixel(x, y);
        Assert.Equal(255, sample.A);
        Assert.Equal(red, sample.R);
        Assert.Equal(green, sample.G);
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
