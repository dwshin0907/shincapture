using System.Drawing;
using System.Reflection;
using ShinCapture.Capture;

namespace ShinCapture.Tests.Capture;

public class ScrollCaptureModeTests
{
    [Fact]
    public void RefineCaptureRegionToContentColumn_ExcludesSideColumns()
    {
        using var screenshot = new Bitmap(320, 220);
        using (var graphics = Graphics.FromImage(screenshot))
        {
            graphics.Clear(Color.FromArgb(245, 245, 245));
            graphics.FillRectangle(new SolidBrush(Color.FromArgb(232, 232, 232)), 20, 20, 56, 160);
            graphics.FillRectangle(Brushes.White, 96, 20, 128, 160);
            graphics.FillRectangle(new SolidBrush(Color.FromArgb(238, 238, 238)), 244, 20, 36, 160);
            graphics.FillRectangle(new SolidBrush(Color.FromArgb(230, 230, 230)), 76, 20, 20, 160);
            graphics.FillRectangle(new SolidBrush(Color.FromArgb(230, 230, 230)), 224, 20, 20, 160);

            for (var y = 30; y < 170; y += 16)
            {
                graphics.FillRectangle(Brushes.DimGray, 108, y, 92, 4);
                graphics.FillRectangle(Brushes.LightGray, 108, y + 8, 70, 3);
            }
        }

        var region = new Rectangle(20, 20, 260, 160);
        var refined = (Rectangle)InvokePrivateStatic(
            "RefineCaptureRegionToContentColumn",
            [screenshot, region, 150, 110])!;

        Assert.InRange(refined.Left, 94, 98);
        Assert.InRange(refined.Right, 222, 226);
        Assert.Equal(region.Top, refined.Top);
        Assert.Equal(region.Bottom, refined.Bottom);
    }
    [Fact]
    public void DetectStickyHeader_WithMinorFrameNoise_StillDetectsHeader()
    {
        using var frame1 = CreateViewportFrame(scrollOffset: 0, animationSeed: 1, headerNoiseX: 4, footerNoiseX: 6);
        using var frame2 = CreateViewportFrame(scrollOffset: 50, animationSeed: 2, headerNoiseX: 5, footerNoiseX: 7);
        using var frame3 = CreateViewportFrame(scrollOffset: 100, animationSeed: 3, headerNoiseX: 6, footerNoiseX: 8);
        var frames = new List<Bitmap> { frame1, frame2, frame3 };

        var headerHeight = (int)InvokePrivateStatic(
            "DetectStickyHeader",
            [frames])!;
        var footerHeight = (int)InvokePrivateStatic(
            "DetectStickyFooter",
            [frames])!;

        Assert.Equal(HeaderHeight, headerHeight);
        Assert.Equal(FooterHeight, footerHeight);
    }

    [Fact]
    public void StitchWithOverlapRemoval_WithAnimatedBodyRegion_ReconstructsSingleContinuousPage()
    {
        using var frame1 = CreateViewportFrame(scrollOffset: 0, animationSeed: 1, headerNoiseX: 4, footerNoiseX: 6);
        using var frame2 = CreateViewportFrame(scrollOffset: 50, animationSeed: 2, headerNoiseX: 5, footerNoiseX: 7);
        using var frame3 = CreateViewportFrame(scrollOffset: 100, animationSeed: 3, headerNoiseX: 6, footerNoiseX: 8);
        using var frame4 = CreateViewportFrame(scrollOffset: 150, animationSeed: 4, headerNoiseX: 7, footerNoiseX: 9);

        var frames = new List<Bitmap> { frame1, frame2, frame3, frame4 };

        var headerHeight = (int)InvokePrivateStatic(
            "DetectStickyHeader",
            [frames])!;
        var footerHeight = (int)InvokePrivateStatic(
            "DetectStickyFooter",
            [frames])!;


        using var stitched = (Bitmap)InvokePrivateStatic(
            "StitchWithOverlapRemoval",
            [frames, headerHeight, footerHeight])!;

        Assert.InRange(stitched.Height, HeaderHeight + DocumentHeight + FooterHeight - 1, HeaderHeight + DocumentHeight + FooterHeight + 1);
        Assert.Equal(ViewportWidth, stitched.Width);

        AssertRowMatchesExpected(stitched, 0, HeaderColor);
        AssertRowMatchesExpected(stitched, HeaderHeight + 15, GetDocumentColor(15));
        AssertRowMatchesExpected(stitched, HeaderHeight + 95, GetDocumentColor(95));
        AssertRowMatchesExpected(stitched, HeaderHeight + 175, GetDocumentColor(175));
        AssertRowMatchesExpected(stitched, HeaderHeight + 225, GetDocumentColor(225));
        AssertRowMatchesExpected(stitched, stitched.Height - 1, FooterColor);
    }

