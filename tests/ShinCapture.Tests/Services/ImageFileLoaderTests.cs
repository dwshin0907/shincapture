using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Services;
using ShinCapture.Tests.Editor;

namespace ShinCapture.Tests.Services;

public class ImageFileLoaderTests
{
    [Theory]
    [InlineData("png")] [InlineData("jpg")] [InlineData("bmp")]
    [InlineData("gif")] [InlineData("tiff")]
    public void OpensImageWithoutKeepingFileLocked(string format) => WatermarkFactoryTests.RunSta(() =>
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "." + format);
        try
        {
            var source = BitmapSource.Create(12, 8, 96, 96, PixelFormats.Bgra32, null, new byte[384], 48);
            BitmapEncoder encoder = format switch
            {
                "png" => new PngBitmapEncoder(), "jpg" => new JpegBitmapEncoder(),
                "bmp" => new BmpBitmapEncoder(), "gif" => new GifBitmapEncoder(), _ => new TiffBitmapEncoder()
            };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var file = File.Create(path)) encoder.Save(file);
            var loaded = ImageFileLoader.Load(path);
            Assert.Equal(12, loaded.PixelWidth); Assert.Equal(8, loaded.PixelHeight);
            Assert.True(loaded.IsFrozen);
            File.Delete(path);
            Assert.False(File.Exists(path));
            var output = new byte[12 * 8 * 4];
            new FormatConvertedBitmap(loaded, PixelFormats.Bgra32, null, 0).CopyPixels(output, 48, 0);
        }
        finally { File.Delete(path); }
    });

    [Fact]
    public void CorruptFileIsRejectedAndUnlocked()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "not an image");
            Assert.ThrowsAny<Exception>(() => ImageFileLoader.Load(path));
            using var file = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { File.Delete(path); }
    }
}
