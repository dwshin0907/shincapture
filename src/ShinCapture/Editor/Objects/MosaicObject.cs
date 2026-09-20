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
    private Rect _region;
    private MosaicSize _mosaicSize = MosaicSize.Medium;
    private BitmapSource? _sourceImage;
    private DrawingGroup? _cachedDrawing;

    public Rect Region
    {
        get => _region;
        set
        {
            if (_region == value) return;
            _region = value;
            _cachedDrawing = null;
        }
    }

    public MosaicSize MosaicSize
    {
        get => _mosaicSize;
        set
        {
            if (_mosaicSize == value) return;
            _mosaicSize = value;
            _cachedDrawing = null;
        }
    }

    public BitmapSource? SourceImage
    {
        get => _sourceImage;
        set
        {
            if (ReferenceEquals(_sourceImage, value)) return;
            _sourceImage = value;
            _cachedDrawing = null;
        }
    }

    public override Rect Bounds => Region;

    public override void Render(DrawingContext dc)
    {
        if (Region.IsEmpty || Region.Width <= 0 || Region.Height <= 0) return;

        if (SourceImage == null)
        {
            var gray = new SolidColorBrush(Color.FromArgb(180, 128, 128, 128));
            gray.Freeze();
            dc.DrawRectangle(gray, null, Region);
            return;
        }

        // Frozen captures cannot change underneath the cached drawing. Mutable
        // sources (e.g. WriteableBitmap) must be sampled again on every render.
        DrawingGroup drawing = _cachedDrawing ?? CreateDrawing(SourceImage);
        if (SourceImage.IsFrozen) _cachedDrawing = drawing;
        dc.DrawDrawing(drawing);
    }

    private DrawingGroup CreateDrawing(BitmapSource source)
    {
        int blockSize = Math.Max(1, (int)MosaicSize);
        double left = Region.X;
        double top = Region.Y;
        double right = Region.X + Region.Width;
        double bottom = Region.Y + Region.Height;

        int imgW = source.PixelWidth;
        int imgH = source.PixelHeight;
        // Include the last partial block's center, which can extend past Region.
        int sampleLeft = (int)Math.Clamp(left, 0, imgW - 1);
        int sampleTop = (int)Math.Clamp(top, 0, imgH - 1);
        int sampleRight = (int)Math.Clamp(right + blockSize / 2.0, 0, imgW - 1);
        int sampleBottom = (int)Math.Clamp(bottom + blockSize / 2.0, 0, imgH - 1);
        var sampleRect = new Int32Rect(sampleLeft, sampleTop,
            sampleRight - sampleLeft + 1, sampleBottom - sampleTop + 1);
        BitmapSource sampleImage = new CroppedBitmap(source, sampleRect);
        if (sampleImage.Format != PixelFormats.Bgra32)
            sampleImage = new FormatConvertedBitmap(sampleImage, PixelFormats.Bgra32, null, 0);
        int stride = checked(sampleRect.Width * 4);
        byte[] pixels = new byte[checked(sampleRect.Height * stride)];
        sampleImage.CopyPixels(pixels, stride, 0);

        var drawing = new DrawingGroup();
        using (DrawingContext dc = drawing.Open())
        {
            for (double y = top; y < bottom; y += blockSize)
            {
                for (double x = left; x < right; x += blockSize)
                {
                    // Sample center pixel of the block.
                    double cx = x + blockSize / 2.0;
                    double cy = y + blockSize / 2.0;

                    int px = (int)Math.Clamp(cx, 0, imgW - 1);
                    int py = (int)Math.Clamp(cy, 0, imgH - 1);

                    int idx = (py - sampleTop) * stride + (px - sampleLeft) * 4;
                    byte b = pixels[idx];
                    byte g = pixels[idx + 1];
                    byte r = pixels[idx + 2];
                    byte a = pixels[idx + 3];

                    var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                    brush.Freeze();

                    double bw = Math.Min(blockSize, right - x);
                    double bh = Math.Min(blockSize, bottom - y);
                    dc.DrawRectangle(brush, null, new Rect(x, y, bw, bh));
                }
            }
        }
        drawing.Freeze();
        return drawing;
    }

    public override bool HitTest(Point point)
    {
        return Region.Contains(point);
    }

    public override void Scale(double factor, Point anchor)
    {
        double x = anchor.X + (Region.X - anchor.X) * factor;
        double y = anchor.Y + (Region.Y - anchor.Y) * factor;
        Region = new Rect(x, y, Region.Width * factor, Region.Height * factor);
    }

    public override void Move(Vector delta)
    {
        Region = new Rect(Region.X + delta.X, Region.Y + delta.Y, Region.Width, Region.Height);
    }

    public override EditorObject Clone()
    {
        return new MosaicObject
        {
            Region = Region,
            MosaicSize = MosaicSize,
            SourceImage = SourceImage,
            IsSelected = IsSelected,
            IsVisible = IsVisible,
            _cachedDrawing = _cachedDrawing
        };
    }
}
