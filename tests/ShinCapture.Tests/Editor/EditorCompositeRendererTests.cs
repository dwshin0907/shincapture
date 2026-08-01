using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Editor;
using ShinCapture.Editor.Objects;
using ShinCapture.Helpers;
using DrawingColor = System.Drawing.Color;
using WpfPoint = System.Windows.Point;

namespace ShinCapture.Tests.Editor;

public class EditorCompositeRendererTests
{
    [Fact]
    public void RendersVisibleEditorObjectsOverSourceImage()
    {
        using Bitmap result = RunInSta(() =>
        {
            using var sourceBitmap = CreateWhiteBitmap();
            var source = BitmapHelper.ToBitmapSource(sourceBitmap);
            var shape = new ShapeObject
            {
                Start = new WpfPoint(2, 2),
                End = new WpfPoint(18, 18),
                StrokeColor = Colors.Red,
                FillMode = FillMode.Solid
            };

            return EditorCompositeRenderer.Render(source, [shape]);
        });

        DrawingColor center = result.GetPixel(10, 10);
        Assert.True(center.R > 200);
        Assert.True(center.G < 80);
        Assert.True(center.B < 80);
    }

    [Fact]
    public void SkipsHiddenEditorObjects()
    {
        using Bitmap result = RunInSta(() =>
        {
            using var sourceBitmap = CreateWhiteBitmap();
            var source = BitmapHelper.ToBitmapSource(sourceBitmap);
            var shape = new ShapeObject
            {
                Start = new WpfPoint(2, 2),
                End = new WpfPoint(18, 18),
                StrokeColor = Colors.Red,
                FillMode = FillMode.Solid,
                IsVisible = false
            };

            return EditorCompositeRenderer.Render(source, [shape]);
        });

        DrawingColor center = result.GetPixel(10, 10);
        Assert.True(center.R > 240);
        Assert.True(center.G > 240);
        Assert.True(center.B > 240);
    }

    [Fact]
    public void RenderBitmapSource_ReturnsFrozenCrossThreadSafeImage()
    {
        BitmapSource result = RunInSta(() =>
        {
            using var sourceBitmap = CreateWhiteBitmap();
            var source = BitmapHelper.ToBitmapSource(sourceBitmap);
            return EditorCompositeRenderer.RenderBitmapSource(source, []);
        });

        Assert.True(result.IsFrozen);
        Assert.Equal(20, result.PixelWidth);
        Assert.Equal(20, result.PixelHeight);
        using Bitmap converted = BitmapHelper.ToBitmap(result);
        Assert.Equal(DrawingColor.White.ToArgb(), converted.GetPixel(10, 10).ToArgb());
    }

    private static Bitmap CreateWhiteBitmap()
    {
        var bitmap = new Bitmap(20, 20);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(DrawingColor.White);
        return bitmap;
    }

    private static T RunInSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception error) { exception = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null) throw exception;
        return result!;
    }
}
