# ShinCapture (신캡쳐) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a lightweight, ad-free screen capture + editor program for Windows that replaces AlCapture with 7 capture modes, 14 editing tools, and Windows 11 Fluent Design.

**Architecture:** Single-process WPF application (.NET 8) with 5 core modules — TrayManager (system tray + global hotkeys), CaptureEngine (7 capture modes via transparent overlay), ImageEditor (canvas-based editing with Command pattern undo/redo), SettingsManager (JSON config), and SaveManager (PNG/JPG/BMP/GIF + clipboard). All theme values externalized to ResourceDictionary for easy design changes.

**Tech Stack:** C# / .NET 8 / WPF / SkiaSharp / System.Text.Json / Win32 Interop (RegisterHotKey, BitBlt, EnumWindows, UIAutomation) / xUnit (tests) / Inno Setup (installer)

**Spec:** `docs/superpowers/specs/2026-03-30-shincapture-design.md`

---

## File Structure

```
ShinCapture/
├── ShinCapture.sln
├── src/
│   └── ShinCapture/
│       ├── ShinCapture.csproj
│       ├── App.xaml / App.xaml.cs
│       ├── Models/
│       │   ├── AppSettings.cs              ← Settings POCO with defaults
│       │   ├── CaptureMode.cs              ← Enum for 7 capture modes
│       │   ├── CaptureResult.cs            ← Bitmap + metadata after capture
│       │   ├── RecentCaptureEntry.cs        ← Path + thumbnail + timestamp
│       │   ├── HotkeyBinding.cs            ← Modifier + key + action mapping
│       │   └── FixedSizePreset.cs          ← Name + width + height
│       ├── Services/
│       │   ├── SettingsManager.cs          ← JSON load/save, defaults, events
│       │   ├── HotkeyManager.cs            ← RegisterHotKey wrapper, dispatch
│       │   ├── CaptureService.cs           ← Screen capture via BitBlt
│       │   ├── SaveManager.cs              ← File save, clipboard, auto-naming
│       │   └── RecentCapturesManager.cs    ← Recent list CRUD, thumbnail cache
│       ├── Views/
│       │   ├── MainWindow.xaml/.cs         ← Tray icon + main window with mode grid
│       │   ├── EditorWindow.xaml/.cs       ← Editor with toolbar + canvas + status bar
│       │   ├── SettingsWindow.xaml/.cs     ← Tabbed settings dialog
│       │   └── Overlay/
│       │       └── CaptureOverlay.xaml/.cs ← Fullscreen transparent overlay
│       ├── ViewModels/
│       │   ├── MainViewModel.cs
│       │   ├── EditorViewModel.cs
│       │   └── SettingsViewModel.cs
│       ├── Editor/
│       │   ├── EditorCanvas.cs             ← Custom Canvas with zoom/pan
│       │   ├── IEditorCommand.cs           ← Undo/redo interface
│       │   ├── CommandStack.cs             ← Undo/redo stack management
│       │   ├── EditorObject.cs             ← Base class for drawn objects
│       │   ├── ITool.cs                    ← Tool interface
│       │   ├── ToolBase.cs                 ← Shared tool logic
│       │   ├── Tools/
│       │   │   ├── PenTool.cs
│       │   │   ├── HighlighterTool.cs
│       │   │   ├── ShapeTool.cs
│       │   │   ├── ArrowTool.cs
│       │   │   ├── TextTool.cs
│       │   │   ├── MosaicTool.cs
│       │   │   ├── BlurTool.cs
│       │   │   ├── NumberTool.cs
│       │   │   ├── BalloonTool.cs
│       │   │   ├── CropTool.cs
│       │   │   ├── EraserTool.cs
│       │   │   ├── EyedropperTool.cs
│       │   │   └── ImageInsertTool.cs
│       │   └── Objects/
│       │       ├── StrokeObject.cs         ← Pen/highlighter strokes
│       │       ├── ShapeObject.cs          ← Rectangle, ellipse, line
│       │       ├── ArrowObject.cs
│       │       ├── TextObject.cs
│       │       ├── MosaicObject.cs
│       │       ├── BlurObject.cs
│       │       ├── NumberObject.cs
│       │       ├── BalloonObject.cs
│       │       └── ImageObject.cs
│       ├── Capture/
│       │   ├── ICaptureMode.cs             ← Mode interface
│       │   ├── RegionCaptureMode.cs        ← Drag rectangle selection
│       │   ├── FreeformCaptureMode.cs      ← Polygon selection
│       │   ├── WindowCaptureMode.cs        ← Window detection + highlight
│       │   ├── ElementCaptureMode.cs       ← UIAutomation element detection
│       │   ├── FullscreenCaptureMode.cs    ← Instant full screen
│       │   ├── ScrollCaptureMode.cs        ← Auto-scroll + stitch
│       │   ├── FixedSizeCaptureMode.cs     ← Fixed frame move + click
│       │   └── Magnifier.cs               ← Zoom lens + color picker overlay
│       ├── Themes/
│       │   └── LightTheme.xaml             ← All colors, spacing, fonts
│       ├── Helpers/
│       │   ├── NativeMethods.cs            ← Win32 P/Invoke declarations
│       │   ├── ScreenHelper.cs             ← Multi-monitor, DPI helpers
│       │   └── BitmapHelper.cs             ← BitmapSource ↔ SKBitmap conversion
│       └── Assets/
│           └── icon.ico
├── tests/
│   └── ShinCapture.Tests/
│       ├── ShinCapture.Tests.csproj
│       ├── Services/
│       │   ├── SettingsManagerTests.cs
│       │   ├── SaveManagerTests.cs
│       │   └── RecentCapturesManagerTests.cs
│       └── Editor/
│           ├── CommandStackTests.cs
│           └── EditorObjectTests.cs
├── installer/
│   └── setup.iss
└── README.md
```

---

## Phase 1: Project Foundation (Tasks 1–4)

### Task 1: Project Scaffolding

**Files:**
- Create: `ShinCapture.sln`
- Create: `src/ShinCapture/ShinCapture.csproj`
- Create: `src/ShinCapture/App.xaml`
- Create: `src/ShinCapture/App.xaml.cs`
- Create: `tests/ShinCapture.Tests/ShinCapture.Tests.csproj`
- Create: `src/ShinCapture/Assets/icon.ico`

- [ ] **Step 1: Create solution and WPF project**

```bash
cd "C:/AI/신캡쳐"
dotnet new sln -n ShinCapture
mkdir -p src/ShinCapture
cd src/ShinCapture
dotnet new wpf -n ShinCapture --framework net8.0
cd ../..
dotnet sln add src/ShinCapture/ShinCapture.csproj
```

- [ ] **Step 2: Add NuGet packages**

```bash
cd src/ShinCapture
dotnet add package SkiaSharp --version 2.88.*
dotnet add package SkiaSharp.Views.WPF --version 2.88.*
dotnet add package System.Drawing.Common --version 8.*
```

- [ ] **Step 3: Create test project**

```bash
cd "C:/AI/신캡쳐"
mkdir -p tests/ShinCapture.Tests
cd tests/ShinCapture.Tests
dotnet new xunit -n ShinCapture.Tests --framework net8.0
dotnet add reference ../../src/ShinCapture/ShinCapture.csproj
cd ../..
dotnet sln add tests/ShinCapture.Tests/ShinCapture.Tests.csproj
```

- [ ] **Step 4: Edit .csproj for single-file publish and app icon**

Edit `src/ShinCapture/ShinCapture.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ApplicationIcon>Assets\icon.ico</ApplicationIcon>
    <AssemblyName>ShinCapture</AssemblyName>
    <RootNamespace>ShinCapture</RootNamespace>
    <Version>1.0.0</Version>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: Create placeholder app icon**

Generate a simple 32x32 .ico file at `src/ShinCapture/Assets/icon.ico`. Use any tool or a placeholder — will be replaced with real icon later.

- [ ] **Step 6: Configure App.xaml for no main window on startup**

The app starts minimized to tray, no main window visible. Edit `src/ShinCapture/App.xaml`:

```xml
<Application x:Class="ShinCapture.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Themes/LightTheme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

Edit `src/ShinCapture/App.xaml.cs`:

```csharp
using System.Windows;

namespace ShinCapture;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // TrayManager will be initialized here in Task 3
    }
}
```

- [ ] **Step 7: Build and verify**

```bash
cd "C:/AI/신캡쳐"
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 8: Initialize git and commit**

```bash
cd "C:/AI/신캡쳐"
git init
```

Create `.gitignore`:

```
bin/
obj/
*.user
*.suo
.vs/
.superpowers/
```

```bash
git add -A
git commit -m "feat: initial project scaffolding — .NET 8 WPF + xUnit"
```

---

### Task 2: Theme System (LightTheme.xaml)

**Files:**
- Create: `src/ShinCapture/Themes/LightTheme.xaml`

- [ ] **Step 1: Create LightTheme.xaml with all design tokens**

Create `src/ShinCapture/Themes/LightTheme.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:system="clr-namespace:System;assembly=mscorlib">

    <!-- === Colors === -->
    <Color x:Key="AccentColor">#0078D4</Color>
    <Color x:Key="AccentHoverColor">#106EBE</Color>
    <Color x:Key="AccentPressedColor">#005A9E</Color>

    <Color x:Key="BackgroundPrimaryColor">#F3F3F3</Color>
    <Color x:Key="BackgroundSecondaryColor">#FBFBFB</Color>
    <Color x:Key="BackgroundTertiaryColor">#F7F7F7</Color>
    <Color x:Key="BackgroundCanvasColor">#E8E8E8</Color>

    <Color x:Key="TextPrimaryColor">#191919</Color>
    <Color x:Key="TextSecondaryColor">#555555</Color>
    <Color x:Key="TextDisabledColor">#CCCCCC</Color>

    <Color x:Key="BorderColor">#E5E5E5</Color>
    <Color x:Key="BorderLightColor">#EBEBEB</Color>
    <Color x:Key="DividerColor">#E0E0E0</Color>

    <Color x:Key="ToolbarButtonHoverColor">#F0F0F0</Color>
    <Color x:Key="ToolbarButtonActiveColor">#E8E8E8</Color>
    <Color x:Key="ToolbarButtonActiveBorderColor">#D0D0D0</Color>

    <Color x:Key="ErrorColor">#C42B1C</Color>

    <!-- === Brushes === -->
    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}"/>
    <SolidColorBrush x:Key="AccentHoverBrush" Color="{StaticResource AccentHoverColor}"/>

    <SolidColorBrush x:Key="BackgroundPrimaryBrush" Color="{StaticResource BackgroundPrimaryColor}"/>
    <SolidColorBrush x:Key="BackgroundSecondaryBrush" Color="{StaticResource BackgroundSecondaryColor}"/>
    <SolidColorBrush x:Key="BackgroundTertiaryBrush" Color="{StaticResource BackgroundTertiaryColor}"/>
    <SolidColorBrush x:Key="BackgroundCanvasBrush" Color="{StaticResource BackgroundCanvasColor}"/>

    <SolidColorBrush x:Key="TextPrimaryBrush" Color="{StaticResource TextPrimaryColor}"/>
    <SolidColorBrush x:Key="TextSecondaryBrush" Color="{StaticResource TextSecondaryColor}"/>
    <SolidColorBrush x:Key="TextDisabledBrush" Color="{StaticResource TextDisabledColor}"/>

    <SolidColorBrush x:Key="BorderBrush" Color="{StaticResource BorderColor}"/>
    <SolidColorBrush x:Key="BorderLightBrush" Color="{StaticResource BorderLightColor}"/>
    <SolidColorBrush x:Key="DividerBrush" Color="{StaticResource DividerColor}"/>

    <SolidColorBrush x:Key="ToolbarButtonHoverBrush" Color="{StaticResource ToolbarButtonHoverColor}"/>
    <SolidColorBrush x:Key="ToolbarButtonActiveBrush" Color="{StaticResource ToolbarButtonActiveColor}"/>
    <SolidColorBrush x:Key="ToolbarButtonActiveBorderBrush" Color="{StaticResource ToolbarButtonActiveBorderColor}"/>

    <SolidColorBrush x:Key="ErrorBrush" Color="{StaticResource ErrorColor}"/>

    <!-- === Dimensions === -->
    <CornerRadius x:Key="ButtonCornerRadius">6</CornerRadius>
    <CornerRadius x:Key="PanelCornerRadius">8</CornerRadius>
    <CornerRadius x:Key="WindowCornerRadius">8</CornerRadius>

    <Thickness x:Key="ToolbarPadding">6,6,6,6</Thickness>
    <Thickness x:Key="ToolbarButtonPadding">6,6,6,6</Thickness>
    <system:Double x:Key="ToolbarButtonMinWidth">40</system:Double>
    <system:Double x:Key="ToolbarGroupSpacing">12</system:Double>

    <!-- === Typography === -->
    <FontFamily x:Key="AppFont">Segoe UI Variable, Segoe UI</FontFamily>
    <system:Double x:Key="FontSizeSmall">11</system:Double>
    <system:Double x:Key="FontSizeNormal">12</system:Double>
    <system:Double x:Key="FontSizeMedium">14</system:Double>
    <system:Double x:Key="FontSizeLarge">16</system:Double>

    <!-- === Shadows === -->
    <DropShadowEffect x:Key="CardShadow" ShadowDepth="2" BlurRadius="8"
                      Color="#000000" Opacity="0.08" Direction="270"/>
    <DropShadowEffect x:Key="MenuShadow" ShadowDepth="4" BlurRadius="16"
                      Color="#000000" Opacity="0.14" Direction="270"/>

    <!-- === Editor Colors (drawing palette) === -->
    <Color x:Key="PaletteBlue">#0078D4</Color>
    <Color x:Key="PaletteRed">#E81123</Color>
    <Color x:Key="PaletteGreen">#10893E</Color>
    <Color x:Key="PaletteOrange">#FF8C00</Color>
    <Color x:Key="PaletteBlack">#191919</Color>
    <Color x:Key="PaletteWhite">#FFFFFF</Color>

