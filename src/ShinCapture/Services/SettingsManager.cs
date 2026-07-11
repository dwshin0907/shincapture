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
    private bool _isUpdating;
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
        ArgumentNullException.ThrowIfNull(settings);

        lock (_sync)
        {
            ThrowIfUpdating();
            WriteCore(settings);
        }
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Update(Action<AppSettings> update, bool raiseChanged = true)
    {
        ArgumentNullException.ThrowIfNull(update);

        lock (_sync)
        {
            ThrowIfUpdating();
            var settings = LoadStrictCore();
            _isUpdating = true;
            try
            {
                update(settings);
            }
            finally
            {
                _isUpdating = false;
            }
            WriteCore(settings);
        }

        if (raiseChanged)
            SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private AppSettings LoadCore()
    {
        try
        {
            return LoadStrictCore();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private AppSettings LoadStrictCore()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();
        var json = File.ReadAllText(_filePath);
        return Normalize(JsonSerializer.Deserialize<AppSettings>(json, JsonOptions));
    }

    private void WriteCore(AppSettings settings)
    {
        Directory.CreateDirectory(_settingsDir);
        var json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
        var tempFilePath = Path.Combine(_settingsDir, $"{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }
    }

    private void ThrowIfUpdating()
    {
        if (_isUpdating)
            throw new InvalidOperationException(
                "Settings cannot be saved or updated from within an update callback.");
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
