using ShinCapture.Models;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class SettingsManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsManager _manager;

    public SettingsManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ShinCapture_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _manager = new SettingsManager(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_WhenNoFile_ReturnsDefaults()
    {
        var settings = _manager.Load();
        Assert.False(settings.General.AutoStart);
        Assert.True(settings.General.MinimizeToTray);
        Assert.Equal("ko", settings.General.Language);
        Assert.Equal(AfterCaptureAction.OpenEditor, settings.Capture.AfterCapture);
        Assert.Equal("png", settings.Save.DefaultFormat);
        Assert.Equal(90, settings.Save.JpgQuality);
        Assert.True(settings.Save.CopyToClipboard);
        Assert.Equal("PrintScreen", settings.Hotkeys.RegionCapture);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var settings = _manager.Load();
        settings.General.AutoStart = true;
        settings.Save.DefaultFormat = "jpg";
        settings.Save.JpgQuality = 75;
        _manager.Save(settings);
        var loaded = _manager.Load();
        Assert.True(loaded.General.AutoStart);
        Assert.Equal("jpg", loaded.Save.DefaultFormat);
        Assert.Equal(75, loaded.Save.JpgQuality);
    }

    [Fact]
    public void Save_CreatesJsonFile()
    {
        var settings = _manager.Load();
        _manager.Save(settings);
        var filePath = Path.Combine(_tempDir, "settings.json");
        Assert.True(File.Exists(filePath));
        var json = File.ReadAllText(filePath);
        Assert.Contains("general", json);
        Assert.Contains("hotkeys", json);
    }

    [Fact]
    public void SettingsChanged_EventFires_OnSave()
    {
        var fired = false;
        _manager.SettingsChanged += (_, _) => fired = true;
        _manager.Save(_manager.Load());
        Assert.True(fired);
    }

    [Fact]
    public void Load_WithCorruptedFile_ReturnsDefaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "settings.json"), "NOT VALID JSON {{{");
        var settings = _manager.Load();
        Assert.Equal("ko", settings.General.Language);
    }

    [Fact]
    public void FixedSizes_HasDefaultPresets()
    {
        var settings = _manager.Load();
        Assert.Equal(2, settings.FixedSizes.Count);
        Assert.Equal("HD", settings.FixedSizes[0].Name);
        Assert.Equal(1280, settings.FixedSizes[0].Width);
    }

    [Fact]
    public void Save_ThenLoad_PersistsTextCaptureHotkey()
    {
        var settings = _manager.Load();
        settings.Hotkeys.TextCapture = "Ctrl+Alt+O";
        _manager.Save(settings);
        var loaded = _manager.Load();
        Assert.Equal("Ctrl+Alt+O", loaded.Hotkeys.TextCapture);
    }

    [Fact]
    public void Load_WhenNoFile_OcrDefaults()
    {
        var settings = _manager.Load();
        Assert.Equal("ko", settings.Ocr.Language);
        Assert.True(settings.Ocr.UpscaleSmallImages);
    }

    [Fact]
    public void Save_ThenLoad_PersistsOcrSettings()
    {
        var settings = _manager.Load();
        settings.Ocr.Language = "en-US";
        settings.Ocr.UpscaleSmallImages = false;
        _manager.Save(settings);
        var loaded = _manager.Load();
        Assert.Equal("en-US", loaded.Ocr.Language);
        Assert.False(loaded.Ocr.UpscaleSmallImages);
    }
}
