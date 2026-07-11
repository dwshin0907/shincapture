using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShinCapture.Models;

namespace ShinCapture.Services;

public class SettingsManager
{
    private readonly string _settingsDir;
    private readonly string _filePath;
    private readonly object _sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public event EventHandler? SettingsChanged;

    public SettingsManager(string? settingsDir = null)
    {
        if (settingsDir != null)
        {
            _settingsDir = settingsDir;
        }
        else
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var portableMarker = Path.Combine(exeDir, "portable.txt");
            _settingsDir = File.Exists(portableMarker)
                ? Path.Combine(exeDir, "config")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShinCapture");
        }
        _filePath = Path.Combine(_settingsDir, "settings.json");
    }

    public AppSettings Load()
    {
        lock (_sync)
        {
            return LoadCore();
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_sync)
        {
            WriteCore(settings);
        }
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Update(Action<AppSettings> update, bool raiseChanged = true)
    {
        ArgumentNullException.ThrowIfNull(update);

        lock (_sync)
        {
            var settings = LoadCore();
            update(settings);
            WriteCore(settings);
        }

        if (raiseChanged)
            SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private AppSettings LoadCore()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSettings();
            var json = File.ReadAllText(_filePath);
            return Normalize(JsonSerializer.Deserialize<AppSettings>(json, JsonOptions));
        }
        catch
        {
            return new AppSettings();
        }
    }

    private void WriteCore(AppSettings settings)
    {
        Directory.CreateDirectory(_settingsDir);
        var json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private static AppSettings Normalize(AppSettings? settings)
    {
        settings ??= new AppSettings();
        settings.General ??= new GeneralSettings();
        settings.Capture ??= new CaptureSettings();
        settings.Editor ??= new EditorSettings();
        settings.Save ??= new SaveSettings();
        settings.Hotkeys ??= new HotkeySettings();
        settings.Ocr ??= new OcrSettings();
        settings.Ai ??= new AiSettings();
        settings.RecentCaptures ??= new RecentCapturesSettings();
        return settings;
    }
}
