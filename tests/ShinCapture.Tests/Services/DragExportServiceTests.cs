using System.Drawing;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public sealed class DragExportServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), $"ShinCapture_DragExport_{Guid.NewGuid():N}");

    public DragExportServiceTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void CreatesUniqueDecodablePngWithoutLeavingPartialFiles()
    {
        var service = new DragExportService(
            _tempDir, TimeSpan.FromHours(24), maxFiles: 100, maxBytes: 1024 * 1024);
        var now = new DateTimeOffset(2026, 7, 12, 18, 45, 12, TimeSpan.FromHours(9));
        using var bitmap = new Bitmap(8, 6);

        string first = service.CreatePng(bitmap, now);
        string second = service.CreatePng(bitmap, now);

        Assert.NotEqual(first, second);
        Assert.EndsWith(".png", first, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFiles(_tempDir, "*.tmp"));
        using Image decoded = Image.FromFile(first);
        Assert.Equal(8, decoded.Width);
        Assert.Equal(6, decoded.Height);
    }

    [Fact]
    public void CleanupRemovesExpiredFilesThenEnforcesFileCount()
    {
        var service = new DragExportService(
            _tempDir, TimeSpan.FromHours(24), maxFiles: 2, maxBytes: 1024 * 1024);
        var now = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        string expired = CreateCacheFile("expired", 1, now.AddHours(-25));
        string oldest = CreateCacheFile("oldest", 1, now.AddHours(-3));
        string middle = CreateCacheFile("middle", 1, now.AddHours(-2));
        string newest = CreateCacheFile("newest", 1, now.AddHours(-1));

        service.Cleanup(now);

        Assert.False(File.Exists(expired));
        Assert.False(File.Exists(oldest));
        Assert.True(File.Exists(middle));
        Assert.True(File.Exists(newest));
    }

    [Fact]
    public void CleanupEnforcesTotalByteLimitOldestFirst()
    {
        var service = new DragExportService(
            _tempDir, TimeSpan.FromHours(24), maxFiles: 100, maxBytes: 10);
        var now = new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        string oldest = CreateCacheFile("oldest", 6, now.AddHours(-3));
        string middle = CreateCacheFile("middle", 6, now.AddHours(-2));
        string newest = CreateCacheFile("newest", 2, now.AddHours(-1));

        service.Cleanup(now);

        Assert.False(File.Exists(oldest));
        Assert.True(File.Exists(middle));
        Assert.True(File.Exists(newest));
    }

    private string CreateCacheFile(
        string suffix, int length, DateTimeOffset lastWriteTime)
    {
        string path = Path.Combine(_tempDir, $"ShinCapture_{suffix}.png");
        File.WriteAllBytes(path, new byte[length]);
        File.SetLastWriteTimeUtc(path, lastWriteTime.UtcDateTime);
        return path;
    }
}