</ResourceDictionary>
```

- [ ] **Step 2: Build and verify theme loads**

```bash
cd "C:/AI/신캡쳐" && dotnet build
```

Expected: Build succeeded (App.xaml references LightTheme.xaml).

- [ ] **Step 3: Commit**

```bash
git add src/ShinCapture/Themes/LightTheme.xaml
git commit -m "feat: add LightTheme.xaml with all Win11 Fluent design tokens"
```

---

### Task 3: Settings Model + SettingsManager

**Files:**
- Create: `src/ShinCapture/Models/AppSettings.cs`
- Create: `src/ShinCapture/Models/CaptureMode.cs`
- Create: `src/ShinCapture/Models/FixedSizePreset.cs`
- Create: `src/ShinCapture/Services/SettingsManager.cs`
- Create: `tests/ShinCapture.Tests/Services/SettingsManagerTests.cs`

- [ ] **Step 1: Write SettingsManager tests**

Create `tests/ShinCapture.Tests/Services/SettingsManagerTests.cs`:

```csharp
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

        var settings = _manager.Load();
        _manager.Save(settings);

        Assert.True(fired);
    }

    [Fact]
    public void Load_WithCorruptedFile_ReturnsDefaults()
    {
        var filePath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(filePath, "NOT VALID JSON {{{");

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
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd "C:/AI/신캡쳐" && dotnet test
```

Expected: FAIL — types don't exist yet.

- [ ] **Step 3: Create CaptureMode enum**

Create `src/ShinCapture/Models/CaptureMode.cs`:

```csharp
namespace ShinCapture.Models;

public enum CaptureMode
{
    Region,
    Freeform,
    Window,
    Element,
    Fullscreen,
    Scroll,
    FixedSize
}

public enum AfterCaptureAction
{
    OpenEditor,
    SaveDirectly,
    ClipboardOnly
}
```

- [ ] **Step 4: Create FixedSizePreset**

Create `src/ShinCapture/Models/FixedSizePreset.cs`:

```csharp
namespace ShinCapture.Models;

public class FixedSizePreset
{
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
}
```

- [ ] **Step 5: Create AppSettings model**

Create `src/ShinCapture/Models/AppSettings.cs`:

```csharp
using System.IO;

namespace ShinCapture.Models;

public class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public CaptureSettings Capture { get; set; } = new();
    public SaveSettings Save { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public List<FixedSizePreset> FixedSizes { get; set; } = new()
    {
        new() { Name = "HD", Width = 1280, Height = 720 },
        new() { Name = "FHD", Width = 1920, Height = 1080 }
    };
    public RecentCapturesSettings RecentCaptures { get; set; } = new();
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
}

public class RecentCapturesSettings
{
    public int MaxCount { get; set; } = 100;
}
```

- [ ] **Step 6: Create SettingsManager**

Create `src/ShinCapture/Services/SettingsManager.cs`:

```csharp
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShinCapture.Models;

namespace ShinCapture.Services;

public class SettingsManager
{
    private readonly string _settingsDir;
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public event EventHandler? SettingsChanged;

    public SettingsManager(string? settingsDir = null)
    {
        _settingsDir = settingsDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShinCapture");
        _filePath = Path.Combine(_settingsDir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_settingsDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePath, json);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 7: Run tests**

```bash
cd "C:/AI/신캡쳐" && dotnet test --verbosity normal
```

Expected: All 6 tests PASS.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: add AppSettings model and SettingsManager with JSON persistence"
```

---

### Task 4: Native Methods + Screen Helpers

**Files:**
- Create: `src/ShinCapture/Helpers/NativeMethods.cs`
- Create: `src/ShinCapture/Helpers/ScreenHelper.cs`
- Create: `src/ShinCapture/Helpers/BitmapHelper.cs`

- [ ] **Step 1: Create NativeMethods (Win32 P/Invoke)**

Create `src/ShinCapture/Helpers/NativeMethods.cs`:

```csharp
using System.Runtime.InteropServices;

namespace ShinCapture.Helpers;

internal static class NativeMethods
{
    // --- Hotkey ---
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_NOREPEAT = 0x4000;
    public const int WM_HOTKEY = 0x0312;

    // --- Screen Capture ---
    [DllImport("user32.dll")]
    public static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    public const uint SRCCOPY = 0x00CC0020;

    // --- Window Enumeration ---
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    public const uint GA_ROOT = 2;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    // --- Cursor ---
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    // --- Scroll ---
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public const uint WM_VSCROLL = 0x0115;
    public const int SB_LINEDOWN = 1;
    public const int SB_PAGEDOWN = 3;

    // --- Structs ---
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }
}
```

- [ ] **Step 2: Create ScreenHelper**

Create `src/ShinCapture/Helpers/ScreenHelper.cs`:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Interop;

namespace ShinCapture.Helpers;

public static class ScreenHelper
{
    public static Bitmap CaptureFullScreen()
    {
        var virtualLeft = (int)SystemParameters.VirtualScreenLeft;
        var virtualTop = (int)SystemParameters.VirtualScreenTop;
        var virtualWidth = (int)SystemParameters.VirtualScreenWidth;
        var virtualHeight = (int)SystemParameters.VirtualScreenHeight;

        var desktopWnd = NativeMethods.GetDesktopWindow();
        var desktopDc = NativeMethods.GetWindowDC(desktopWnd);
        var memDc = NativeMethods.CreateCompatibleDC(desktopDc);
        var hBitmap = NativeMethods.CreateCompatibleBitmap(desktopDc, virtualWidth, virtualHeight);
        var oldBitmap = NativeMethods.SelectObject(memDc, hBitmap);

        NativeMethods.BitBlt(memDc, 0, 0, virtualWidth, virtualHeight,
            desktopDc, virtualLeft, virtualTop, NativeMethods.SRCCOPY);

        NativeMethods.SelectObject(memDc, oldBitmap);
        var bitmap = Image.FromHbitmap(hBitmap);

        NativeMethods.DeleteObject(hBitmap);
        NativeMethods.DeleteDC(memDc);
        NativeMethods.ReleaseDC(desktopWnd, desktopDc);

        return bitmap;
    }

    public static Bitmap CropBitmap(Bitmap source, Rectangle region)
    {
        return source.Clone(region, source.PixelFormat);
    }

    public static System.Drawing.Color GetPixelColor(Bitmap bitmap, int x, int y)
    {
        if (x >= 0 && x < bitmap.Width && y >= 0 && y < bitmap.Height)
            return bitmap.GetPixel(x, y);
        return System.Drawing.Color.Transparent;
    }

    public static string ColorToHex(System.Drawing.Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
```

- [ ] **Step 3: Create BitmapHelper**

Create `src/ShinCapture/Helpers/BitmapHelper.cs`:

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace ShinCapture.Helpers;

public static class BitmapHelper
{
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = stream;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }

    public static Bitmap ToBitmap(BitmapSource source)
    {
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }
}
```

- [ ] **Step 4: Build and verify**

```bash
cd "C:/AI/신캡쳐" && dotnet build
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add NativeMethods, ScreenHelper, BitmapHelper"
```

---

## Phase 2: Tray + Capture Engine (Tasks 5–11)

### Task 5: HotkeyManager + TrayManager + MainWindow Shell

**Files:**
- Create: `src/ShinCapture/Services/HotkeyManager.cs`
- Create: `src/ShinCapture/Views/MainWindow.xaml`
- Create: `src/ShinCapture/Views/MainWindow.xaml.cs`
- Modify: `src/ShinCapture/App.xaml.cs`

- [ ] **Step 1: Create HotkeyManager**

Create `src/ShinCapture/Services/HotkeyManager.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ShinCapture.Helpers;
using ShinCapture.Models;

namespace ShinCapture.Services;

public class HotkeyManager : IDisposable
{
    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _nextId = 1;

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    public int Register(string hotkeyString, Action action)
    {
        ParseHotkeyString(hotkeyString, out uint modifiers, out uint vk);
        modifiers |= NativeMethods.MOD_NOREPEAT;

        var id = _nextId++;
        if (NativeMethods.RegisterHotKey(_hwnd, id, modifiers, vk))
        {
            _hotkeyActions[id] = action;
            return id;
        }
        return -1;
    }

    public void Unregister(int id)
    {
        if (_hotkeyActions.ContainsKey(id))
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
            _hotkeyActions.Remove(id);
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeyActions.Keys.ToList())
            Unregister(id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public static void ParseHotkeyString(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = hotkey.Split('+').Select(p => p.Trim()).ToList();
        foreach (var part in parts)
        {
            switch (part.ToLower())
            {
                case "ctrl":
                case "control":
                    modifiers |= NativeMethods.MOD_CONTROL;
                    break;
                case "shift":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case "alt":
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case "printscreen":
                    vk = (uint)KeyInterop.VirtualKeyFromKey(Key.PrintScreen);
                    break;
                default:
                    if (Enum.TryParse<Key>(part, true, out var key))
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }
}
```

- [ ] **Step 2: Create MainWindow.xaml (shell with tray icon)**

Create `src/ShinCapture/Views/MainWindow.xaml`:

```xml
<Window x:Class="ShinCapture.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="신캡쳐" Width="360" Height="480"
        WindowStartupLocation="CenterScreen"
        ShowInTaskbar="False"
        Visibility="Hidden"
        Background="{DynamicResource BackgroundPrimaryBrush}"
        FontFamily="{DynamicResource AppFont}"
        FontSize="{DynamicResource FontSizeNormal}">

    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <TextBlock Grid.Row="0" Text="캡쳐 모드"
                   Foreground="{DynamicResource TextSecondaryBrush}"
                   FontSize="{DynamicResource FontSizeSmall}"
                   Margin="0,0,0,10"/>

        <!-- Capture mode buttons (2-column grid) -->
        <ItemsControl Grid.Row="1" x:Name="CaptureModesPanel">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <UniformGrid Columns="2"/>
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
        </ItemsControl>

        <!-- Recent Captures -->
        <TextBlock Grid.Row="2" Text="최근 캡쳐"
                   Foreground="{DynamicResource TextSecondaryBrush}"
                   FontSize="{DynamicResource FontSizeSmall}"
                   Margin="0,16,0,8"/>

        <!-- Footer -->
        <DockPanel Grid.Row="3" Margin="0,8,0,0">
            <TextBlock Text="v1.0.0"
                       Foreground="{DynamicResource TextDisabledBrush}"
                       FontSize="{DynamicResource FontSizeSmall}"
                       VerticalAlignment="Center"/>
            <Button Content="⚙ 설정" HorizontalAlignment="Right"
                    Padding="12,4" Click="OnSettingsClick"/>
        </DockPanel>
    </Grid>
</Window>
```

- [ ] **Step 3: Create MainWindow.xaml.cs (tray icon + hotkey integration)**

Create `src/ShinCapture/Views/MainWindow.xaml.cs`:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using ShinCapture.Models;
using ShinCapture.Services;

namespace ShinCapture.Views;

public partial class MainWindow : Window
{
    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyManager _hotkeyManager;
    private readonly SettingsManager _settingsManager;
    private AppSettings _settings;

    public MainWindow(SettingsManager settingsManager)
    {
        InitializeComponent();
        _settingsManager = settingsManager;
        _settings = settingsManager.Load();
        _hotkeyManager = new HotkeyManager();

        _trayIcon = new NotifyIcon
        {
            Icon = new Icon(System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/icon.ico"))!.Stream),
            Text = "신캡쳐",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _trayIcon.DoubleClick += (_, _) => ToggleMainWindow();

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hotkeyManager.Initialize(this);
        RegisterHotkeys();
    }

    private void RegisterHotkeys()
    {
        _hotkeyManager.UnregisterAll();
        _hotkeyManager.Register(_settings.Hotkeys.RegionCapture, () => StartCapture(CaptureMode.Region));
        _hotkeyManager.Register(_settings.Hotkeys.FreeformCapture, () => StartCapture(CaptureMode.Freeform));
        _hotkeyManager.Register(_settings.Hotkeys.WindowCapture, () => StartCapture(CaptureMode.Window));
        _hotkeyManager.Register(_settings.Hotkeys.ElementCapture, () => StartCapture(CaptureMode.Element));
        _hotkeyManager.Register(_settings.Hotkeys.FullscreenCapture, () => StartCapture(CaptureMode.Fullscreen));
        _hotkeyManager.Register(_settings.Hotkeys.ScrollCapture, () => StartCapture(CaptureMode.Scroll));
        _hotkeyManager.Register(_settings.Hotkeys.FixedSizeCapture, () => StartCapture(CaptureMode.FixedSize));
    }

    private void StartCapture(CaptureMode mode)
    {
        // Will be implemented in Task 6-7
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("✏ 영역지정 캡쳐\tPrtSc", null, (_, _) => StartCapture(CaptureMode.Region));
        menu.Items.Add("✧ 자유형 캡쳐\tCtrl+Shift+F", null, (_, _) => StartCapture(CaptureMode.Freeform));
        menu.Items.Add("☐ 창 캡쳐\tCtrl+Shift+W", null, (_, _) => StartCapture(CaptureMode.Window));
        menu.Items.Add("◫ 단위영역 캡쳐\tCtrl+Shift+D", null, (_, _) => StartCapture(CaptureMode.Element));
        menu.Items.Add("⊡ 전체화면 캡쳐\tCtrl+Shift+A", null, (_, _) => StartCapture(CaptureMode.Fullscreen));
        menu.Items.Add("↕ 스크롤 캡쳐\tCtrl+Shift+S", null, (_, _) => StartCapture(CaptureMode.Scroll));
        menu.Items.Add("⊞ 지정사이즈 캡쳐\tCtrl+Shift+Z", null, (_, _) => StartCapture(CaptureMode.FixedSize));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("📂 최근 캡쳐", null, (_, _) => { });
        menu.Items.Add("📁 저장 폴더 열기", null, (_, _) => OpenSaveFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("⚙ 환경설정", null, (_, _) => OpenSettings());
        menu.Items.Add("ℹ 신캡쳐 정보", null, (_, _) => { });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("✕ 종료", null, (_, _) => ExitApplication());

        return menu;
    }

    private void ToggleMainWindow()
    {
        if (Visibility == Visibility.Visible)
        {
            Hide();
        }
        else
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_settings.General.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void OpenSaveFolder()
    {
        var path = _settings.Save.AutoSavePath;
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private void OpenSettings() { /* Task 25 */ }
    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettings();

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _hotkeyManager.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}
```

- [ ] **Step 4: Update App.xaml.cs to launch MainWindow**

Edit `src/ShinCapture/App.xaml.cs`:

```csharp
using System.Windows;
using ShinCapture.Services;
using ShinCapture.Views;

namespace ShinCapture;

public partial class App : Application
{
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsManager = new SettingsManager();
        _mainWindow = new MainWindow(settingsManager);
        // Window starts hidden (Visibility="Hidden" in XAML), tray icon is visible
    }
}
```

- [ ] **Step 5: Add Windows Forms reference for NotifyIcon**

Edit `src/ShinCapture/ShinCapture.csproj` to add UseWindowsForms:

```xml
<PropertyGroup>
    ...
    <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>
```

- [ ] **Step 6: Build and run to verify tray icon appears**

```bash
cd "C:/AI/신캡쳐" && dotnet build && dotnet run --project src/ShinCapture
```

Expected: App starts with tray icon visible, no main window. Right-click tray → context menu appears. Double-click tray → main window toggles. Click "종료" → app exits.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add HotkeyManager, TrayManager with context menu, MainWindow shell"
```

---

### Task 6: Capture Overlay (Base)

**Files:**
- Create: `src/ShinCapture/Views/Overlay/CaptureOverlay.xaml`
- Create: `src/ShinCapture/Views/Overlay/CaptureOverlay.xaml.cs`
- Create: `src/ShinCapture/Capture/ICaptureMode.cs`
- Create: `src/ShinCapture/Models/CaptureResult.cs`

- [ ] **Step 1: Create CaptureResult model**

Create `src/ShinCapture/Models/CaptureResult.cs`:

```csharp
using System.Drawing;

namespace ShinCapture.Models;

public class CaptureResult
{
    public required Bitmap Image { get; init; }
    public Rectangle Region { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
```

- [ ] **Step 2: Create ICaptureMode interface**

Create `src/ShinCapture/Capture/ICaptureMode.cs`:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Input;

namespace ShinCapture.Capture;

public interface ICaptureMode
{
    /// <summary>Called when overlay is shown, with the frozen screen bitmap.</summary>
    void Initialize(Bitmap screenBitmap, FrameworkElement overlay);

    /// <summary>Handle mouse events on the overlay.</summary>
    void OnMouseDown(MouseButtonEventArgs e);
    void OnMouseMove(MouseEventArgs e);
    void OnMouseUp(MouseButtonEventArgs e);

    /// <summary>Render mode-specific visuals on the overlay.</summary>
    void Render(DrawingContext dc, double overlayWidth, double overlayHeight);

    /// <summary>Returns the selected region when capture is confirmed. Null if cancelled.</summary>
    Rectangle? GetSelectedRegion();

    /// <summary>True when the user has completed selection.</summary>
    bool IsComplete { get; }

    /// <summary>True when the user has cancelled.</summary>
    bool IsCancelled { get; }
}
```

- [ ] **Step 3: Create CaptureOverlay.xaml**

Create `src/ShinCapture/Views/Overlay/CaptureOverlay.xaml`:

```xml
<Window x:Class="ShinCapture.Views.Overlay.CaptureOverlay"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None" AllowsTransparency="True"
        Background="Transparent" Topmost="True"
        ShowInTaskbar="False" ResizeMode="NoResize"
        Cursor="Cross" WindowState="Normal">

    <Grid x:Name="RootGrid">
        <!-- Frozen screen capture as background -->
        <Image x:Name="BackgroundImage" Stretch="None"/>

        <!-- Semi-transparent dark overlay -->
        <Rectangle x:Name="DimOverlay" Fill="#55000000"/>

        <!-- Custom rendering surface for selection, magnifier, etc. -->
        <Canvas x:Name="RenderCanvas" ClipToBounds="True"/>

        <!-- Magnifier panel (bottom-right of cursor) -->
        <Border x:Name="MagnifierPanel"
                Width="200" Height="200"
                BorderBrush="#888" BorderThickness="1"
                Background="White" CornerRadius="4"
                HorizontalAlignment="Left" VerticalAlignment="Top"
                Visibility="Visible" IsHitTestVisible="False">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                <Image x:Name="MagnifierImage" Grid.Row="0" Stretch="None"
                       RenderOptions.BitmapScalingMode="NearestNeighbor"/>
                <Border Grid.Row="1" Background="#F0F0F0" Padding="4">
                    <StackPanel>
                        <TextBlock x:Name="MagnifierCoords" FontSize="10"
                                   FontFamily="Segoe UI" Foreground="#333"
                                   Text="X: 0, Y: 0"/>
                        <TextBlock x:Name="MagnifierColor" FontSize="10"
                                   FontFamily="Segoe UI" Foreground="#333"
                                   Text="#000000"/>
                        <TextBlock x:Name="MagnifierSize" FontSize="10"
                                   FontFamily="Segoe UI" Foreground="#333"
                                   Text="0 × 0"/>
                    </StackPanel>
                </Border>
            </Grid>
        </Border>

        <!-- Crosshair lines -->
        <Line x:Name="CrosshairH" Stroke="#40FFFFFF" StrokeThickness="1"
              IsHitTestVisible="False"/>
        <Line x:Name="CrosshairV" Stroke="#40FFFFFF" StrokeThickness="1"
              IsHitTestVisible="False"/>
    </Grid>
</Window>
```

- [ ] **Step 4: Create CaptureOverlay.xaml.cs**

Create `src/ShinCapture/Views/Overlay/CaptureOverlay.xaml.cs`:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ShinCapture.Capture;
using ShinCapture.Helpers;
using ShinCapture.Models;

namespace ShinCapture.Views.Overlay;

public partial class CaptureOverlay : Window
{
    private Bitmap? _screenBitmap;
    private BitmapSource? _screenSource;
    private ICaptureMode? _captureMode;
    private readonly CaptureSettings _captureSettings;

    public CaptureResult? Result { get; private set; }

    public CaptureOverlay(CaptureSettings captureSettings)
    {
        InitializeComponent();
        _captureSettings = captureSettings;

        KeyDown += OnKeyDown;
        MouseDown += OnOverlayMouseDown;
        MouseMove += OnOverlayMouseMove;
        MouseUp += OnOverlayMouseUp;
    }

    public void Start(ICaptureMode mode)
    {
        _captureMode = mode;

        // Capture full screen before showing overlay
        _screenBitmap = ScreenHelper.CaptureFullScreen();
        _screenSource = BitmapHelper.ToBitmapSource(_screenBitmap);
        BackgroundImage.Source = _screenSource;

        // Position overlay to cover all monitors
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        _captureMode.Initialize(_screenBitmap, RenderCanvas);

        Show();
        Activate();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Result = null;
            Close();
        }
    }

    private void OnOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        _captureMode?.OnMouseDown(e);
    }

    private void OnOverlayMouseMove(object sender, MouseEventArgs e)
    {
        _captureMode?.OnMouseMove(e);
        UpdateMagnifier(e);
        UpdateCrosshair(e);
    }

    private void OnOverlayMouseUp(object sender, MouseButtonEventArgs e)
    {
        _captureMode?.OnMouseUp(e);

        if (_captureMode?.IsComplete == true)
        {
            var region = _captureMode.GetSelectedRegion();
            if (region.HasValue && _screenBitmap != null)
            {
                var cropped = ScreenHelper.CropBitmap(_screenBitmap, region.Value);
                Result = new CaptureResult
                {
                    Image = cropped,
                    Region = region.Value
                };
            }
            Close();
        }
        else if (_captureMode?.IsCancelled == true)
        {
            Result = null;
            Close();
        }
    }

    private void UpdateMagnifier(MouseEventArgs e)
    {
        if (_screenBitmap == null || !_captureSettings.ShowColorCode) return;

        var pos = e.GetPosition(this);
        var x = (int)pos.X;
        var y = (int)pos.Y;

        // Position magnifier near cursor (offset to bottom-right)
        var magLeft = pos.X + 20;
        var magTop = pos.Y + 20;
        if (magLeft + 200 > Width) magLeft = pos.X - 220;
        if (magTop + 200 > Height) magTop = pos.Y - 220;

        MagnifierPanel.Margin = new Thickness(magLeft, magTop, 0, 0);

        // Update color info
        var screenX = x + (int)SystemParameters.VirtualScreenLeft;
        var screenY = y + (int)SystemParameters.VirtualScreenTop;
        if (screenX >= 0 && screenX < _screenBitmap.Width && screenY >= 0 && screenY < _screenBitmap.Height)
        {
            var color = _screenBitmap.GetPixel(screenX, screenY);
            MagnifierColor.Text = ScreenHelper.ColorToHex(color);
        }
        MagnifierCoords.Text = $"X: {screenX}, Y: {screenY}";

        // Update zoom preview
        var zoom = _captureSettings.MagnifierZoom;
        var srcSize = (int)(200.0 / zoom);
        var srcRect = new Rectangle(
            Math.Max(0, screenX - srcSize / 2),
            Math.Max(0, screenY - srcSize / 2),
            Math.Min(srcSize, _screenBitmap.Width),
            Math.Min(srcSize, _screenBitmap.Height));

        if (srcRect.Width > 0 && srcRect.Height > 0)
        {
            using var zoomed = ScreenHelper.CropBitmap(_screenBitmap, srcRect);
            MagnifierImage.Source = BitmapHelper.ToBitmapSource(zoomed);
        }
    }

    private void UpdateCrosshair(MouseEventArgs e)
    {
        if (!_captureSettings.ShowCrosshair) return;

        var pos = e.GetPosition(this);
        CrosshairH.X1 = 0; CrosshairH.X2 = Width;
        CrosshairH.Y1 = pos.Y; CrosshairH.Y2 = pos.Y;
        CrosshairV.X1 = pos.X; CrosshairV.X2 = pos.X;
        CrosshairV.Y1 = 0; CrosshairV.Y2 = Height;
    }

    protected override void OnClosed(EventArgs e)
    {
        _screenBitmap?.Dispose();
        base.OnClosed(e);
    }
}
```

- [ ] **Step 5: Build and verify**

```bash
cd "C:/AI/신캡쳐" && dotnet build
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add CaptureOverlay with magnifier, crosshair, ICaptureMode interface"
```

---

### Task 7: Region Capture + Fullscreen Capture Modes

**Files:**
- Create: `src/ShinCapture/Capture/RegionCaptureMode.cs`
- Create: `src/ShinCapture/Capture/FullscreenCaptureMode.cs`
- Modify: `src/ShinCapture/Views/MainWindow.xaml.cs` (wire up StartCapture)

- [ ] **Step 1: Create RegionCaptureMode**

Create `src/ShinCapture/Capture/RegionCaptureMode.cs`:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ShinCapture.Capture;

public class RegionCaptureMode : ICaptureMode
{
    private System.Windows.Point _startPoint;
    private System.Windows.Point _currentPoint;
    private bool _isDragging;
    private Canvas? _canvas;
    private System.Windows.Shapes.Rectangle? _selectionRect;
    private System.Windows.Shapes.Rectangle? _dimTop, _dimBottom, _dimLeft, _dimRight;

    public bool IsComplete { get; private set; }
    public bool IsCancelled { get; private set; }

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _canvas = overlay as Canvas;
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _startPoint = e.GetPosition(_canvas);
            _isDragging = true;

            // Create selection rectangle
            _selectionRect = new System.Windows.Shapes.Rectangle
            {
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                Fill = Brushes.Transparent
            };
            _canvas?.Children.Add(_selectionRect);

            // Create dim regions (to make selected area bright, rest dark)
            _dimTop = CreateDimRect();
            _dimBottom = CreateDimRect();
            _dimLeft = CreateDimRect();
            _dimRight = CreateDimRect();
            _canvas?.Children.Add(_dimTop);
            _canvas?.Children.Add(_dimBottom);
            _canvas?.Children.Add(_dimLeft);
            _canvas?.Children.Add(_dimRight);
        }
        else if (e.RightButton == MouseButtonState.Pressed)
        {
            IsCancelled = true;
        }
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        if (!_isDragging || _canvas == null || _selectionRect == null) return;

        _currentPoint = e.GetPosition(_canvas);
        var rect = GetSelectionRect();

        Canvas.SetLeft(_selectionRect, rect.X);
        Canvas.SetTop(_selectionRect, rect.Y);
        _selectionRect.Width = rect.Width;
        _selectionRect.Height = rect.Height;

        UpdateDimRegions(rect, _canvas.ActualWidth, _canvas.ActualHeight);
    }

    public void OnMouseUp(MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;

        var rect = GetSelectionRect();
        if (rect.Width > 5 && rect.Height > 5)
        {
            IsComplete = true;
        }
        else
        {
            // Too small — cancel
            _canvas?.Children.Clear();
        }
    }

    public void Render(DrawingContext dc, double overlayWidth, double overlayHeight) { }

    public Rectangle? GetSelectedRegion()
    {
        if (!IsComplete) return null;
        var rect = GetSelectionRect();
        var screenLeft = (int)SystemParameters.VirtualScreenLeft;
        var screenTop = (int)SystemParameters.VirtualScreenTop;
        return new Rectangle(
            (int)rect.X + screenLeft,
            (int)rect.Y + screenTop,
            (int)rect.Width,
            (int)rect.Height);
    }

    private Rect GetSelectionRect()
    {
        var x = Math.Min(_startPoint.X, _currentPoint.X);
        var y = Math.Min(_startPoint.Y, _currentPoint.Y);
        var w = Math.Abs(_currentPoint.X - _startPoint.X);
        var h = Math.Abs(_currentPoint.Y - _startPoint.Y);
        return new Rect(x, y, w, h);
    }

    private System.Windows.Shapes.Rectangle CreateDimRect()
    {
        return new System.Windows.Shapes.Rectangle
        {
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 0, 0, 0)),
            IsHitTestVisible = false
        };
    }

    private void UpdateDimRegions(Rect sel, double totalW, double totalH)
    {
        if (_dimTop == null) return;

        // Top strip
        Canvas.SetLeft(_dimTop, 0); Canvas.SetTop(_dimTop, 0);
        _dimTop.Width = totalW; _dimTop.Height = sel.Y;

        // Bottom strip
        Canvas.SetLeft(_dimBottom, 0); Canvas.SetTop(_dimBottom, sel.Y + sel.Height);
        _dimBottom.Width = totalW; _dimBottom.Height = totalH - sel.Y - sel.Height;

        // Left strip
        Canvas.SetLeft(_dimLeft, 0); Canvas.SetTop(_dimLeft, sel.Y);
        _dimLeft.Width = sel.X; _dimLeft.Height = sel.Height;

        // Right strip
        Canvas.SetLeft(_dimRight, sel.X + sel.Width); Canvas.SetTop(_dimRight, sel.Y);
        _dimRight.Width = totalW - sel.X - sel.Width; _dimRight.Height = sel.Height;
    }
}
```

- [ ] **Step 2: Create FullscreenCaptureMode**

Create `src/ShinCapture/Capture/FullscreenCaptureMode.cs`:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ShinCapture.Capture;

public class FullscreenCaptureMode : ICaptureMode
{
    private Bitmap? _screenBitmap;

    public bool IsComplete { get; private set; } = true;
    public bool IsCancelled { get; private set; }

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _screenBitmap = screenBitmap;
    }

    public void OnMouseDown(MouseButtonEventArgs e) { }
    public void OnMouseMove(MouseEventArgs e) { }
    public void OnMouseUp(MouseButtonEventArgs e) { }
    public void Render(DrawingContext dc, double overlayWidth, double overlayHeight) { }

    public Rectangle? GetSelectedRegion()
    {
        if (_screenBitmap == null) return null;
        return new Rectangle(0, 0, _screenBitmap.Width, _screenBitmap.Height);
    }
}
```

- [ ] **Step 3: Wire up StartCapture in MainWindow**

Add to `src/ShinCapture/Views/MainWindow.xaml.cs`, replace the empty `StartCapture` method:

```csharp
private void StartCapture(CaptureMode mode)
{
    ICaptureMode captureMode = mode switch
    {
        CaptureMode.Region => new RegionCaptureMode(),
        CaptureMode.Fullscreen => new FullscreenCaptureMode(),
        // Remaining modes will be added in Tasks 8-11
        _ => new RegionCaptureMode()
    };

    if (mode == CaptureMode.Fullscreen)
    {
        // Fullscreen: instant capture, no overlay needed
        var bitmap = ScreenHelper.CaptureFullScreen();
        var result = new CaptureResult
        {
            Image = bitmap,
            Region = new Rectangle(0, 0, bitmap.Width, bitmap.Height)
        };
        HandleCaptureResult(result);
        return;
    }

    var overlay = new CaptureOverlay(_settings.Capture);
    overlay.Closed += (_, _) =>
    {
        if (overlay.Result != null)
            HandleCaptureResult(overlay.Result);
    };
    overlay.Start(captureMode);
}

private void HandleCaptureResult(CaptureResult result)
{
    switch (_settings.Capture.AfterCapture)
    {
        case AfterCaptureAction.OpenEditor:
            // EditorWindow will be created in Task 12
            break;
        case AfterCaptureAction.SaveDirectly:
            // SaveManager will be used in Task 23
            break;
        case AfterCaptureAction.ClipboardOnly:
            System.Windows.Clipboard.SetImage(BitmapHelper.ToBitmapSource(result.Image));
            break;
    }
}
```

Add the using at the top:
```csharp
using ShinCapture.Capture;
using ShinCapture.Helpers;
```

- [ ] **Step 4: Build and test manually**

```bash
cd "C:/AI/신캡쳐" && dotnet build && dotnet run --project src/ShinCapture
```

Expected: Press PrintScreen → overlay appears with crosshair + magnifier → drag to select region → overlay closes. Press Ctrl+Shift+A → instant capture (no visible effect yet since editor isn't built).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add RegionCapture and FullscreenCapture modes with overlay integration"
```

---

### Task 8: Window Capture Mode

**Files:**
- Create: `src/ShinCapture/Capture/WindowCaptureMode.cs`
- Modify: `src/ShinCapture/Views/MainWindow.xaml.cs` (add case)

- [ ] **Step 1: Create WindowCaptureMode**

Create `src/ShinCapture/Capture/WindowCaptureMode.cs`:

```csharp
using System.Drawing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ShinCapture.Helpers;

namespace ShinCapture.Capture;

public class WindowCaptureMode : ICaptureMode
{
    private Canvas? _canvas;
    private System.Windows.Shapes.Rectangle? _highlight;
    private NativeMethods.RECT _currentWindowRect;

    public bool IsComplete { get; private set; }
    public bool IsCancelled { get; private set; }

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _canvas = overlay as Canvas;
        _highlight = new System.Windows.Shapes.Rectangle
        {
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 0, 120, 212)),
            IsHitTestVisible = false
        };
        _canvas?.Children.Add(_highlight);
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            IsComplete = true;
        else if (e.RightButton == MouseButtonState.Pressed)
            IsCancelled = true;
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        if (_canvas == null || _highlight == null) return;

        var pos = e.GetPosition(_canvas);
        var screenX = (int)(pos.X + SystemParameters.VirtualScreenLeft);
        var screenY = (int)(pos.Y + SystemParameters.VirtualScreenTop);

        var point = new NativeMethods.POINT(screenX, screenY);
        var hwnd = NativeMethods.WindowFromPoint(point);
        hwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);

        if (hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            _currentWindowRect = rect;
            var offsetX = SystemParameters.VirtualScreenLeft;
            var offsetY = SystemParameters.VirtualScreenTop;

            Canvas.SetLeft(_highlight, rect.Left - offsetX);
            Canvas.SetTop(_highlight, rect.Top - offsetY);
            _highlight.Width = Math.Max(0, rect.Width);
            _highlight.Height = Math.Max(0, rect.Height);
            _highlight.Visibility = Visibility.Visible;
        }
    }

    public void OnMouseUp(MouseButtonEventArgs e) { }

    public void Render(DrawingContext dc, double overlayWidth, double overlayHeight) { }

    public System.Drawing.Rectangle? GetSelectedRegion()
    {
        if (!IsComplete) return null;
        return new System.Drawing.Rectangle(
            _currentWindowRect.Left,
            _currentWindowRect.Top,
            _currentWindowRect.Width,
            _currentWindowRect.Height);
    }
}
```

- [ ] **Step 2: Add WindowCapture case in MainWindow.StartCapture**

In `src/ShinCapture/Views/MainWindow.xaml.cs`, add to the switch:

```csharp
CaptureMode.Window => new WindowCaptureMode(),
```

- [ ] **Step 3: Build and test**

```bash
cd "C:/AI/신캡쳐" && dotnet build && dotnet run --project src/ShinCapture
```

Expected: Ctrl+Shift+W → overlay appears → moving mouse highlights different windows with blue border → click to capture.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add WindowCaptureMode with window detection and highlight"
```

---

### Task 9: Freeform Capture Mode

**Files:**
- Create: `src/ShinCapture/Capture/FreeformCaptureMode.cs`

- [ ] **Step 1: Create FreeformCaptureMode**

Create `src/ShinCapture/Capture/FreeformCaptureMode.cs`:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ShinCapture.Capture;

public class FreeformCaptureMode : ICaptureMode
{
    private Canvas? _canvas;
    private Polyline? _polyline;
    private readonly List<System.Windows.Point> _points = new();
    private bool _isDrawing;

    public bool IsComplete { get; private set; }
    public bool IsCancelled { get; private set; }

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _canvas = overlay as Canvas;
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDrawing = true;
            _points.Clear();
            var pos = e.GetPosition(_canvas);
            _points.Add(pos);

            _polyline = new Polyline
            {
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            _polyline.Points.Add(pos);
            _canvas?.Children.Add(_polyline);
        }
        else if (e.RightButton == MouseButtonState.Pressed)
        {
            IsCancelled = true;
        }
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        if (!_isDrawing || _polyline == null) return;

        var pos = e.GetPosition(_canvas);
        _points.Add(pos);
        _polyline.Points.Add(pos);
    }

    public void OnMouseUp(MouseButtonEventArgs e)
    {
        if (!_isDrawing) return;
        _isDrawing = false;

        if (_points.Count > 10)
            IsComplete = true;
    }

    public void Render(DrawingContext dc, double overlayWidth, double overlayHeight) { }

    public Rectangle? GetSelectedRegion()
    {
        if (!IsComplete || _points.Count == 0) return null;

        var minX = _points.Min(p => p.X);
        var minY = _points.Min(p => p.Y);
        var maxX = _points.Max(p => p.X);
        var maxY = _points.Max(p => p.Y);

        var screenLeft = (int)SystemParameters.VirtualScreenLeft;
        var screenTop = (int)SystemParameters.VirtualScreenTop;

        return new Rectangle(
            (int)minX + screenLeft,
            (int)minY + screenTop,
            (int)(maxX - minX),
            (int)(maxY - minY));
    }
}
```

- [ ] **Step 2: Add case in MainWindow and commit**

Add `CaptureMode.Freeform => new FreeformCaptureMode(),` to the switch.

```bash
cd "C:/AI/신캡쳐" && dotnet build
git add -A && git commit -m "feat: add FreeformCaptureMode with polygon selection"
```

---

### Task 10: Element Capture + Fixed Size Capture Modes

**Files:**
- Create: `src/ShinCapture/Capture/ElementCaptureMode.cs`
- Create: `src/ShinCapture/Capture/FixedSizeCaptureMode.cs`

- [ ] **Step 1: Create ElementCaptureMode**

Create `src/ShinCapture/Capture/ElementCaptureMode.cs`:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ShinCapture.Helpers;

namespace ShinCapture.Capture;

public class ElementCaptureMode : ICaptureMode
{
    private Canvas? _canvas;
    private System.Windows.Shapes.Rectangle? _highlight;
    private System.Windows.Rect _currentBounds;

    public bool IsComplete { get; private set; }
    public bool IsCancelled { get; private set; }

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _canvas = overlay as Canvas;
        _highlight = new System.Windows.Shapes.Rectangle
        {
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 0, 120, 212)),
            IsHitTestVisible = false
        };
        _canvas?.Children.Add(_highlight);
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            IsComplete = true;
        else if (e.RightButton == MouseButtonState.Pressed)
            IsCancelled = true;
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        if (_canvas == null || _highlight == null) return;

        var pos = e.GetPosition(_canvas);
        var screenX = pos.X + SystemParameters.VirtualScreenLeft;
        var screenY = pos.Y + SystemParameters.VirtualScreenTop;

        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(screenX, screenY));
            if (element != null)
            {
                _currentBounds = element.Current.BoundingRectangle;
                var offsetX = SystemParameters.VirtualScreenLeft;
                var offsetY = SystemParameters.VirtualScreenTop;

                Canvas.SetLeft(_highlight, _currentBounds.Left - offsetX);
                Canvas.SetTop(_highlight, _currentBounds.Top - offsetY);
                _highlight.Width = Math.Max(0, _currentBounds.Width);
                _highlight.Height = Math.Max(0, _currentBounds.Height);
                _highlight.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            _highlight.Visibility = Visibility.Hidden;
        }
    }

    public void OnMouseUp(MouseButtonEventArgs e) { }
    public void Render(DrawingContext dc, double overlayWidth, double overlayHeight) { }

    public Rectangle? GetSelectedRegion()
    {
        if (!IsComplete) return null;
        return new Rectangle(
            (int)_currentBounds.Left,
            (int)_currentBounds.Top,
            (int)_currentBounds.Width,
            (int)_currentBounds.Height);
    }
}
```

- [ ] **Step 2: Create FixedSizeCaptureMode**

Create `src/ShinCapture/Capture/FixedSizeCaptureMode.cs`:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ShinCapture.Capture;

public class FixedSizeCaptureMode : ICaptureMode
{
    private Canvas? _canvas;
    private System.Windows.Shapes.Rectangle? _frame;
    private readonly int _width;
    private readonly int _height;
    private System.Windows.Point _currentPos;

    public bool IsComplete { get; private set; }
    public bool IsCancelled { get; private set; }

    public FixedSizeCaptureMode(int width, int height)
    {
        _width = width;
        _height = height;
    }

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _canvas = overlay as Canvas;
        _frame = new System.Windows.Shapes.Rectangle
        {
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 4 },
            Fill = Brushes.Transparent,
            Width = _width,
            Height = _height,
            IsHitTestVisible = false
        };
        _canvas?.Children.Add(_frame);
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            IsComplete = true;
        else if (e.RightButton == MouseButtonState.Pressed)
            IsCancelled = true;
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        if (_canvas == null || _frame == null) return;

        _currentPos = e.GetPosition(_canvas);
        Canvas.SetLeft(_frame, _currentPos.X - _width / 2.0);
        Canvas.SetTop(_frame, _currentPos.Y - _height / 2.0);
    }

    public void OnMouseUp(MouseButtonEventArgs e) { }
    public void Render(DrawingContext dc, double overlayWidth, double overlayHeight) { }

    public Rectangle? GetSelectedRegion()
    {
        if (!IsComplete) return null;
        var screenLeft = (int)SystemParameters.VirtualScreenLeft;
        var screenTop = (int)SystemParameters.VirtualScreenTop;
        return new Rectangle(
            (int)(_currentPos.X - _width / 2.0) + screenLeft,
            (int)(_currentPos.Y - _height / 2.0) + screenTop,
            _width, _height);
    }
}
```