    private static void AssertRowMatchesExpected(Bitmap bitmap, int y, Color expected)
    {
        for (var candidateY = Math.Max(0, y - 1); candidateY <= Math.Min(bitmap.Height - 1, y + 1); candidateY++)
        {
            var rowMatches = true;
            for (var x = 0; x < bitmap.Width; x += 12)
            {
                var actual = bitmap.GetPixel(x, candidateY);
                if (!ColorNear(expected, actual, tolerance: 4))
                {
                    rowMatches = false;
                    break;
                }
            }

            if (rowMatches)
                return;
        }

        Assert.Fail($"No matching row found near y={y}.");
    }

    private static bool ColorNear(Color expected, Color actual, int tolerance)
    {
        return
            Math.Abs(expected.R - actual.R) <= tolerance &&
            Math.Abs(expected.G - actual.G) <= tolerance &&
            Math.Abs(expected.B - actual.B) <= tolerance;
    }

    private static object? InvokePrivateStatic(string methodName, object?[] args)
    {
        var method = typeof(ScrollCaptureMode).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return method!.Invoke(null, args);
    }

    private static Bitmap CreateViewportFrame(int scrollOffset, int animationSeed, int headerNoiseX, int footerNoiseX)
    {
        var bitmap = new Bitmap(ViewportWidth, ViewportHeight);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.Black);
        graphics.FillRectangle(new SolidBrush(HeaderColor), 0, 0, ViewportWidth, HeaderHeight);
        graphics.FillRectangle(new SolidBrush(FooterColor), 0, ViewportHeight - FooterHeight, ViewportWidth, FooterHeight);

        for (var y = 0; y < ContentViewportHeight; y++)
        {
            var documentY = scrollOffset + y;
            var color = GetDocumentColor(documentY);
            using var pen = new Pen(color);
            graphics.DrawLine(pen, 0, HeaderHeight + y, ViewportWidth - 1, HeaderHeight + y);
        }

        AddRepeatedMarkers(graphics, scrollOffset);
        AddAnimatedDocumentRegion(graphics, scrollOffset, animationSeed);

        bitmap.SetPixel(headerNoiseX, 3, GetNoiseColor(animationSeed));
        bitmap.SetPixel(footerNoiseX, ViewportHeight - 3, GetNoiseColor(animationSeed + 10));

        return bitmap;
    }

    private static void AddRepeatedMarkers(Graphics graphics, int scrollOffset)
    {
        for (var blockStart = 0; blockStart < DocumentHeight; blockStart += 40)
        {
            var localY = blockStart - scrollOffset;
            if (localY < 0 || localY >= ContentViewportHeight)
            {
                continue;
            }

            var top = HeaderHeight + localY;
            graphics.FillRectangle(Brushes.WhiteSmoke, 18, top, ViewportWidth - 36, 3);
            if (top + 12 < HeaderHeight + ContentViewportHeight)
            {
                graphics.FillRectangle(Brushes.Gainsboro, 36, top + 12, ViewportWidth - 72, 2);
            }
        }
    }

    private static void AddAnimatedDocumentRegion(Graphics graphics, int scrollOffset, int animationSeed)
    {
        const int animatedDocTop = 120;
        const int animatedDocHeight = 34;
        var overlapTop = Math.Max(animatedDocTop, scrollOffset);
        var overlapBottom = Math.Min(animatedDocTop + animatedDocHeight, scrollOffset + ContentViewportHeight);
        if (overlapTop >= overlapBottom)
        {
            return;
        }

        var localTop = HeaderHeight + (overlapTop - scrollOffset);
        var height = overlapBottom - overlapTop;

        using var brush = new SolidBrush(GetNoiseColor(animationSeed * 3));
        graphics.FillRectangle(brush, 24, localTop, ViewportWidth - 48, height);

        var stripeOffset = animationSeed % 9;
        using var stripePen = new Pen(Color.FromArgb(30 + animationSeed * 20, 30, 30), 2);
        for (var x = 24 + stripeOffset; x < ViewportWidth - 24; x += 12)
        {
            graphics.DrawLine(stripePen, x, localTop, x, localTop + height - 1);
        }
    }

    private static Color GetDocumentColor(int documentY)
    {
        return Color.FromArgb(
            180 + (documentY * 17 % 50),
            175 + (documentY * 11 % 60),
            170 + (documentY * 7 % 70));
    }

    private static Color GetNoiseColor(int seed)
    {
        return Color.FromArgb(
            80 + (seed * 17 % 120),
            60 + (seed * 23 % 120),
            70 + (seed * 29 % 120));
    }

    private const int ViewportWidth = 120;
    private const int ViewportHeight = 120;
    private const int HeaderHeight = 12;
    private const int FooterHeight = 8;
    private const int ContentViewportHeight = ViewportHeight - HeaderHeight - FooterHeight;
    private const int DocumentHeight = 250;
    private static readonly Color HeaderColor = Color.FromArgb(35, 86, 152);
    private static readonly Color FooterColor = Color.FromArgb(64, 64, 64);
}









