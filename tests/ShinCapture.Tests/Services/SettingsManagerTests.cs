using System.Text.Json;
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
    public void Load_LegacySettingsWithoutEditor_UsesEditorDefaultsAndPreservesGeneralSettings()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "settings.json"),
            "{\"general\":{\"autoStart\":true}}");

        var loaded = _manager.Load();

        Assert.True(loaded.General.AutoStart);
        Assert.Equal(EditorWindowSizeMode.RememberLast, loaded.Editor.WindowSizeMode);
        Assert.Equal(1100, loaded.Editor.WindowWidth);
        Assert.Equal(750, loaded.Editor.WindowHeight);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsEditorSettings()
    {
        var settings = _manager.Load();
        settings.Editor.WindowSizeMode = EditorWindowSizeMode.Maximized;
        settings.Editor.WindowWidth = 1280;
        settings.Editor.WindowHeight = 720;

        _manager.Save(settings);
        var loaded = _manager.Load();

        Assert.Equal(EditorWindowSizeMode.Maximized, loaded.Editor.WindowSizeMode);
        Assert.Equal(1280, loaded.Editor.WindowWidth);
        Assert.Equal(720, loaded.Editor.WindowHeight);
    }

    [Fact]
    public void Update_WithoutRaisingChanged_PreservesHotkeyAndUpdatesEditorSettings()
    {
        var settings = _manager.Load();
        settings.Hotkeys.RegionCapture = "Ctrl+Alt+9";
        _manager.Save(settings);
        var eventCount = 0;
        _manager.SettingsChanged += (_, _) => eventCount++;

        _manager.Update(
            current =>
            {
                current.Editor.WindowWidth = 1234;
                current.Editor.WindowHeight = 777;
            },
            raiseChanged: false);
        var loaded = _manager.Load();

        Assert.Equal("Ctrl+Alt+9", loaded.Hotkeys.RegionCapture);
        Assert.Equal(1234, loaded.Editor.WindowWidth);
        Assert.Equal(777, loaded.Editor.WindowHeight);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void Update_ByDefault_RaisesSettingsChangedExactlyOnce()
    {
        var eventCount = 0;
        _manager.SettingsChanged += (_, _) => eventCount++;

        _manager.Update(settings => settings.Editor.WindowWidth = 1234);

        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void Update_WhenExistingJsonIsCorrupted_ThrowsAndPreservesFileAndEvent()
    {
        var filePath = Path.Combine(_tempDir, "settings.json");
        const string corruptedJson = "NOT VALID JSON {{{";
        File.WriteAllText(filePath, corruptedJson);
        var eventCount = 0;
        _manager.SettingsChanged += (_, _) => eventCount++;

        Assert.Throws<JsonException>(() =>
            _manager.Update(settings => settings.General.AutoStart = true));

        Assert.Equal(corruptedJson, File.ReadAllText(filePath));
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void Update_WhenCallbackThrows_PreservesFileSettingsAndEvent()
    {
        var settings = _manager.Load();
        settings.General.AutoStart = false;
        _manager.Save(settings);
        var filePath = Path.Combine(_tempDir, "settings.json");
        var originalJson = File.ReadAllText(filePath);
        var expectedException = new InvalidOperationException("Callback failed.");
        var eventCount = 0;
        _manager.SettingsChanged += (_, _) => eventCount++;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _manager.Update(current =>
            {
                current.General.AutoStart = true;
                throw expectedException;
            }));

        Assert.Same(expectedException, exception);
        Assert.Equal(originalJson, File.ReadAllText(filePath));
        Assert.False(_manager.Load().General.AutoStart);
        Assert.Equal(0, eventCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Update_WhenCallbackReentersMutation_ThrowsAndPreservesFileAndEvent(bool useSave)
    {
        var settings = _manager.Load();
        settings.General.AutoStart = false;
        _manager.Save(settings);
        var filePath = Path.Combine(_tempDir, "settings.json");
        var originalJson = File.ReadAllText(filePath);
        var eventCount = 0;
        _manager.SettingsChanged += (_, _) => eventCount++;

        Assert.Throws<InvalidOperationException>(() =>
            _manager.Update(current =>
            {
                current.Editor.WindowWidth = 1234;
                if (useSave)
                {
                    _manager.Save(current);
                }
                else
                {
                    _manager.Update(nested => nested.General.AutoStart = true);
                }
            }));

        Assert.Equal(originalJson, File.ReadAllText(filePath));
        Assert.False(_manager.Load().General.AutoStart);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void Update_WithNullCallback_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _manager.Update(null!));
    }

    [Fact]
    public void Update_ConcurrentSilentIncrements_DoesNotLoseChanges()
    {
        var settings = _manager.Load();
        settings.RecentCaptures.MaxCount = 0;
        _manager.Save(settings);
        var eventCount = 0;
        _manager.SettingsChanged += (_, _) => eventCount++;
        const int updateCount = 32;

        Parallel.For(
            0,
            updateCount,
            _ => _manager.Update(
                current => current.RecentCaptures.MaxCount++,
                raiseChanged: false));

        Assert.Equal(updateCount, _manager.Load().RecentCaptures.MaxCount);
        Assert.Equal(0, eventCount);
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
    public void Save_WithNullSettings_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _manager.Save(null!));
        Assert.False(File.Exists(Path.Combine(_tempDir, "settings.json")));
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
    public void Load_WithExplicitNullObjectSections_NormalizesDefaults()
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "settings.json"),
            """
            {
              "general": null,
              "capture": null,
              "editor": null,
              "save": null,
              "hotkeys": null,
              "ocr": null,
              "ai": null,
              "recentCaptures": null
            }
            """);

        var loaded = _manager.Load();

        Assert.NotNull(loaded.General);
        Assert.NotNull(loaded.Capture);
        Assert.NotNull(loaded.Editor);
        Assert.NotNull(loaded.Save);
        Assert.NotNull(loaded.Hotkeys);
        Assert.NotNull(loaded.Ocr);
        Assert.NotNull(loaded.Ai);
        Assert.NotNull(loaded.RecentCaptures);
        Assert.Equal("ko", loaded.General.Language);
        Assert.Equal(AfterCaptureAction.OpenEditor, loaded.Capture.AfterCapture);
        Assert.Equal(EditorWindowSizeMode.RememberLast, loaded.Editor.WindowSizeMode);
        Assert.Equal("png", loaded.Save.DefaultFormat);
        Assert.Equal("PrintScreen", loaded.Hotkeys.RegionCapture);
        Assert.Equal("ko", loaded.Ocr.Language);
        Assert.Equal("openai", loaded.Ai.Provider);
        Assert.Equal(100, loaded.RecentCaptures.MaxCount);
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

    [Fact]
    public void RoundTrip_PreservesAiSettings()
    {
        var dir = Path.Combine(Path.GetTempPath(), "shincap_test_" + Guid.NewGuid());
        try
        {
            var mgr = new SettingsManager(dir);
            var s = new AppSettings();
            s.Ai.Enabled = true;
            s.Ai.Model = "gpt-4o-mini";
            s.Ai.TargetLanguage = "ko";
            mgr.Save(s);

            var loaded = new SettingsManager(dir).Load();
            Assert.True(loaded.Ai.Enabled);
            Assert.Equal("gpt-4o-mini", loaded.Ai.Model);
            Assert.Equal("ko", loaded.Ai.TargetLanguage);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
