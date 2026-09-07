using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using ShinCapture.Capture;

namespace ShinCapture.Tests.Capture;

public sealed class SmartCutProcessorTests
{
    [Fact]
    public void ExtractsColoredCircleWithTransparentBackgroundAndAntialiasedEdge()
    {
        using Bitmap input = CreateCanvas(128, 128, Color.White);
        using (Graphics graphics = Graphics.FromImage(input))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(28, 104, 214));
            graphics.FillEllipse(brush, 24, 24, 80, 80);
        }
        PointF[] polygon = CreateCirclePolygon(64, 64, 43, 32);

        using Bitmap result = SmartCutProcessor.Process(input, polygon);

        Assert.NotSame(input, result);
        Assert.Equal(input.Size, result.Size);
        Assert.Equal(0, result.GetPixel(4, 4).A);
        Color center = result.GetPixel(64, 64);
        Assert.Equal(255, center.A);
        Assert.Equal(input.GetPixel(64, 64).ToArgb(), center.ToArgb());
        Assert.True(CountPixels(result, color => color.A is > 0 and < 255) > 0);
    }

    [Fact]
    public void PreservesRotatedObjectCornersAndOriginalRgb()
    {
        using Bitmap input = CreateCanvas(140, 140, Color.White);
        using (Graphics graphics = Graphics.FromImage(input))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(70, 70);
            graphics.RotateTransform(32);
            using var brush = new SolidBrush(Color.FromArgb(220, 54, 72));
            graphics.FillRectangle(brush, -38, -22, 76, 44);
            graphics.ResetTransform();
        }
        PointF[] polygon =
        [
            new(46, 27), new(112, 68), new(94, 113), new(27, 72)
        ];

        using Bitmap result = SmartCutProcessor.Process(input, polygon);

        Assert.Equal(255, result.GetPixel(70, 70).A);
        Assert.Equal(input.GetPixel(70, 70).ToArgb(), result.GetPixel(70, 70).ToArgb());
        Assert.Equal(0, result.GetPixel(5, 5).A);
        Assert.True(CountPixels(result, color => color.A is > 0 and < 255) >= 8);
    }

    [Fact]
    public void LooseLassoStillRemovesBackgroundAroundSmallObject()
    {
        using Bitmap input = CreateCanvas(140, 140, Color.White);
        using (Graphics graphics = Graphics.FromImage(input))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.SeaGreen);
            graphics.FillEllipse(brush, 52, 52, 36, 36);
        }
        PointF[] loosePolygon =
        [
            new(12, 12), new(127, 12), new(127, 127), new(12, 127)
        ];

        using Bitmap result = SmartCutProcessor.Process(input, loosePolygon);

        Assert.Equal(255, result.GetPixel(70, 70).A);
        Assert.Equal(0, result.GetPixel(25, 25).A);
        Assert.True(CountPixels(result, color => color.A > 0) < 3_000);
    }

    [Fact]
    public void ThinSelectionFallsBackToAntialiasedLassoInsteadOfEmptyImage()
    {
        using Bitmap input = CreateCanvas(100, 50, Color.FromArgb(38, 170, 92));
        PointF[] thinPolygon =
        [
            new(5.25f, 20.25f), new(94.5f, 22.25f),
            new(94.5f, 27.5f), new(5.25f, 25.5f)
        ];

        using Bitmap result = SmartCutProcessor.Process(input, thinPolygon);

        Assert.True(CountPixels(result, color => color.A > 0) > 200);
        Assert.Equal(0, result.GetPixel(0, 0).A);
        Color retained = result.GetPixel(50, 24);
        Assert.True(retained.A > 0);
        Assert.Equal(input.GetPixel(50, 24).R, retained.R);
        Assert.Equal(input.GetPixel(50, 24).G, retained.G);
        Assert.Equal(input.GetPixel(50, 24).B, retained.B);
    }

    [Fact]
    public void UniformFullFrameSelectionUsesSafeOpaqueFallback()
    {
        using Bitmap input = CreateCanvas(48, 32, Color.DarkOrange);
        PointF[] fullFrame =
        [
            new(0, 0), new(47, 0), new(47, 31), new(0, 31)
        ];

        using Bitmap result = SmartCutProcessor.Process(input, fullFrame);

        Assert.Equal(input.Size, result.Size);
        Assert.Equal(Color.DarkOrange.ToArgb(), result.GetPixel(24, 16).ToArgb());
        Assert.Equal(255, result.GetPixel(0, 0).A);
    }

    [Fact]
    public void LargeSelectionKeepsOriginalOutputResolution()
    {
        using Bitmap input = CreateCanvas(2048, 1152, Color.MediumPurple);
        PointF[] fullFrame =
        [
            new(0, 0), new(2047, 0), new(2047, 1151), new(0, 1151)
        ];

        using Bitmap result = SmartCutProcessor.Process(input, fullFrame);

        Assert.Equal(2048, result.Width);
        Assert.Equal(1152, result.Height);
        Assert.Equal(Color.MediumPurple.ToArgb(), result.GetPixel(1024, 576).ToArgb());
    }

    [Fact]
    public void FourKContourKeepsResolutionCornersAndSmoothEdgeWithinBoundedTime()
    {
        using Bitmap input = CreateCanvas(3840, 2160, Color.FromArgb(28, 34, 44));
        using (Graphics graphics = Graphics.FromImage(input))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(1920, 1080);
            graphics.RotateTransform(27);
            using var brush = new SolidBrush(Color.FromArgb(238, 124, 42));
            graphics.FillRectangle(brush, -720, -390, 1440, 780);
            graphics.ResetTransform();
        }
        PointF[] polygon =
        [
            new(1450, 345), new(2790, 1025), new(2385, 1810), new(1045, 1130)
        ];
        var stopwatch = Stopwatch.StartNew();

        using Bitmap result = SmartCutProcessor.Process(input, polygon);
        stopwatch.Stop();

        Assert.Equal(input.Size, result.Size);
        Assert.Equal(255, result.GetPixel(1920, 1080).A);
        Assert.Equal(0, result.GetPixel(200, 200).A);
        Assert.True(CountPixelsInRegion(
            result,
            new Rectangle(950, 250, 1950, 1650),
            color => color.A is > 0 and < 255) > 40);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20),
            $"4K smart cut took {stopwatch.Elapsed}.");

        string? artifactPath = Environment.GetEnvironmentVariable(
            "SHINCAPTURE_SMARTCUT_ARTIFACT");
        if (!string.IsNullOrWhiteSpace(artifactPath))
            SaveComparisonArtifact(input, result, artifactPath);
    }

    [Fact]
    public void RepeatedOrDegeneratePointsAreRejectedClearly()
    {
        using Bitmap input = CreateCanvas(20, 20, Color.White);
        PointF[] points = [new(4, 4), new(4, 4), new(5, 5), new(6, 6), new(4, 4)];

        Assert.Throws<ArgumentException>(() => SmartCutProcessor.Process(input, points));
        Assert.Equal(Color.White.ToArgb(), input.GetPixel(10, 10).ToArgb());
    }

    [Fact]
    public void PreCancelledRequestStopsWithoutTakingOwnershipOfInput()
    {
        using Bitmap input = CreateCanvas(40, 40, Color.CadetBlue);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        PointF[] polygon = [new(2, 2), new(37, 2), new(37, 37), new(2, 37)];

        Assert.Throws<OperationCanceledException>(() =>
            SmartCutProcessor.Process(input, polygon, cancellation.Token));
        Assert.Equal(Color.CadetBlue.ToArgb(), input.GetPixel(20, 20).ToArgb());
    }

    private static Bitmap CreateCanvas(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    private static PointF[] CreateCirclePolygon(
        float centerX,
        float centerY,
        float radius,
        int pointCount) =>
        Enumerable.Range(0, pointCount)
            .Select(index => index * Math.PI * 2 / pointCount)
            .Select(angle => new PointF(
                centerX + radius * (float)Math.Cos(angle),
                centerY + radius * (float)Math.Sin(angle)))
            .ToArray();

    private static int CountPixels(Bitmap bitmap, Func<Color, bool> predicate)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
        {
            if (predicate(bitmap.GetPixel(x, y))) count++;
        }
        return count;
    }

    private static int CountPixelsInRegion(
        Bitmap bitmap,
        Rectangle region,
        Func<Color, bool> predicate)
    {
        int count = 0;
        for (int y = region.Top; y < region.Bottom; y += 2)
        for (int x = region.Left; x < region.Right; x += 2)
        {
            if (predicate(bitmap.GetPixel(x, y))) count++;
        }
        return count;
    }

    private static void SaveComparisonArtifact(
        Bitmap source,
        Bitmap extracted,
        string path)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        const int panelWidth = 960;
        const int panelHeight = 540;
        using var comparison = new Bitmap(panelWidth * 2, panelHeight);
        using Graphics graphics = Graphics.FromImage(comparison);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, new Rectangle(0, 0, panelWidth, panelHeight));
        const int tile = 18;
        for (int y = 0; y < panelHeight; y += tile)
        for (int x = panelWidth; x < panelWidth * 2; x += tile)
        {
            bool light = ((x / tile) + (y / tile)) % 2 == 0;
            using var brush = new SolidBrush(light ? Color.WhiteSmoke : Color.LightGray);
            graphics.FillRectangle(brush, x, y, tile, tile);
        }
        graphics.DrawImage(
            extracted,
            new Rectangle(panelWidth, 0, panelWidth, panelHeight));
        comparison.Save(fullPath, ImageFormat.Png);
    }
}
