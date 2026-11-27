using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using ShinCapture.Helpers;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public sealed class ClipboardImageServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), $"ShinCapture Clipboard 한글 {Guid.NewGuid():N}");

    public ClipboardImageServiceTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void DefaultPackageContainsImageUnicodePathAndRawFileDropPath()
    {
        string path = Path.Combine(_tempDir, "편집 결과 이미지.png");
        File.WriteAllBytes(path, [1]);
        using var bitmap = new Bitmap(2, 1, PixelFormat.Format32bppArgb);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 17, 83, 201));
        var source = BitmapHelper.ToBitmapSource(bitmap);

        using BitmapHelper.ClipboardDataPackage package =
            BitmapHelper.BuildClipboardDataPackage(source, path);
        var data = package.DataObject;

        Assert.True(data.GetDataPresent("PNG", autoConvert: false));
        Assert.True(data.GetDataPresent(
            System.Windows.Forms.DataFormats.Bitmap,
            autoConvert: false));
        Assert.True(data.GetDataPresent(
            System.Windows.Forms.DataFormats.UnicodeText,
            autoConvert: false));
        Assert.True(data.GetDataPresent(
            System.Windows.Forms.DataFormats.FileDrop,
            autoConvert: false));
        Assert.Equal($"\"{Path.GetFullPath(path)}\"", data.GetData(
            System.Windows.Forms.DataFormats.UnicodeText,
            autoConvert: false));

        StringCollection files = data.GetFileDropList();
        Assert.Single(files.Cast<string>());
        Assert.Equal(Path.GetFullPath(path), files[0]);

        var png = Assert.IsAssignableFrom<Stream>(data.GetData("PNG", autoConvert: false));
        png.Position = 0;
        using var decoded = new Bitmap(png);
        Assert.Equal(Color.FromArgb(255, 17, 83, 201), decoded.GetPixel(0, 0));
    }

    [Fact]
    public void ImageOnlyPackageHasNoTextOrFileFormatsEvenWithAutomaticConversion()
    {
        using var bitmap = new Bitmap(2, 1, PixelFormat.Format32bppArgb);
        bitmap.SetPixel(0, 0, Color.FromArgb(0, 0, 0, 0));
        bitmap.SetPixel(1, 0, Color.FromArgb(255, 17, 83, 201));
        var source = BitmapHelper.ToBitmapSource(bitmap);

        using BitmapHelper.ClipboardDataPackage package =
            BitmapHelper.BuildClipboardDataPackage(source);
        var data = package.DataObject;

        Assert.True(data.GetDataPresent("PNG", autoConvert: false));
        Assert.True(data.GetDataPresent(System.Windows.Forms.DataFormats.Bitmap, autoConvert: false));
        foreach (string format in new[]
        {
            System.Windows.Forms.DataFormats.Text,
            System.Windows.Forms.DataFormats.UnicodeText,
            System.Windows.Forms.DataFormats.OemText,
            System.Windows.Forms.DataFormats.StringFormat,
            System.Windows.Forms.DataFormats.FileDrop
        })
        {
            Assert.False(data.GetDataPresent(format, autoConvert: true), format);
        }

        var png = Assert.IsAssignableFrom<Stream>(data.GetData("PNG", autoConvert: false));
        png.Position = 0;
        using var decoded = new Bitmap(png);
        Assert.Equal(0, decoded.GetPixel(0, 0).A);
        Assert.Equal(Color.FromArgb(255, 17, 83, 201), decoded.GetPixel(1, 0));

        var flat = Assert.IsType<Bitmap>(data.GetData(System.Windows.Forms.DataFormats.Bitmap, autoConvert: false));
        Assert.Equal(Color.White.ToArgb(), flat.GetPixel(0, 0).ToArgb());
        Assert.Equal(Color.FromArgb(255, 17, 83, 201), flat.GetPixel(1, 0));
    }

    [Fact]
    public void TerminalPathIsAlwaysQuotedForSafeDirectPaste()
    {
        string path = Path.Combine(Path.GetTempPath(), "shell&name.png");

        Assert.Equal($"\"{Path.GetFullPath(path)}\"", BitmapHelper.QuotePathForTerminal(path));
    }

    [Theory]
    [InlineData("capture\nnext.png")]
    [InlineData("capture\rnext.png")]
    public void TerminalPathRejectsLineBreaks(string fileName)
    {
        Assert.Throws<ArgumentException>(() =>
            BitmapHelper.QuotePathForTerminal(Path.Combine(_tempDir, fileName)));
    }

    [Fact]
    public void EditorPackageRejectsMissingFileInsteadOfBuildingImageOnlyPayload()
    {
        string missing = Path.Combine(_tempDir, "missing.png");
        using var bitmap = new Bitmap(1, 1);
        var source = BitmapHelper.ToBitmapSource(bitmap);

        var error = Assert.Throws<FileNotFoundException>(() =>
            BitmapHelper.BuildClipboardDataPackage(source, missing));

        Assert.Equal(Path.GetFullPath(missing), error.FileName);
    }

    [Fact]
    public void ExportUsesCurrentSnapshotPixelsAndCreatesUniquePersistentFiles()
    {
        var exporter = new DragExportService(_tempDir);
        using var firstBitmap = new Bitmap(2, 1, PixelFormat.Format32bppArgb);
        firstBitmap.SetPixel(0, 0, Color.Red);
        var firstSource = BitmapHelper.ToBitmapSource(firstBitmap);
        using var secondBitmap = new Bitmap(2, 1, PixelFormat.Format32bppArgb);
        secondBitmap.SetPixel(0, 0, Color.Blue);
        var secondSource = BitmapHelper.ToBitmapSource(secondBitmap);

        string first = exporter.CreatePng(firstSource);
        string second = exporter.CreatePng(secondSource);

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
        using var firstDecoded = new Bitmap(first);
        using var secondDecoded = new Bitmap(second);
        Assert.Equal(Color.Red.ToArgb(), firstDecoded.GetPixel(0, 0).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), secondDecoded.GetPixel(0, 0).ToArgb());
    }

    [Fact]
    public void ExportReportsFileSystemErrors()
    {
        string occupiedPath = Path.Combine(_tempDir, "not-a-directory");
        File.WriteAllText(occupiedPath, "occupied");
        var exporter = new DragExportService(occupiedPath);
        using var bitmap = new Bitmap(1, 1);
        var source = BitmapHelper.ToBitmapSource(bitmap);

        Assert.ThrowsAny<IOException>(() =>
            exporter.CreatePng(source));
    }
}
