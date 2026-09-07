using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Services;

public static class ImageFileLoader
{
    public const string DialogFilter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|모든 파일|*.*";

    public static BitmapSource Load(string path)
    {
        // Decode before closing the stream: imported files remain replaceable/deletable.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
        var frame = decoder.Frames[0]; // GIF/TIFF: first frame/page only.
        if ((long)frame.PixelWidth * frame.PixelHeight > 80_000_000)
            throw new InvalidDataException("8천만 화소 이하의 이미지를 열어주세요.");
        BitmapSource image = new CachedBitmap(frame, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        // Respect camera EXIF orientation, including mirrored orientations.
        int orientation = 1;
        try
        {
            if (frame.Metadata is BitmapMetadata metadata)
            {
                object? value = metadata.GetQuery("/app1/ifd/{ushort=274}")
                    ?? metadata.GetQuery("/ifd/{ushort=274}");
                if (value != null) orientation = Convert.ToInt32(value);
            }
        }
        catch (NotSupportedException) { }
        var transform = new TransformGroup();
        if (orientation is 2 or 4 or 5 or 7) transform.Children.Add(new ScaleTransform(-1, 1));
        int angle = orientation switch { 3 or 4 => 180, 5 or 8 => 270, 6 or 7 => 90, _ => 0 };
        if (angle != 0) transform.Children.Add(new RotateTransform(angle));
        if (transform.Children.Count > 0) image = new TransformedBitmap(image, transform);
        image.Freeze();
        return image;
    }
}