- [ ] **Step 3: Add cases in MainWindow and add UIAutomationClient reference**

Add to `src/ShinCapture/ShinCapture.csproj`:
```xml
<ItemGroup>
    <Reference Include="UIAutomationClient" />
    <Reference Include="UIAutomationTypes" />
</ItemGroup>
```

Add to the switch in MainWindow:
```csharp
CaptureMode.Element => new ElementCaptureMode(),
CaptureMode.FixedSize => new FixedSizeCaptureMode(
    _settings.FixedSizes.FirstOrDefault()?.Width ?? 1280,
    _settings.FixedSizes.FirstOrDefault()?.Height ?? 720),
```

- [ ] **Step 4: Build and commit**

```bash
cd "C:/AI/신캡쳐" && dotnet build
git add -A && git commit -m "feat: add ElementCapture and FixedSizeCapture modes"
```

---

### Task 11: Scroll Capture Mode

**Files:**
- Create: `src/ShinCapture/Capture/ScrollCaptureMode.cs`

- [ ] **Step 1: Create ScrollCaptureMode**

Create `src/ShinCapture/Capture/ScrollCaptureMode.cs`:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Helpers;

namespace ShinCapture.Capture;

/// <summary>
/// Scroll capture works in two phases:
/// 1. User selects a region (like RegionCapture)
/// 2. Auto-scroll the underlying window and stitch frames together
/// </summary>
public class ScrollCaptureMode : ICaptureMode
{
    private readonly RegionCaptureMode _regionMode = new();
    private Bitmap? _screenBitmap;
    private bool _regionSelected;
    private Bitmap? _stitchedResult;

