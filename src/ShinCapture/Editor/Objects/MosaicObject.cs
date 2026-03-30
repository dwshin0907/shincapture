using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor.Objects;

public enum MosaicSize
{
    Small = 5,
    Medium = 10,
    Large = 20
}

public class MosaicObject : EditorObject
{
    public Rect Region { get; set; }
    public MosaicSize MosaicSize { get; set; } = MosaicSize.Medium;
    public BitmapSource? SourceImage { get; set; }

    public override Rect Bounds => Region;

    public override void Render(DrawingContext dc)
    {
        int blockSize = (int)MosaicSize;
        double left = Region.X;
        double top = Region.Y;
        double right = Region.X + Region.Width;
        double bottom = Region.Y + Region.Height;

        if (SourceImage == null)
        {
            // Fallback: gray pixelated pattern
            var gray = new SolidColorBrush(Color.FromArgb(180, 128, 128, 128));
            gray.Freeze();
            dc.DrawRectangle(gray, null, Region);
            return;
        }

        int imgW = SourceImage.PixelWidth;
        int imgH = SourceImage.PixelHeight;
        int stride = imgW * 4;
        byte[] pixels = new byte[imgH * stride];
        SourceImage.CopyPixels(pixels, stride, 0);

        for (double y = top; y < bottom; y += blockSize)
        {
            for (double x = left; x < right; x += blockSize)
            {
                // Sample center pixel of the block
                double cx = x + blockSize / 2.0;
                double cy = y + blockSize / 2.0;

                int px = (int)Math.Clamp(cx, 0, imgW - 1);
                int py = (int)Math.Clamp(cy, 0, imgH - 1);

                int idx = py * stride + px * 4;
                byte b = pixels[idx];
                byte g = pixels[idx + 1];
                byte r = pixels[idx + 2];
                byte a = pixels[idx + 3];

                var color = Color.FromArgb(a, r, g, b);
                var brush = new SolidColorBrush(color);
                brush.Freeze();

                double bw = Math.Min(blockSize, right - x);
                double bh = Math.Min(blockSize, bottom - y);
                dc.DrawRectangle(brush, null, new Rect(x, y, bw, bh));
            }
        }
    }

    public override bool HitTest(Point point)
    {
        return Region.Contains(point);
    }

    public override EditorObject Clone()
    {
        return new MosaicObject
        {
            Region = Region,
            MosaicSize = MosaicSize,
            SourceImage = SourceImage,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
