using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Helpers;

namespace ShinCapture.Editor;

public static class EditorCompositeRenderer
{
    public static Bitmap Render(
        BitmapSource sourceImage,
        IEnumerable<EditorObject> objects)
    {
        int width = sourceImage.PixelWidth;
        int height = sourceImage.PixelHeight;
        var visual = new DrawingVisual();
        using (DrawingContext drawingContext = visual.RenderOpen())
        {
            drawingContext.DrawImage(sourceImage, new Rect(0, 0, width, height));
            foreach (EditorObject editorObject in objects.Where(item => item.IsVisible))
                editorObject.RenderWithTransform(drawingContext);
        }

        var target = new RenderTargetBitmap(
            width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        return BitmapHelper.ToBitmap(target);
    }
}