    public bool IsComplete { get; private set; }
    public bool IsCancelled { get; private set; }

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _screenBitmap = screenBitmap;
        _regionMode.Initialize(screenBitmap, overlay);
    }

    public void OnMouseDown(MouseButtonEventArgs e) => _regionMode.OnMouseDown(e);
    public void OnMouseMove(MouseEventArgs e) => _regionMode.OnMouseMove(e);

    public void OnMouseUp(MouseButtonEventArgs e)
    {
        _regionMode.OnMouseUp(e);

        if (_regionMode.IsCancelled)
        {
            IsCancelled = true;
            return;
        }

        if (_regionMode.IsComplete && !_regionSelected)
        {
            _regionSelected = true;
            var region = _regionMode.GetSelectedRegion();
            if (region.HasValue)
            {
                _stitchedResult = CaptureWithScroll(region.Value);
                IsComplete = true;
            }
        }
    }

    public void Render(DrawingContext dc, double overlayWidth, double overlayHeight) { }

    public Rectangle? GetSelectedRegion()
    {
        // For scroll capture, the result is the stitched bitmap, not a screen region
        return _stitchedResult != null
            ? new Rectangle(0, 0, _stitchedResult.Width, _stitchedResult.Height)
            : null;
    }

    /// <summary>Get the stitched scroll capture result instead of using GetSelectedRegion + crop.</summary>
    public Bitmap? GetStitchedBitmap() => _stitchedResult;

    private Bitmap CaptureWithScroll(Rectangle region)
    {
        // Find the window under the selected region
        var centerX = region.Left + region.Width / 2;
        var centerY = region.Top + region.Height / 2;
        var point = new NativeMethods.POINT(centerX, centerY);
        var hwnd = NativeMethods.WindowFromPoint(point);
        hwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);

        var frames = new List<Bitmap>();
        var maxScrolls = 50; // Safety limit

        // Capture first frame from the already-captured screen
        if (_screenBitmap != null)
        {
            frames.Add(ScreenHelper.CropBitmap(_screenBitmap, region));
        }

        for (var i = 0; i < maxScrolls; i++)
        {
            // Send scroll down
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_VSCROLL,
                (IntPtr)NativeMethods.SB_PAGEDOWN, IntPtr.Zero);

            System.Threading.Thread.Sleep(200); // Wait for scroll animation

            // Capture new frame
            using var fullScreen = ScreenHelper.CaptureFullScreen();
            var frame = ScreenHelper.CropBitmap(fullScreen, region);

            // Check if we've reached the bottom (frame is identical to previous)
            if (frames.Count > 0 && AreBitmapsIdentical(frames[^1], frame))
            {
                frame.Dispose();
                break;
            }

            frames.Add(frame);
        }

        // Stitch frames vertically
        var result = StitchVertically(frames);

        // Dispose individual frames
        foreach (var f in frames) f.Dispose();

        return result;
    }

    private static bool AreBitmapsIdentical(Bitmap a, Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height) return false;

        // Sample comparison for speed (check every 10th row, every 10th pixel)
        for (var y = 0; y < a.Height; y += 10)
        for (var x = 0; x < a.Width; x += 10)
        {
            if (a.GetPixel(x, y) != b.GetPixel(x, y))
                return false;
        }
        return true;
    }

    private static Bitmap StitchVertically(List<Bitmap> frames)
    {
        if (frames.Count == 0)
            return new Bitmap(1, 1);

        if (frames.Count == 1)
            return (Bitmap)frames[0].Clone();

        var width = frames[0].Width;
        var totalHeight = frames.Sum(f => f.Height);
        var result = new Bitmap(width, totalHeight);

        using var g = Graphics.FromImage(result);
        var y = 0;
        foreach (var frame in frames)
        {
            g.DrawImage(frame, 0, y);
            y += frame.Height;
        }

        return result;
    }
}
```

- [ ] **Step 2: Update MainWindow.StartCapture for scroll mode**

Add the scroll capture case and special handling for the stitched result:

```csharp
CaptureMode.Scroll => new ScrollCaptureMode(),
```

Also update the overlay.Closed handler to handle scroll capture's stitched bitmap:

```csharp
overlay.Closed += (_, _) =>
{
    if (overlay.Result != null)
    {
        HandleCaptureResult(overlay.Result);
    }
    else if (captureMode is ScrollCaptureMode scrollMode && scrollMode.GetStitchedBitmap() is { } stitched)
    {
        HandleCaptureResult(new CaptureResult
        {
            Image = stitched,
            Region = new Rectangle(0, 0, stitched.Width, stitched.Height)
        });
    }
};
```

- [ ] **Step 3: Build and commit**

```bash
cd "C:/AI/신캡쳐" && dotnet build
git add -A && git commit -m "feat: add ScrollCaptureMode with auto-scroll and frame stitching"
```

---

## Phase 3: Editor Core (Tasks 12–16)

### Task 12: Editor Command System (Undo/Redo)

**Files:**
- Create: `src/ShinCapture/Editor/IEditorCommand.cs`
- Create: `src/ShinCapture/Editor/CommandStack.cs`
- Create: `tests/ShinCapture.Tests/Editor/CommandStackTests.cs`

- [ ] **Step 1: Write CommandStack tests**

Create `tests/ShinCapture.Tests/Editor/CommandStackTests.cs`:

```csharp
using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class CommandStackTests
{
    [Fact]
    public void Execute_AddsToUndoStack()
    {
        var stack = new CommandStack();
        var cmd = new TestCommand();

        stack.Execute(cmd);

        Assert.True(cmd.Executed);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Undo_ReversesLastCommand()
    {
        var stack = new CommandStack();
        var cmd = new TestCommand();
        stack.Execute(cmd);

        stack.Undo();

        Assert.True(cmd.Undone);
        Assert.False(stack.CanUndo);
        Assert.True(stack.CanRedo);
    }

    [Fact]
    public void Redo_ReExecutesCommand()
    {
        var stack = new CommandStack();
        var cmd = new TestCommand();
        stack.Execute(cmd);
        stack.Undo();

        stack.Redo();

        Assert.Equal(2, cmd.ExecuteCount);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Execute_AfterUndo_ClearsRedoStack()
    {
        var stack = new CommandStack();
        stack.Execute(new TestCommand());
        stack.Undo();

        stack.Execute(new TestCommand());

        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Clear_ResetsEverything()
    {
        var stack = new CommandStack();
        stack.Execute(new TestCommand());
        stack.Execute(new TestCommand());

        stack.Clear();

        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Changed_EventFires_OnExecuteUndoRedo()
    {
        var stack = new CommandStack();
        var count = 0;
        stack.Changed += (_, _) => count++;

        stack.Execute(new TestCommand());
        stack.Undo();
        stack.Redo();

        Assert.Equal(3, count);
    }

    private class TestCommand : IEditorCommand
    {
        public bool Executed { get; private set; }
        public bool Undone { get; private set; }
        public int ExecuteCount { get; private set; }

        public void Execute() { Executed = true; ExecuteCount++; }
        public void Undo() { Undone = true; }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd "C:/AI/신캡쳐" && dotnet test
```

Expected: FAIL.

- [ ] **Step 3: Create IEditorCommand**

Create `src/ShinCapture/Editor/IEditorCommand.cs`:

```csharp
namespace ShinCapture.Editor;

public interface IEditorCommand
{
    void Execute();
    void Undo();
}
```

- [ ] **Step 4: Create CommandStack**

Create `src/ShinCapture/Editor/CommandStack.cs`:

```csharp
namespace ShinCapture.Editor;

public class CommandStack
{
    private readonly Stack<IEditorCommand> _undoStack = new();
    private readonly Stack<IEditorCommand> _redoStack = new();

    public event EventHandler? Changed;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void Execute(IEditorCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var cmd = _undoStack.Pop();
        cmd.Undo();
        _redoStack.Push(cmd);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var cmd = _redoStack.Pop();
        cmd.Execute();
        _undoStack.Push(cmd);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
```

- [ ] **Step 5: Run tests**

```bash
cd "C:/AI/신캡쳐" && dotnet test --verbosity normal
```

Expected: All CommandStack tests PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add IEditorCommand and CommandStack with undo/redo"
```

---

### Task 13: Editor Objects (Base + Stroke + Shape)

**Files:**
- Create: `src/ShinCapture/Editor/EditorObject.cs`
- Create: `src/ShinCapture/Editor/Objects/StrokeObject.cs`
- Create: `src/ShinCapture/Editor/Objects/ShapeObject.cs`
- Create: `src/ShinCapture/Editor/Objects/ArrowObject.cs`
- Create: `src/ShinCapture/Editor/Objects/TextObject.cs`

- [ ] **Step 1: Create EditorObject base class**

Create `src/ShinCapture/Editor/EditorObject.cs`:

```csharp
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor;

public abstract class EditorObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public bool IsSelected { get; set; }
    public bool IsVisible { get; set; } = true;

    public abstract Rect Bounds { get; }
    public abstract void Render(DrawingContext dc);
    public abstract bool HitTest(Point point);
    public abstract EditorObject Clone();
}
```

- [ ] **Step 2: Create StrokeObject**

Create `src/ShinCapture/Editor/Objects/StrokeObject.cs`:

```csharp
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public class StrokeObject : EditorObject
{
    public List<Point> Points { get; set; } = new();
    public Color StrokeColor { get; set; } = Colors.Black;
    public double StrokeWidth { get; set; } = 3;
    public double Opacity { get; set; } = 1.0;
    public bool IsHighlighter { get; set; }

    public override Rect Bounds
    {
        get
        {
            if (Points.Count == 0) return Rect.Empty;
            var minX = Points.Min(p => p.X);
            var minY = Points.Min(p => p.Y);
            var maxX = Points.Max(p => p.X);
            var maxY = Points.Max(p => p.Y);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }

    public override void Render(DrawingContext dc)
    {
        if (Points.Count < 2) return;

        var pen = new Pen(new SolidColorBrush(StrokeColor) { Opacity = Opacity }, StrokeWidth)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(Points[0], false, false);
            for (var i = 1; i < Points.Count; i++)
                ctx.LineTo(Points[i], true, true);
        }
        geometry.Freeze();

        dc.DrawGeometry(null, pen, geometry);
    }

    public override bool HitTest(Point point)
    {
        return Points.Any(p => (p - point).Length < StrokeWidth + 5);
    }

    public override EditorObject Clone()
    {
        return new StrokeObject
        {
            Points = new List<Point>(Points),
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth,
            Opacity = Opacity,
            IsHighlighter = IsHighlighter
        };
    }
}
```

- [ ] **Step 3: Create ShapeObject**

Create `src/ShinCapture/Editor/Objects/ShapeObject.cs`:

```csharp
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public enum ShapeType { Rectangle, Ellipse, Line, DashedLine }
public enum FillMode { None, Solid, SemiTransparent }

public class ShapeObject : EditorObject
{
    public Point Start { get; set; }
    public Point End { get; set; }
    public ShapeType Shape { get; set; } = ShapeType.Rectangle;
    public Color StrokeColor { get; set; } = Colors.Red;
    public double StrokeWidth { get; set; } = 2;
    public FillMode Fill { get; set; } = FillMode.None;

    public override Rect Bounds => new(
        Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y),
        Math.Abs(End.X - Start.X), Math.Abs(End.Y - Start.Y));

    public override void Render(DrawingContext dc)
    {
        var pen = new Pen(new SolidColorBrush(StrokeColor), StrokeWidth);
        if (Shape == ShapeType.DashedLine)
            pen.DashStyle = DashStyles.Dash;

        Brush? fillBrush = Fill switch
        {
            FillMode.Solid => new SolidColorBrush(StrokeColor),
            FillMode.SemiTransparent => new SolidColorBrush(StrokeColor) { Opacity = 0.3 },
            _ => null
        };

        switch (Shape)
        {
            case ShapeType.Rectangle:
                dc.DrawRoundedRectangle(fillBrush, pen, Bounds, 0, 0);
                break;
            case ShapeType.Ellipse:
                dc.DrawEllipse(fillBrush, pen, Bounds.Center(), Bounds.Width / 2, Bounds.Height / 2);
                break;
            case ShapeType.Line:
            case ShapeType.DashedLine:
                dc.DrawLine(pen, Start, End);
                break;
        }
    }

    public override bool HitTest(Point point)
    {
        var expanded = Bounds;
        expanded.Inflate(5, 5);
        return expanded.Contains(point);
    }

    public override EditorObject Clone()
    {
        return new ShapeObject
        {
            Start = Start, End = End, Shape = Shape,
            StrokeColor = StrokeColor, StrokeWidth = StrokeWidth, Fill = Fill
        };
    }
}

public static class RectExtensions
{
    public static Point Center(this Rect rect) =>
        new(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
}
```

- [ ] **Step 4: Create ArrowObject**

Create `src/ShinCapture/Editor/Objects/ArrowObject.cs`:

```csharp
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public class ArrowObject : EditorObject
{
    public Point Start { get; set; }
    public Point End { get; set; }
    public Color StrokeColor { get; set; } = Colors.Red;
    public double StrokeWidth { get; set; } = 2;
    public double HeadSize { get; set; } = 12;

    public override Rect Bounds => new(
        Math.Min(Start.X, End.X) - HeadSize,
        Math.Min(Start.Y, End.Y) - HeadSize,
        Math.Abs(End.X - Start.X) + HeadSize * 2,
        Math.Abs(End.Y - Start.Y) + HeadSize * 2);

    public override void Render(DrawingContext dc)
    {
        var pen = new Pen(new SolidColorBrush(StrokeColor), StrokeWidth);
        dc.DrawLine(pen, Start, End);

        // Arrowhead
        var angle = Math.Atan2(End.Y - Start.Y, End.X - Start.X);
        var p1 = new Point(
            End.X - HeadSize * Math.Cos(angle - Math.PI / 6),
            End.Y - HeadSize * Math.Sin(angle - Math.PI / 6));
        var p2 = new Point(
            End.X - HeadSize * Math.Cos(angle + Math.PI / 6),
            End.Y - HeadSize * Math.Sin(angle + Math.PI / 6));

        var headGeometry = new StreamGeometry();
        using (var ctx = headGeometry.Open())
        {
            ctx.BeginFigure(End, true, true);
            ctx.LineTo(p1, true, false);
            ctx.LineTo(p2, true, false);
        }
        headGeometry.Freeze();
        dc.DrawGeometry(new SolidColorBrush(StrokeColor), pen, headGeometry);
    }

    public override bool HitTest(Point point)
    {
        var expanded = Bounds;
        expanded.Inflate(5, 5);
        return expanded.Contains(point);
    }

    public override EditorObject Clone()
    {
        return new ArrowObject
        {
            Start = Start, End = End,
            StrokeColor = StrokeColor, StrokeWidth = StrokeWidth, HeadSize = HeadSize
        };
    }
}
```

- [ ] **Step 5: Create TextObject**

Create `src/ShinCapture/Editor/Objects/TextObject.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public class TextObject : EditorObject
{
    public string Text { get; set; } = "";
    public Point Position { get; set; }
    public Color TextColor { get; set; } = Colors.Black;
    public string FontFamilyName { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 16;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }

    public override Rect Bounds
    {
        get
        {
            var ft = CreateFormattedText();
            return new Rect(Position, new Size(ft.Width, ft.Height));
        }
    }

    public override void Render(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;
        var ft = CreateFormattedText();
        dc.DrawText(ft, Position);
    }

    public override bool HitTest(Point point)
    {
        var b = Bounds;
        b.Inflate(5, 5);
        return b.Contains(point);
    }

    public override EditorObject Clone()
    {
        return new TextObject
        {
            Text = Text, Position = Position, TextColor = TextColor,
            FontFamilyName = FontFamilyName, FontSize = FontSize,
            IsBold = IsBold, IsItalic = IsItalic, IsUnderline = IsUnderline
        };
    }

    private FormattedText CreateFormattedText()
    {
        var typeface = new Typeface(
            new FontFamily(FontFamilyName),
            IsItalic ? FontStyles.Italic : FontStyles.Normal,
            IsBold ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal);

        var ft = new FormattedText(Text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, FontSize,
            new SolidColorBrush(TextColor), 1.0);

        if (IsUnderline)
            ft.SetTextDecorations(TextDecorations.Underline);

        return ft;
    }
}
```

- [ ] **Step 6: Build and commit**

```bash
cd "C:/AI/신캡쳐" && dotnet build
git add -A && git commit -m "feat: add EditorObject base + Stroke, Shape, Arrow, Text objects"
```

---

### Task 14: Editor Effect Objects (Mosaic, Blur, Number, Balloon, Image)

**Files:**
- Create: `src/ShinCapture/Editor/Objects/MosaicObject.cs`
- Create: `src/ShinCapture/Editor/Objects/BlurObject.cs`
- Create: `src/ShinCapture/Editor/Objects/NumberObject.cs`
- Create: `src/ShinCapture/Editor/Objects/BalloonObject.cs`
- Create: `src/ShinCapture/Editor/Objects/ImageObject.cs`

- [ ] **Step 1: Create MosaicObject**

Create `src/ShinCapture/Editor/Objects/MosaicObject.cs`:

```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor.Objects;

public enum MosaicSize { Small = 5, Medium = 10, Large = 20 }

public class MosaicObject : EditorObject
{
    public Rect Region { get; set; }
    public MosaicSize BlockSize { get; set; } = MosaicSize.Medium;
    public BitmapSource? SourceImage { get; set; }

    public override Rect Bounds => Region;

    public override void Render(DrawingContext dc)
    {
        if (SourceImage == null) return;

        var size = (int)BlockSize;
        var x0 = (int)Region.X;
        var y0 = (int)Region.Y;
        var w = (int)Region.Width;
        var h = (int)Region.Height;

        // Draw pixelated blocks
        for (var by = 0; by < h; by += size)
        for (var bx = 0; bx < w; bx += size)
        {
            var sampleX = Math.Min(x0 + bx + size / 2, (int)SourceImage.Width - 1);
            var sampleY = Math.Min(y0 + by + size / 2, (int)SourceImage.Height - 1);

            if (sampleX < 0 || sampleY < 0) continue;

            // Sample a pixel from the source
            var pixel = new byte[4];
            SourceImage.CopyPixels(new Int32Rect(sampleX, sampleY, 1, 1), pixel, 4, 0);
            var color = Color.FromRgb(pixel[2], pixel[1], pixel[0]);

            var blockRect = new Rect(x0 + bx, y0 + by,
                Math.Min(size, w - bx), Math.Min(size, h - by));
            dc.DrawRectangle(new SolidColorBrush(color), null, blockRect);
        }
    }

    public override bool HitTest(Point point) => Bounds.Contains(point);

    public override EditorObject Clone()
    {
        return new MosaicObject
        {
            Region = Region, BlockSize = BlockSize, SourceImage = SourceImage
        };
    }
}
```

- [ ] **Step 2: Create BlurObject**

Create `src/ShinCapture/Editor/Objects/BlurObject.cs`:

```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ShinCapture.Editor.Objects;

public enum BlurStrength { Light = 5, Medium = 15, Strong = 30 }

public class BlurObject : EditorObject
{
    public Rect Region { get; set; }
    public BlurStrength Strength { get; set; } = BlurStrength.Medium;

    public override Rect Bounds => Region;

    public override void Render(DrawingContext dc)
    {
        // Blur is applied as a semi-transparent overlay
        // Actual Gaussian blur requires rendering to a RenderTargetBitmap
        // For WPF, we render a frosted glass effect
        var brush = new SolidColorBrush(Color.FromArgb(180, 200, 200, 200));
        dc.DrawRectangle(brush, null, Region);
    }

    public override bool HitTest(Point point) => Bounds.Contains(point);

    public override EditorObject Clone()
    {
        return new BlurObject { Region = Region, Strength = Strength };
    }
}
```

- [ ] **Step 3: Create NumberObject**

Create `src/ShinCapture/Editor/Objects/NumberObject.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public class NumberObject : EditorObject
{
    public int Number { get; set; } = 1;
    public Point Center { get; set; }
    public Color CircleColor { get; set; } = Colors.Red;
    public double Radius { get; set; } = 14;

    public override Rect Bounds => new(
        Center.X - Radius, Center.Y - Radius, Radius * 2, Radius * 2);

    public override void Render(DrawingContext dc)
    {
        dc.DrawEllipse(new SolidColorBrush(CircleColor), null, Center, Radius, Radius);

        var ft = new FormattedText(Number.ToString(), CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                FontWeights.Bold, FontStretches.Normal),
            Radius, Brushes.White, 1.0);

        var textPos = new Point(Center.X - ft.Width / 2, Center.Y - ft.Height / 2);
        dc.DrawText(ft, textPos);
    }

    public override bool HitTest(Point point)
    {
        return (point - Center).Length <= Radius + 5;
    }

    public override EditorObject Clone()
    {
        return new NumberObject
        {
            Number = Number, Center = Center,
            CircleColor = CircleColor, Radius = Radius
        };
    }
}
```

- [ ] **Step 4: Create BalloonObject**

Create `src/ShinCapture/Editor/Objects/BalloonObject.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public enum BalloonStyle { Rounded, Square }

public class BalloonObject : EditorObject
{
    public string Text { get; set; } = "";
    public Point Position { get; set; }
    public Point TailTarget { get; set; }
    public Color FillColor { get; set; } = Colors.White;
    public Color BorderColor { get; set; } = Colors.Black;
    public BalloonStyle Style { get; set; } = BalloonStyle.Rounded;

    public override Rect Bounds
    {
        get
        {
            var ft = CreateFormattedText();
            var padding = 12.0;
            return new Rect(Position.X, Position.Y,
                ft.Width + padding * 2, ft.Height + padding * 2);
        }
    }

    public override void Render(DrawingContext dc)
    {
        var ft = CreateFormattedText();
        var padding = 12.0;
        var bodyRect = new Rect(Position.X, Position.Y,
            ft.Width + padding * 2, ft.Height + padding * 2);

        var pen = new Pen(new SolidColorBrush(BorderColor), 1.5);
        var fillBrush = new SolidColorBrush(FillColor);

        var cornerRadius = Style == BalloonStyle.Rounded ? 8.0 : 2.0;
        dc.DrawRoundedRectangle(fillBrush, pen, bodyRect, cornerRadius, cornerRadius);

        // Tail triangle
        var tailStart = new Point(bodyRect.Left + bodyRect.Width * 0.3, bodyRect.Bottom);
        var tailEnd = new Point(bodyRect.Left + bodyRect.Width * 0.5, bodyRect.Bottom);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(tailStart, true, true);
            ctx.LineTo(TailTarget, true, false);
            ctx.LineTo(tailEnd, true, false);
        }
        geometry.Freeze();
        dc.DrawGeometry(fillBrush, pen, geometry);

        // Text
        dc.DrawText(ft, new Point(Position.X + padding, Position.Y + padding));
    }

    public override bool HitTest(Point point)
    {
        var expanded = Bounds;
        expanded.Inflate(5, 5);
        return expanded.Contains(point);
    }

    public override EditorObject Clone()
    {
        return new BalloonObject
        {
            Text = Text, Position = Position, TailTarget = TailTarget,
            FillColor = FillColor, BorderColor = BorderColor, Style = Style
        };
    }

    private FormattedText CreateFormattedText()
    {
        return new FormattedText(Text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 14,
            new SolidColorBrush(BorderColor), 1.0);
    }
}
```

- [ ] **Step 5: Create ImageObject**

Create `src/ShinCapture/Editor/Objects/ImageObject.cs`:

```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor.Objects;

public class ImageObject : EditorObject
{
    public BitmapSource? Source { get; set; }
    public Point Position { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public override Rect Bounds => new(Position, new Size(Width, Height));

    public override void Render(DrawingContext dc)
    {
        if (Source == null) return;
        dc.DrawImage(Source, Bounds);
    }

    public override bool HitTest(Point point) => Bounds.Contains(point);

    public override EditorObject Clone()
    {
        return new ImageObject
        {
            Source = Source, Position = Position, Width = Width, Height = Height
        };
    }
}
```

- [ ] **Step 6: Build and commit**

```bash
cd "C:/AI/신캡쳐" && dotnet build
git add -A && git commit -m "feat: add Mosaic, Blur, Number, Balloon, Image editor objects"
```

---

### Task 15: Tool System (ITool + ToolBase + First Tools)

**Files:**
- Create: `src/ShinCapture/Editor/ITool.cs`
- Create: `src/ShinCapture/Editor/ToolBase.cs`
- Create: `src/ShinCapture/Editor/Tools/PenTool.cs`
- Create: `src/ShinCapture/Editor/Tools/HighlighterTool.cs`
- Create: `src/ShinCapture/Editor/Tools/ShapeTool.cs`
- Create: `src/ShinCapture/Editor/Tools/ArrowTool.cs`
- Create: `src/ShinCapture/Editor/Tools/TextTool.cs`

- [ ] **Step 1: Create ITool and ToolBase**

Create `src/ShinCapture/Editor/ITool.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ShinCapture.Editor;

public interface ITool
{
    string Name { get; }
    string Icon { get; }

    Color CurrentColor { get; set; }
    double CurrentWidth { get; set; }
    double CurrentOpacity { get; set; }

    void OnMouseDown(Point position, MouseButtonEventArgs e);
    void OnMouseMove(Point position, MouseEventArgs e);
    void OnMouseUp(Point position, MouseButtonEventArgs e);

    /// <summary>Returns the command to execute when the tool interaction is complete, or null.</summary>
    IEditorCommand? GetCommand();

    /// <summary>Render preview while user is interacting.</summary>
    void RenderPreview(DrawingContext dc);

    void Reset();
}
```

Create `src/ShinCapture/Editor/ToolBase.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ShinCapture.Editor;

public abstract class ToolBase : ITool
{
    public abstract string Name { get; }
    public abstract string Icon { get; }

    public Color CurrentColor { get; set; } = Colors.Red;
    public double CurrentWidth { get; set; } = 3;
    public double CurrentOpacity { get; set; } = 1.0;

    public abstract void OnMouseDown(Point position, MouseButtonEventArgs e);
    public abstract void OnMouseMove(Point position, MouseEventArgs e);
    public abstract void OnMouseUp(Point position, MouseButtonEventArgs e);
    public abstract IEditorCommand? GetCommand();
    public virtual void RenderPreview(DrawingContext dc) { }
    public virtual void Reset() { }
}
```

- [ ] **Step 2: Create AddObjectCommand**

Create `src/ShinCapture/Editor/AddObjectCommand.cs`:

```csharp
namespace ShinCapture.Editor;

public class AddObjectCommand : IEditorCommand
{
    private readonly List<EditorObject> _objects;
    private readonly EditorObject _object;

    public AddObjectCommand(List<EditorObject> objects, EditorObject obj)
    {
        _objects = objects;
        _object = obj;
    }

    public void Execute() => _objects.Add(_object);
    public void Undo() => _objects.Remove(_object);
}
```

- [ ] **Step 3: Create PenTool**

Create `src/ShinCapture/Editor/Tools/PenTool.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class PenTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private StrokeObject? _currentStroke;

    public override string Name => "펜";
    public override string Icon => "✏";

    public PenTool(List<EditorObject> objects)
    {
        _objects = objects;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _currentStroke = new StrokeObject
        {
            StrokeColor = CurrentColor,
            StrokeWidth = CurrentWidth,
            Opacity = CurrentOpacity,
            Points = { position }
        };
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        _currentStroke?.Points.Add(position);
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        _currentStroke?.Points.Add(position);
    }

    public override IEditorCommand? GetCommand()
    {
        if (_currentStroke == null || _currentStroke.Points.Count < 2) return null;
        var cmd = new AddObjectCommand(_objects, _currentStroke);
        _currentStroke = null;
        return cmd;
    }

    public override void RenderPreview(DrawingContext dc)
    {
        _currentStroke?.Render(dc);
    }

    public override void Reset() => _currentStroke = null;
}
```

- [ ] **Step 4: Create HighlighterTool**

Create `src/ShinCapture/Editor/Tools/HighlighterTool.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class HighlighterTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private StrokeObject? _currentStroke;

    public override string Name => "형광펜";
    public override string Icon => "🖍";

    public HighlighterTool(List<EditorObject> objects)
    {
        _objects = objects;
        CurrentWidth = 20;
        CurrentOpacity = 0.5;
        CurrentColor = Colors.Yellow;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _currentStroke = new StrokeObject
        {
            StrokeColor = CurrentColor,
            StrokeWidth = CurrentWidth,
            Opacity = CurrentOpacity,
            IsHighlighter = true,
            Points = { position }
        };
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        _currentStroke?.Points.Add(position);
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        _currentStroke?.Points.Add(position);
    }

    public override IEditorCommand? GetCommand()
    {
        if (_currentStroke == null || _currentStroke.Points.Count < 2) return null;
        var cmd = new AddObjectCommand(_objects, _currentStroke);
        _currentStroke = null;
        return cmd;
    }

    public override void RenderPreview(DrawingContext dc) => _currentStroke?.Render(dc);
    public override void Reset() => _currentStroke = null;
}
```

- [ ] **Step 5: Create ShapeTool, ArrowTool, TextTool**

Create `src/ShinCapture/Editor/Tools/ShapeTool.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class ShapeTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private ShapeObject? _current;

    public override string Name => "도형";
    public override string Icon => "⃞";

    public ShapeType SelectedShape { get; set; } = ShapeType.Rectangle;
    public FillMode SelectedFill { get; set; } = FillMode.None;

    public ShapeTool(List<EditorObject> objects) { _objects = objects; }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _current = new ShapeObject
        {
            Start = position, End = position,
            Shape = SelectedShape, StrokeColor = CurrentColor,
            StrokeWidth = CurrentWidth, Fill = SelectedFill
        };
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        if (_current != null) _current.End = position;
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        if (_current != null) _current.End = position;
    }

    public override IEditorCommand? GetCommand()
    {
        if (_current == null || _current.Bounds.Width < 3) return null;
        var cmd = new AddObjectCommand(_objects, _current);
        _current = null;
        return cmd;
    }

    public override void RenderPreview(DrawingContext dc) => _current?.Render(dc);
    public override void Reset() => _current = null;
}
```

Create `src/ShinCapture/Editor/Tools/ArrowTool.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class ArrowTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private ArrowObject? _current;

    public override string Name => "화살표";
    public override string Icon => "↗";

    public ArrowTool(List<EditorObject> objects) { _objects = objects; }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _current = new ArrowObject
        {
            Start = position, End = position,
            StrokeColor = CurrentColor, StrokeWidth = CurrentWidth
        };
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        if (_current != null) _current.End = position;
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        if (_current != null) _current.End = position;
    }

    public override IEditorCommand? GetCommand()
    {
        if (_current == null) return null;
        var len = (_current.End - _current.Start).Length;
        if (len < 5) return null;
        var cmd = new AddObjectCommand(_objects, _current);
        _current = null;
        return cmd;
    }

    public override void RenderPreview(DrawingContext dc) => _current?.Render(dc);
    public override void Reset() => _current = null;
}
```

Create `src/ShinCapture/Editor/Tools/TextTool.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class TextTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private readonly Canvas _canvas;
    private TextBox? _inputBox;
    private Point _position;

    public override string Name => "텍스트";
    public override string Icon => "T";
    public string FontFamilyName { get; set; } = "Segoe UI";
    public double FontSizeValue { get; set; } = 16;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }

    public TextTool(List<EditorObject> objects, Canvas canvas)
    {
        _objects = objects;
        _canvas = canvas;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        if (_inputBox != null) FinalizeText();

        _position = position;
        _inputBox = new TextBox
        {
            FontSize = FontSizeValue,
            FontFamily = new FontFamily(FontFamilyName),
            FontWeight = IsBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = IsItalic ? FontStyles.Italic : FontStyles.Normal,
            Foreground = new SolidColorBrush(CurrentColor),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.Gray),
            MinWidth = 50,
            AcceptsReturn = true
        };
        _inputBox.LostFocus += (_, _) => FinalizeText();
        _inputBox.KeyDown += (_, ke) => { if (ke.Key == Key.Escape) FinalizeText(); };

        Canvas.SetLeft(_inputBox, position.X);
        Canvas.SetTop(_inputBox, position.Y);
        _canvas.Children.Add(_inputBox);
        _inputBox.Focus();
    }

    public override void OnMouseMove(Point position, MouseEventArgs e) { }
    public override void OnMouseUp(Point position, MouseButtonEventArgs e) { }

    public override IEditorCommand? GetCommand() => null;

    private void FinalizeText()
    {
        if (_inputBox == null || string.IsNullOrWhiteSpace(_inputBox.Text)) return;

        var textObj = new TextObject
        {
            Text = _inputBox.Text,
            Position = _position,
            TextColor = CurrentColor,
            FontFamilyName = FontFamilyName,
            FontSize = FontSizeValue,
            IsBold = IsBold,
            IsItalic = IsItalic
        };

        var cmd = new AddObjectCommand(_objects, textObj);
        // TextTool needs direct access to CommandStack — this will be wired in EditorWindow
        _canvas.Children.Remove(_inputBox);
        _inputBox = null;

        // Store for retrieval
        _pendingCommand = cmd;
    }

    private IEditorCommand? _pendingCommand;

    public IEditorCommand? TakePendingCommand()
    {
        var cmd = _pendingCommand;
        _pendingCommand = null;
        return cmd;
    }

    public override void Reset()
    {
        if (_inputBox != null)
        {
            _canvas.Children.Remove(_inputBox);
            _inputBox = null;
        }
    }
}
```

- [ ] **Step 6: Build and commit**

```bash
cd "C:/AI/신캡쳐" && dotnet build
git add -A && git commit -m "feat: add ITool system with Pen, Highlighter, Shape, Arrow, Text tools"
```

---

### Task 16: Remaining Tools (Mosaic, Blur, Number, Balloon, Crop, Eraser, Eyedropper, ImageInsert)

**Files:**
- Create: `src/ShinCapture/Editor/Tools/MosaicTool.cs`
- Create: `src/ShinCapture/Editor/Tools/BlurTool.cs`
- Create: `src/ShinCapture/Editor/Tools/NumberTool.cs`
- Create: `src/ShinCapture/Editor/Tools/BalloonTool.cs`
- Create: `src/ShinCapture/Editor/Tools/CropTool.cs`
- Create: `src/ShinCapture/Editor/Tools/EraserTool.cs`
- Create: `src/ShinCapture/Editor/Tools/EyedropperTool.cs`
- Create: `src/ShinCapture/Editor/Tools/ImageInsertTool.cs`
- Create: `src/ShinCapture/Editor/RemoveObjectCommand.cs`
- Create: `src/ShinCapture/Editor/CropCommand.cs`

These tools follow the same pattern as Task 15. Each tool creates its corresponding EditorObject on mouse interaction and returns an AddObjectCommand. Special tools:

- **EraserTool**: hit-tests objects and returns a `RemoveObjectCommand`
- **EyedropperTool**: samples pixel color and raises `ColorPicked` event
- **CropTool**: drag rectangle → returns `CropCommand` that replaces the source image
- **ImageInsertTool**: opens file dialog → creates `ImageObject`

- [ ] **Step 1: Create all 8 tool files + RemoveObjectCommand + CropCommand**

(Each follows the same ToolBase pattern. The code for each tool mirrors Task 15's pattern — mousedown starts, mousemove updates, mouseup finalizes, getcommand returns the AddObjectCommand. Specific implementations vary per object type.)

Create `src/ShinCapture/Editor/RemoveObjectCommand.cs`:

```csharp
namespace ShinCapture.Editor;

public class RemoveObjectCommand : IEditorCommand
{
    private readonly List<EditorObject> _objects;
    private readonly EditorObject _target;
    private int _index;

    public RemoveObjectCommand(List<EditorObject> objects, EditorObject target)
    {
        _objects = objects;
        _target = target;
    }

    public void Execute()
    {
        _index = _objects.IndexOf(_target);
        _objects.Remove(_target);
    }

    public void Undo()
    {
        if (_index >= 0 && _index <= _objects.Count)
            _objects.Insert(_index, _target);
        else
            _objects.Add(_target);
    }
}
```

Create `src/ShinCapture/Editor/CropCommand.cs`:

```csharp
using System.Windows;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor;

public class CropCommand : IEditorCommand
{
    private readonly Action<BitmapSource> _setImage;
    private readonly BitmapSource _originalImage;
    private readonly BitmapSource _croppedImage;

    public CropCommand(BitmapSource original, Int32Rect cropRect, Action<BitmapSource> setImage)
    {
        _setImage = setImage;
        _originalImage = original;
        _croppedImage = new CroppedBitmap(original, cropRect);
        _croppedImage.Freeze();
    }

    public void Execute() => _setImage(_croppedImage);
    public void Undo() => _setImage(_originalImage);
}
```

Create the remaining 8 tool files following the established pattern. (MosaicTool drags rect → MosaicObject, BlurTool drags rect → BlurObject, NumberTool clicks → NumberObject with auto-incrementing counter, BalloonTool drags body then tail → BalloonObject, CropTool drags → CropCommand, EraserTool clicks on object → RemoveObjectCommand, EyedropperTool clicks → reads pixel → fires event, ImageInsertTool opens dialog → ImageObject.)

- [ ] **Step 2: Build and commit**

```bash
cd "C:/AI/신캡쳐" && dotnet build
git add -A && git commit -m "feat: add remaining 8 editor tools + RemoveObjectCommand + CropCommand"
```

---

## Phase 4: Editor Window (Tasks 17–19)

### Task 17: EditorCanvas (Custom Canvas with Zoom/Pan)

**Files:**
- Create: `src/ShinCapture/Editor/EditorCanvas.cs`

- [ ] **Step 1: Create EditorCanvas**

Create `src/ShinCapture/Editor/EditorCanvas.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor;

public class EditorCanvas : Canvas
{
    private BitmapSource? _backgroundImage;
    private readonly List<EditorObject> _objects = new();
    private ITool? _currentTool;
    private bool _isInteracting;

    // Zoom & Pan
    private double _zoom = 1.0;
    private Vector _pan;
    private Point _panStart;
    private bool _isPanning;

    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 0.25, 4.0);
            InvalidateVisual();
            ZoomChanged?.Invoke(this, _zoom);
        }
    }

    public event EventHandler<double>? ZoomChanged;

    public BitmapSource? BackgroundImage
    {
        get => _backgroundImage;
        set { _backgroundImage = value; FitToView(); InvalidateVisual(); }
    }

    public List<EditorObject> Objects => _objects;

    public void SetTool(ITool? tool) => _currentTool = tool;

    public void FitToView()
    {
        if (_backgroundImage == null || ActualWidth == 0) return;
        var zoomW = ActualWidth / _backgroundImage.PixelWidth;
        var zoomH = ActualHeight / _backgroundImage.PixelHeight;
        _zoom = Math.Min(zoomW, zoomH) * 0.9;
        _pan = new Vector(
            (ActualWidth - _backgroundImage.PixelWidth * _zoom) / 2,
            (ActualHeight - _backgroundImage.PixelHeight * _zoom) / 2);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // Canvas background
        dc.DrawRectangle(
            (Brush)FindResource("BackgroundCanvasBrush"),
            null, new Rect(0, 0, ActualWidth, ActualHeight));

        dc.PushTransform(new TranslateTransform(_pan.X, _pan.Y));
        dc.PushTransform(new ScaleTransform(_zoom, _zoom));

        // Draw source image
        if (_backgroundImage != null)
        {
            dc.DrawImage(_backgroundImage,
                new Rect(0, 0, _backgroundImage.PixelWidth, _backgroundImage.PixelHeight));
        }

        // Draw all committed objects
        foreach (var obj in _objects.Where(o => o.IsVisible))
            obj.Render(dc);

        // Draw tool preview
        _currentTool?.RenderPreview(dc);

        dc.Pop(); // scale
        dc.Pop(); // translate
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _isPanning = true;
            _panStart = e.GetPosition(this);
            CaptureMouse();
            return;
        }

        var pos = ScreenToImage(e.GetPosition(this));
        _isInteracting = true;
        _currentTool?.OnMouseDown(pos, e);
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isPanning)
        {
            var current = e.GetPosition(this);
            _pan += current - _panStart;
            _panStart = current;
            InvalidateVisual();
            return;
        }

        if (_isInteracting)
        {
            var pos = ScreenToImage(e.GetPosition(this));
            _currentTool?.OnMouseMove(pos, e);
            InvalidateVisual();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isPanning)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            return;
        }

        if (_isInteracting)
        {
            _isInteracting = false;
            var pos = ScreenToImage(e.GetPosition(this));
            _currentTool?.OnMouseUp(pos, e);

            var cmd = _currentTool?.GetCommand();
            if (cmd != null)
                CommandRequested?.Invoke(this, cmd);

            InvalidateVisual();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        var factor = e.Delta > 0 ? 1.1 : 0.9;
        Zoom *= factor;
    }

    public event EventHandler<IEditorCommand>? CommandRequested;

    private Point ScreenToImage(Point screenPoint)
    {
        return new Point(
            (screenPoint.X - _pan.X) / _zoom,
            (screenPoint.Y - _pan.Y) / _zoom);
    }
}
```

- [ ] **Step 2: Build and commit**

```bash
cd "C:/AI/신캡쳐" && dotnet build
git add -A && git commit -m "feat: add EditorCanvas with zoom, pan, tool integration"
```

---

### Task 18: EditorWindow (XAML Layout)

**Files:**
- Create: `src/ShinCapture/Views/EditorWindow.xaml`
- Create: `src/ShinCapture/Views/EditorWindow.xaml.cs`

- [ ] **Step 1: Create EditorWindow.xaml**

Create `src/ShinCapture/Views/EditorWindow.xaml`:

```xml
<Window x:Class="ShinCapture.Views.EditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:editor="clr-namespace:ShinCapture.Editor"
        Title="신캡쳐" Width="1100" Height="750"
        WindowStartupLocation="CenterScreen"
        Background="{DynamicResource BackgroundPrimaryBrush}"
        FontFamily="{DynamicResource AppFont}"
        FontSize="{DynamicResource FontSizeNormal}">

    <Window.InputBindings>
        <KeyBinding Key="Z" Modifiers="Ctrl" Command="{Binding UndoCommand}"/>
        <KeyBinding Key="Y" Modifiers="Ctrl" Command="{Binding RedoCommand}"/>
        <KeyBinding Key="S" Modifiers="Ctrl" Command="{Binding SaveCommand}"/>
        <KeyBinding Key="S" Modifiers="Ctrl+Shift" Command="{Binding SaveAsCommand}"/>
        <KeyBinding Key="C" Modifiers="Ctrl" Command="{Binding CopyCommand}"/>
        <KeyBinding Key="Escape" Command="{Binding CloseCommand}"/>
        <KeyBinding Key="Delete" Command="{Binding DeleteCommand}"/>
    </Window.InputBindings>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- Toolbar -->
            <RowDefinition Height="Auto"/>  <!-- SubBar -->
            <RowDefinition Height="*"/>     <!-- Canvas -->
            <RowDefinition Height="Auto"/>  <!-- StatusBar -->
        </Grid.RowDefinitions>

        <!-- Main Toolbar -->
        <Border Grid.Row="0" Background="{DynamicResource BackgroundSecondaryBrush}"
                BorderBrush="{DynamicResource BorderBrush}" BorderThickness="0,0,0,1"
                Padding="{DynamicResource ToolbarPadding}">
            <WrapPanel x:Name="ToolbarPanel" Orientation="Horizontal">
                <!-- Populated in code-behind -->
            </WrapPanel>
        </Border>

        <!-- Property SubBar -->
        <Border Grid.Row="1" Background="{DynamicResource BackgroundTertiaryBrush}"
                BorderBrush="{DynamicResource BorderLightBrush}" BorderThickness="0,0,0,1"
                Padding="6,5">
            <StackPanel x:Name="PropertyPanel" Orientation="Horizontal">
                <!-- Color palette, width slider, opacity slider — populated in code-behind -->
            </StackPanel>
        </Border>

        <!-- Editor Canvas -->
        <editor:EditorCanvas Grid.Row="2" x:Name="Canvas" ClipToBounds="True"/>

        <!-- Status Bar -->
        <Border Grid.Row="3" Background="{DynamicResource BackgroundSecondaryBrush}"
                BorderBrush="{DynamicResource BorderBrush}" BorderThickness="0,1,0,0"
                Padding="14,8">
            <DockPanel>
                <TextBlock x:Name="StatusText" VerticalAlignment="Center"
                           Foreground="{DynamicResource TextSecondaryBrush}"
                           FontSize="{DynamicResource FontSizeSmall}"/>
                <StackPanel DockPanel.Dock="Right" Orientation="Horizontal"
                            HorizontalAlignment="Right">
                    <Button x:Name="CopyBtn" Content="📋 복사" Margin="0,0,8,0"
                            Padding="18,6" Click="OnCopyClick"/>
                    <Button x:Name="SaveAsBtn" Content="다른 이름으로 저장" Margin="0,0,8,0"
                            Padding="18,6" Click="OnSaveAsClick"/>
                    <Button x:Name="SaveBtn" Content="💾 저장" Padding="24,6"
                            Click="OnSaveClick"
                            Background="{DynamicResource AccentBrush}"
                            Foreground="White"/>
                </StackPanel>
            </DockPanel>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 2: Create EditorWindow.xaml.cs**

Create `src/ShinCapture/Views/EditorWindow.xaml.cs` with toolbar button creation, tool switching, property panel, and save integration:

```csharp
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Editor;
using ShinCapture.Editor.Tools;
using ShinCapture.Helpers;
using ShinCapture.Models;
using ShinCapture.Services;

namespace ShinCapture.Views;

public partial class EditorWindow : Window
{
    private readonly CommandStack _commandStack = new();
    private readonly List<EditorObject> _objects = new();
    private readonly SaveManager _saveManager;
    private readonly AppSettings _settings;
    private BitmapSource _sourceImage;
    private Bitmap _sourceBitmap;
    private ITool? _activeTool;
    private readonly Dictionary<string, Button> _toolButtons = new();

    public EditorWindow(Bitmap capturedImage, SaveManager saveManager, AppSettings settings)
    {
        InitializeComponent();
        _saveManager = saveManager;
        _settings = settings;
        _sourceBitmap = capturedImage;
        _sourceImage = BitmapHelper.ToBitmapSource(capturedImage);

        Canvas.BackgroundImage = _sourceImage;
        Canvas.CommandRequested += OnCommandRequested;
        _commandStack.Changed += (_, _) => Canvas.InvalidateVisual();

        BuildToolbar();
        BuildPropertyPanel();
        UpdateStatus();

        Loaded += (_, _) => Canvas.FitToView();
    }

    private void BuildToolbar()
    {
        var tools = new (string name, string icon, string group)[]
        {
            ("펜", "✏", "draw"), ("형광펜", "🖍", "draw"), ("도형", "⃞", "draw"),
            ("화살표", "↗", "draw"), ("텍스트", "T", "draw"),
            ("모자이크", "▦", "effect"), ("블러", "◎", "effect"),
            ("번호", "①", "effect"), ("말풍선", "💬", "effect"),
            ("크롭", "✂", "edit"), ("지우개", "🧹", "edit"),
            ("스포이드", "🔍", "edit"), ("이미지", "🖼", "edit"),
        };

        string? lastGroup = null;
        foreach (var (name, icon, group) in tools)
        {
            if (lastGroup != null && lastGroup != group)
                ToolbarPanel.Children.Add(CreateSeparator());

            var btn = CreateToolButton(icon, name);
            btn.Click += (_, _) => SelectTool(name);
            ToolbarPanel.Children.Add(btn);
            _toolButtons[name] = btn;
            lastGroup = group;
        }

        // Separator + Undo/Redo
        ToolbarPanel.Children.Add(CreateSeparator());
        var undoBtn = CreateToolButton("↩", "실행취소");
        undoBtn.Click += (_, _) => _commandStack.Undo();
        ToolbarPanel.Children.Add(undoBtn);

        var redoBtn = CreateToolButton("↪", "다시실행");
        redoBtn.Click += (_, _) => _commandStack.Redo();
        ToolbarPanel.Children.Add(redoBtn);
    }

    private void SelectTool(string name)
    {
        _activeTool?.Reset();

        _activeTool = name switch
        {
            "펜" => new PenTool(_objects),
            "형광펜" => new HighlighterTool(_objects),
            "도형" => new ShapeTool(_objects),
            "화살표" => new ArrowTool(_objects),
            "텍스트" => new TextTool(_objects, Canvas),
            // Effect and edit tools will be wired similarly
            _ => null
        };

        Canvas.SetTool(_activeTool);

        // Update button states
        foreach (var (n, btn) in _toolButtons)
        {
            btn.Background = n == name
                ? (Brush)FindResource("ToolbarButtonActiveBrush")
                : System.Windows.Media.Brushes.Transparent;
            btn.BorderBrush = n == name
                ? (Brush)FindResource("ToolbarButtonActiveBorderBrush")
                : System.Windows.Media.Brushes.Transparent;
        }
    }

    private void OnCommandRequested(object? sender, IEditorCommand cmd)
    {
        _commandStack.Execute(cmd);
        UpdateStatus();
    }

    private Button CreateToolButton(string icon, string tooltip)
    {
        return new Button
        {
            Content = $"{icon} {tooltip}",
            Padding = new Thickness(6),
            Margin = new Thickness(1),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderBrush = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(1),
            FontSize = 12,
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = tooltip
        };
    }

    private UIElement CreateSeparator()
    {
        return new Border
        {
            Width = 1, Height = 24, Margin = new Thickness(6, 0, 6, 0),
            Background = (Brush)FindResource("DividerBrush")
        };
    }

    private void BuildPropertyPanel()
    {
        // Color palette
        var colors = new[] { "#0078D4", "#E81123", "#10893E", "#FF8C00", "#191919", "#FFFFFF" };
        foreach (var hex in colors)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var swatch = new Border
            {
                Width = 18, Height = 18, CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(color),
                BorderThickness = new Thickness(2),
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                Margin = new Thickness(2),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            swatch.MouseDown += (_, _) =>
            {
                if (_activeTool != null) _activeTool.CurrentColor = color;
            };
            PropertyPanel.Children.Add(swatch);
        }
    }

    private void UpdateStatus()
    {
        if (_sourceImage != null)
            StatusText.Text = $"{_sourceImage.PixelWidth} × {_sourceImage.PixelHeight} px · PNG";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var rendered = RenderFinalImage();
        _saveManager.SaveAuto(rendered, _settings);
    }

    private void OnSaveAsClick(object sender, RoutedEventArgs e)
    {
        var rendered = RenderFinalImage();
        _saveManager.SaveAs(rendered);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var rendered = RenderFinalImage();
        var bitmapSource = BitmapHelper.ToBitmapSource(rendered);
        Clipboard.SetImage(bitmapSource);
    }

    private Bitmap RenderFinalImage()
    {
        // Render all objects onto the source image
        var result = (Bitmap)_sourceBitmap.Clone();
        // Objects rendering to bitmap will be finalized when SaveManager is built
        return result;
    }
}
```

- [ ] **Step 3: Wire EditorWindow into MainWindow.HandleCaptureResult**

Update the `HandleCaptureResult` method in MainWindow:

```csharp
case AfterCaptureAction.OpenEditor:
    var editor = new EditorWindow(result.Image, _saveManager, _settings);
    editor.Show();
    break;
```

(This requires adding `_saveManager` field to MainWindow — will be done in Task 23.)

- [ ] **Step 4: Build and commit**

```bash
cd "C:/AI/신캡쳐" && dotnet build
git add -A && git commit -m "feat: add EditorWindow with toolbar, property panel, canvas, status bar"
```

---

### Task 19: SaveManager Service

**Files:**
- Create: `src/ShinCapture/Services/SaveManager.cs`
- Create: `src/ShinCapture/Models/RecentCaptureEntry.cs`
- Create: `src/ShinCapture/Services/RecentCapturesManager.cs`
- Create: `tests/ShinCapture.Tests/Services/SaveManagerTests.cs`

- [ ] **Step 1: Write SaveManager tests**

Create `tests/ShinCapture.Tests/Services/SaveManagerTests.cs`:

```csharp
using System.Drawing;
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
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
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
}
```

- [ ] **Step 2: Run tests to verify they fail, then implement**

Create `src/ShinCapture/Services/SaveManager.cs`:

```csharp
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ShinCapture.Models;

namespace ShinCapture.Services;

public class SaveManager
{
    public void SaveToFile(Bitmap bitmap, string filePath, string format, int jpgQuality)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var encoder = GetEncoder(format);
        if (format.ToLower() == "jpg" || format.ToLower() == "jpeg")
        {
            var qualityParam = new EncoderParameter(Encoder.Quality, jpgQuality);
            var encoderParams = new EncoderParameters(1) { Param = { [0] = qualityParam } };
            bitmap.Save(filePath, encoder, encoderParams);
        }
        else
        {
            bitmap.Save(filePath, GetImageFormat(format));
        }
    }

    public string SaveAuto(Bitmap bitmap, AppSettings settings)
    {
        var dir = settings.Save.AutoSavePath;
        Directory.CreateDirectory(dir);

        var fileName = GenerateFileName(settings.Save);
        var ext = settings.Save.DefaultFormat;
        var filePath = Path.Combine(dir, $"{fileName}.{ext}");

        SaveToFile(bitmap, filePath, ext, settings.Save.JpgQuality);
        return filePath;
    }

    public string? SaveAs(Bitmap bitmap)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|BMP (*.bmp)|*.bmp|GIF (*.gif)|*.gif",
            DefaultExt = ".png",
            FileName = GenerateFileName(new SaveSettings())
        };

        if (dialog.ShowDialog() == true)
        {
            var ext = Path.GetExtension(dialog.FileName).TrimStart('.');
            SaveToFile(bitmap, dialog.FileName, ext, 90);
            return dialog.FileName;
        }
        return null;
    }

    public static string GenerateFileName(SaveSettings settings)
    {
        var now = DateTime.Now;
        return settings.FileNamePattern
            .Replace("{date}", now.ToString("yyyyMMdd"))
            .Replace("{time}", now.ToString("HHmmss"))
            .Replace("신캡쳐_{date}_{time}", $"신캡쳐_{now:yyyyMMdd}_{now:HHmmss}");
    }

    private static ImageCodecInfo GetEncoder(string format)
    {
        var mimeType = format.ToLower() switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "bmp" => "image/bmp",
            "gif" => "image/gif",
            _ => "image/png"
        };
        return ImageCodecInfo.GetImageEncoders().First(e => e.MimeType == mimeType);
    }

    private static ImageFormat GetImageFormat(string format) => format.ToLower() switch
    {
        "jpg" or "jpeg" => ImageFormat.Jpeg,
        "bmp" => ImageFormat.Bmp,
        "gif" => ImageFormat.Gif,
        _ => ImageFormat.Png
    };
}
```

Create `src/ShinCapture/Models/RecentCaptureEntry.cs`:

```csharp
namespace ShinCapture.Models;

public class RecentCaptureEntry
{
    public string FilePath { get; set; } = "";
    public string ThumbnailPath { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
```

- [ ] **Step 3: Run tests**

```bash
cd "C:/AI/신캡쳐" && dotnet test --verbosity normal
```

Expected: All tests PASS.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: add SaveManager with PNG/JPG/BMP/GIF support + auto-naming"
```

---

## Phase 5: Settings Window & Integration (Tasks 20–22)

### Task 20: Settings Window

**Files:**
- Create: `src/ShinCapture/Views/SettingsWindow.xaml`
- Create: `src/ShinCapture/Views/SettingsWindow.xaml.cs`

- [ ] **Step 1: Create tabbed SettingsWindow.xaml**

Create SettingsWindow with 5 tabs (일반, 캡쳐, 저장, 단축키, 지정사이즈), all styled with Fluent Design tokens. Each tab contains the relevant settings controls bound to AppSettings properties.

- [ ] **Step 2: Create SettingsWindow.xaml.cs**

Wire up load/save through SettingsManager, hotkey conflict detection, and folder browser dialog for save path.

- [ ] **Step 3: Wire into MainWindow.OpenSettings**

Update `MainWindow.OpenSettings()` to open the SettingsWindow and reload hotkeys on close.

- [ ] **Step 4: Build and commit**

```bash
cd "C:/AI/신캡쳐" && dotnet build
git add -A && git commit -m "feat: add SettingsWindow with 5 tabs"
```

---

### Task 21: Full Integration (MainWindow ↔ Editor ↔ Save)

**Files:**
- Modify: `src/ShinCapture/Views/MainWindow.xaml.cs`
- Modify: `src/ShinCapture/Views/MainWindow.xaml`
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs`
- Modify: `src/ShinCapture/App.xaml.cs`

- [ ] **Step 1: Add SaveManager to App and MainWindow**

Update `App.xaml.cs` to create SaveManager and pass it to MainWindow. Update MainWindow to hold SaveManager reference and pass it to EditorWindow on capture.

- [ ] **Step 2: Populate MainWindow capture mode grid buttons**

Fill the CaptureModesPanel in MainWindow with 7 styled buttons (icon + name + hotkey) using the Fluent Design theme resources.

- [ ] **Step 3: Wire clipboard copy on afterCapture=ClipboardOnly**

Ensure all three afterCapture paths work: OpenEditor, SaveDirectly, ClipboardOnly.

- [ ] **Step 4: Test full flow manually**

```bash
cd "C:/AI/신캡쳐" && dotnet run --project src/ShinCapture
```

Test: PrintScreen → drag region → editor opens → draw with pen → save → file appears in save folder.

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat: wire full capture → editor → save pipeline"
```

---

### Task 22: Recent Captures Manager

**Files:**
- Create: `src/ShinCapture/Services/RecentCapturesManager.cs`
- Create: `tests/ShinCapture.Tests/Services/RecentCapturesManagerTests.cs`

- [ ] **Step 1: Write tests and implement RecentCapturesManager**

RecentCapturesManager maintains a list of RecentCaptureEntry objects persisted to `recent.json` alongside settings. It generates 150x100 thumbnail images, auto-prunes entries beyond maxCount, and removes entries whose source files no longer exist.

- [ ] **Step 2: Integrate into MainWindow (recent captures display) and EditorWindow (add entry on save)**

- [ ] **Step 3: Run tests and commit**

```bash
cd "C:/AI/신캡쳐" && dotnet test && dotnet build
git add -A && git commit -m "feat: add RecentCapturesManager with thumbnail caching"
```

---

## Phase 6: Polish & Deployment (Tasks 23–25)

### Task 23: Render Final Image (Objects → Bitmap)

**Files:**
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs` (RenderFinalImage)
- Modify: `src/ShinCapture/Helpers/BitmapHelper.cs`

- [ ] **Step 1: Implement proper RenderFinalImage using RenderTargetBitmap**

Update `EditorWindow.RenderFinalImage()` to render all EditorObjects onto the source bitmap using WPF's `RenderTargetBitmap` and `DrawingVisual`:

```csharp
private Bitmap RenderFinalImage()
{
    var width = _sourceImage.PixelWidth;
    var height = _sourceImage.PixelHeight;

    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        dc.DrawImage(_sourceImage, new Rect(0, 0, width, height));
        foreach (var obj in _objects.Where(o => o.IsVisible))
            obj.Render(dc);
    }

    var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    rtb.Render(visual);

    return BitmapHelper.ToBitmap(rtb);
}
```

- [ ] **Step 2: Test save with drawn objects**

Verify that saved images include all drawn annotations.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: render editor objects onto final saved image"
```

---

### Task 24: Portable Mode Detection

**Files:**
- Modify: `src/ShinCapture/Services/SettingsManager.cs`

- [ ] **Step 1: Add portable mode**

If a `portable.txt` file exists next to the executable, use `config/` folder next to exe instead of `%AppData%`:

```csharp
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
            : Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData), "ShinCapture");
    }
    _filePath = Path.Combine(_settingsDir, "settings.json");
}
```

- [ ] **Step 2: Commit**

```bash
git add -A && git commit -m "feat: add portable mode with config/ folder detection"
```

---

### Task 25: Inno Setup Installer

**Files:**
- Create: `installer/setup.iss`

- [ ] **Step 1: Create Inno Setup script**

Create `installer/setup.iss`:

```iss
[Setup]
AppName=신캡쳐
AppVersion=1.0.0
AppPublisher=ShinCapture
DefaultDirName={autopf}\ShinCapture
DefaultGroupName=신캡쳐
OutputDir=output
OutputBaseFilename=ShinCapture_Setup_1.0.0
Compression=lzma2
SolidCompression=yes
SetupIconFile=..\src\ShinCapture\Assets\icon.ico
UninstallDisplayIcon={app}\ShinCapture.exe
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\신캡쳐"; Filename: "{app}\ShinCapture.exe"
Name: "{autodesktop}\신캡쳐"; Filename: "{app}\ShinCapture.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "바탕화면에 바로가기 만들기"; GroupDescription: "추가 설정"
Name: "startup"; Description: "Windows 시작 시 자동 실행"; GroupDescription: "추가 설정"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "ShinCapture"; ValueData: """{app}\ShinCapture.exe"""; \
    Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\ShinCapture.exe"; Description: "신캡쳐 실행"; Flags: nowait postinstall skipifsilent
```

- [ ] **Step 2: Create build + package scripts**

Create `build.bat`:

```bat
@echo off
echo Building ShinCapture...
dotnet publish src\ShinCapture\ShinCapture.csproj -c Release -r win-x64 --self-contained -o publish
echo.
echo Build complete. Output in publish/
echo.
echo To create installer, run Inno Setup on installer/setup.iss
echo To create portable zip, zip the publish/ folder and add portable.txt
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: add Inno Setup installer script and build script"
```

---

## Post-Implementation Checklist

After all tasks are complete, verify:

- [ ] All 7 capture modes work (PrintScreen, Ctrl+Shift+F/W/D/A/S/Z)
- [ ] Editor opens after capture with all 14 tools functional
- [ ] Undo/Redo works correctly
- [ ] Save (PNG/JPG/BMP/GIF), Save As, Clipboard Copy all work
- [ ] Settings persist across restarts
- [ ] Tray icon, context menu, hotkeys all functional
- [ ] Recent captures list populates
- [ ] Installer creates working installation
- [ ] Portable zip works with portable.txt marker
- [ ] All unit tests pass: `dotnet test`
