using System.Drawing;
using System.Drawing.Imaging;
using ShinCapture.Helpers;

namespace ShinCapture.Tests.Helpers;

public class BitmapHelperTests
{
    [Fact]
    public void ToBitmapSource_PreservesDimensions()
    {
        using var bmp = new Bitmap(7, 3, PixelFormat.Format32bppArgb);

        var src = BitmapHelper.ToBitmapSource(bmp);

        Assert.Equal(7, src.PixelWidth);
        Assert.Equal(3, src.PixelHeight);
        Assert.Equal(System.Windows.Media.PixelFormats.Bgra32, src.Format);
        Assert.True(src.IsFrozen);
    }

    [Fact]
    public void ToBitmapSource_PreservesColorChannelOrderAndAlpha()
    {
        using var bmp = new Bitmap(2, 1, PixelFormat.Format32bppArgb);
        // 채널 뒤바뀜을 잡기 위해 R/G/B를 모두 다른 값으로.
        bmp.SetPixel(0, 0, Color.FromArgb(255, 10, 20, 30)); // 불투명
        bmp.SetPixel(1, 0, Color.FromArgb(0, 200, 100, 50)); // 완전 투명(비프리멀티 → 색상 보존)

        var src = BitmapHelper.ToBitmapSource(bmp);

        int stride = src.PixelWidth * 4;
        var pixels = new byte[stride * src.PixelHeight];
        src.CopyPixels(pixels, stride, 0);

        // Bgra32 메모리 순서: B, G, R, A
        // 픽셀(0,0): R=10 G=20 B=30 A=255
        Assert.Equal(30, pixels[0]);  // B
        Assert.Equal(20, pixels[1]);  // G
        Assert.Equal(10, pixels[2]);  // R
        Assert.Equal(255, pixels[3]); // A

        // 픽셀(1,0): R=200 G=100 B=50 A=0 — 프리멀티가 아님을 보장(색상이 0으로 뭉개지면 안 됨)
        Assert.Equal(50, pixels[4]);  // B
        Assert.Equal(100, pixels[5]); // G
        Assert.Equal(200, pixels[6]); // R
        Assert.Equal(0, pixels[7]);   // A
    }

    [Fact]
    public void ToBitmap_ReturnsIndependentBitmapThatPreservesPixelsAndCanBeSaved()
    {
        using var original = new Bitmap(3, 2, PixelFormat.Format32bppArgb);
        original.SetPixel(0, 0, Color.FromArgb(255, 11, 22, 33));
        original.SetPixel(2, 1, Color.FromArgb(128, 201, 102, 53));
        var source = BitmapHelper.ToBitmapSource(original);

        using Bitmap converted = BitmapHelper.ToBitmap(source);

        Assert.Equal(3, converted.Width);
        Assert.Equal(2, converted.Height);
        Assert.Equal(Color.FromArgb(255, 11, 22, 33), converted.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(128, 201, 102, 53), converted.GetPixel(2, 1));

        using var encoded = new MemoryStream();
        converted.Save(encoded, ImageFormat.Png);
        encoded.Position = 0;
        using Image decoded = Image.FromStream(encoded);
        Assert.Equal(3, decoded.Width);
        Assert.Equal(2, decoded.Height);
    }

    [Fact]
    public void EncodePng_ProducesDecodableImage()
    {
        using var bitmap = new Bitmap(5, 4, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
            graphics.Clear(Color.CornflowerBlue);
        var source = BitmapHelper.ToBitmapSource(bitmap);

        byte[] png = BitmapHelper.EncodePng(source);

        using var stream = new MemoryStream(png);
        using Image decoded = Image.FromStream(stream);
        Assert.Equal(5, decoded.Width);
        Assert.Equal(4, decoded.Height);
    }
}
