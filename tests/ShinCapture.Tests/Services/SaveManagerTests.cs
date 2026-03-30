using System;
using System.Drawing;
using System.IO;
using ShinCapture.Models;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class SaveManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SaveManager _manager;

    public SaveManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ShinCapture_Save_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _manager = new SaveManager();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void GenerateFileName_UsesPattern()
    {
        var settings = new AppSettings { Save = { FileNamePattern = "test_{date}_{time}" } };
        var name = SaveManager.GenerateFileName(settings.Save);
        Assert.StartsWith("test_", name);
        Assert.Contains(DateTime.Now.ToString("yyyyMMdd"), name);
    }

    [Fact]
    public void SaveToFile_Png_CreatesFile()
    {
        using var bitmap = new Bitmap(100, 100);
        var path = Path.Combine(_tempDir, "test.png");
        _manager.SaveToFile(bitmap, path, "png", 90);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public void SaveToFile_Jpg_CreatesFile()
    {
        using var bitmap = new Bitmap(100, 100);
        var path = Path.Combine(_tempDir, "test.jpg");
        _manager.SaveToFile(bitmap, path, "jpg", 90);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SaveToFile_Bmp_CreatesFile()
    {
        using var bitmap = new Bitmap(50, 50);
        var path = Path.Combine(_tempDir, "test.bmp");
        _manager.SaveToFile(bitmap, path, "bmp", 90);
        Assert.True(File.Exists(path));
    }
}
