using System;
using System.Collections.Generic;
using System.IO;

namespace ShinCapture.Models;

public class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public CaptureSettings Capture { get; set; } = new();
    public SaveSettings Save { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public OcrSettings Ocr { get; set; } = new();
    public List<FixedSizePreset>? FixedSizes { get; set; } = new()
    {
        new() { Name = "HD", Width = 1280, Height = 720 },
        new() { Name = "FHD", Width = 1920, Height = 1080 }
    };
    public RecentCapturesSettings RecentCaptures { get; set; } = new();
    public int[] CustomColors { get; set; } = Array.Empty<int>();
}

public class GeneralSettings
{
    public bool AutoStart { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public string Language { get; set; } = "ko";
}

public class CaptureSettings
{
    public AfterCaptureAction AfterCapture { get; set; } = AfterCaptureAction.OpenEditor;
    public int MagnifierZoom { get; set; } = 2;
    public bool ShowCrosshair { get; set; } = true;
    public bool ShowColorCode { get; set; } = true;
}

public class SaveSettings
{
    public string DefaultFormat { get; set; } = "png";
    public int JpgQuality { get; set; } = 90;
    public string AutoSavePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ShinCapture");
    public string FileNamePattern { get; set; } = "신캡쳐_{date}_{time}";
    public bool AutoSave { get; set; } = false;
    public bool CopyToClipboard { get; set; } = true;
}

public class HotkeySettings
{
    public string RegionCapture { get; set; } = "PrintScreen";
    public string FreeformCapture { get; set; } = "Ctrl+Shift+F";
    public string WindowCapture { get; set; } = "Ctrl+Shift+W";
    public string ElementCapture { get; set; } = "Ctrl+Shift+D";
    public string FullscreenCapture { get; set; } = "Ctrl+Shift+A";
    public string ScrollCapture { get; set; } = "Ctrl+Shift+S";
    public string FixedSizeCapture { get; set; } = "Ctrl+Shift+Z";
    public string TextCapture { get; set; } = "Ctrl+Shift+T";

    // true: PrintScreen 키를 신캡쳐가 독점 (Windows Snipping Tool 선점 해제)
    // false: Windows 기본 동작에 양보 (PrintScreen → Snipping Tool)
    public bool OverridePrintScreen { get; set; } = true;
}

public class RecentCapturesSettings
{
    public int MaxCount { get; set; } = 100;
}

public class OcrSettings
{
    public string Language { get; set; } = "ko";
    public bool UpscaleSmallImages { get; set; } = true;
}
