# Editor Window Sizing Modes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add remembered-size, maximized, and capture-fit editor window modes, defaulting to the user's last mouse-resized size without breaking mixed-DPI or OCR behavior.

**Architecture:** Persist an additive `EditorSettings` section, keep size validation in a pure `EditorWindowSizingPolicy`, and isolate Win32 monitor/DPI lookup in `MonitorWorkAreaService`. `EditorWindow` becomes the only place that applies the mode, while `MainWindow` and `SettingsWindow` request policy refreshes instead of assigning window state directly.

**Tech Stack:** C# 12, .NET 8, WPF, Win32 `HwndSource`/monitor APIs, System.Text.Json, xUnit

---

## File map

- Create `src/ShinCapture/Models/EditorWindowSizeMode.cs`: serialized mode enum.
- Create `src/ShinCapture/Models/EditorSettings.cs`: persisted editor window preferences.
- Modify `src/ShinCapture/Models/AppSettings.cs`: expose the additive `Editor` section.
- Create `src/ShinCapture/Editor/EditorWindowSizingPolicy.cs`: pure size normalization and mode rules.
- Create `tests/ShinCapture.Tests/Editor/EditorWindowSizingPolicyTests.cs`: pure policy tests.
- Modify `src/ShinCapture/Services/SettingsManager.cs`: normalized load and read-modify-write update path.
- Modify `tests/ShinCapture.Tests/Services/SettingsManagerTests.cs`: migration, round-trip, partial/silent update tests.
- Modify `src/ShinCapture/Helpers/NativeMethods.cs`: monitor/DPI and size/move message declarations.
- Create `src/ShinCapture/Services/MonitorWorkAreaService.cs`: convert the current monitor work area to WPF DIP.
- Modify `src/ShinCapture/Views/EditorWindow.xaml.cs`: central policy application and `WM_EXITSIZEMOVE` persistence.
- Modify `src/ShinCapture/Views/SettingsWindow.xaml`: mode selector and explanatory copy.
- Modify `src/ShinCapture/Views/SettingsWindow.xaml.cs`: load/save selected mode.
- Modify `src/ShinCapture/Views/MainWindow.xaml.cs`: stop forcing `Normal` and refresh the editor after settings changes.

### Task 1: Persisted model and pure sizing policy

**Files:**
- Create: `src/ShinCapture/Models/EditorWindowSizeMode.cs`
- Create: `src/ShinCapture/Models/EditorSettings.cs`
- Modify: `src/ShinCapture/Models/AppSettings.cs`
- Create: `src/ShinCapture/Editor/EditorWindowSizingPolicy.cs`
- Test: `tests/ShinCapture.Tests/Editor/EditorWindowSizingPolicyTests.cs`

- [ ] **Step 1: Write failing policy tests**

Create `tests/ShinCapture.Tests/Editor/EditorWindowSizingPolicyTests.cs`:

```csharp
using ShinCapture.Editor;
using ShinCapture.Models;

namespace ShinCapture.Tests.Editor;

public class EditorWindowSizingPolicyTests
{
    [Fact]
    public void NormalizeRememberedSize_KeepsValidDipSize()
    {
        var result = EditorWindowSizingPolicy.NormalizeRememberedSize(
            width: 1200, height: 800, workAreaWidth: 1920, workAreaHeight: 1040);

        Assert.Equal(1200, result.Width);
        Assert.Equal(800, result.Height);
    }

    [Theory]
    [InlineData(double.NaN, 750)]
    [InlineData(double.PositiveInfinity, 750)]
    [InlineData(0, 750)]
    [InlineData(-1, 750)]
    public void NormalizeRememberedSize_InvalidWidth_UsesDefault(double width, double height)
    {
        var result = EditorWindowSizingPolicy.NormalizeRememberedSize(
            width, height, workAreaWidth: 1920, workAreaHeight: 1040);

        Assert.Equal(EditorWindowSizingPolicy.DefaultWidth, result.Width);
    }

    [Fact]
    public void NormalizeRememberedSize_ClampsToUsableRange()
    {
        var tooSmall = EditorWindowSizingPolicy.NormalizeRememberedSize(200, 100, 1920, 1040);
        var tooLarge = EditorWindowSizingPolicy.NormalizeRememberedSize(4000, 3000, 1366, 728);

        Assert.Equal(EditorWindowSizingPolicy.MinimumWidth, tooSmall.Width);
        Assert.Equal(EditorWindowSizingPolicy.MinimumHeight, tooSmall.Height);
        Assert.Equal(1366, tooLarge.Width);
        Assert.Equal(728, tooLarge.Height);
    }

    [Fact]
    public void ModeRules_AreMutuallyExclusive()
    {
        Assert.True(EditorWindowSizingPolicy.ShouldKeepOuterSize(EditorWindowSizeMode.RememberLast));
        Assert.True(EditorWindowSizingPolicy.ShouldMaximize(EditorWindowSizeMode.Maximized));
        Assert.True(EditorWindowSizingPolicy.ShouldFitToCapture(EditorWindowSizeMode.FitToCapture));
        Assert.False(EditorWindowSizingPolicy.ShouldGrowForOcr(EditorWindowSizeMode.RememberLast));
        Assert.False(EditorWindowSizingPolicy.ShouldGrowForOcr(EditorWindowSizeMode.Maximized));
        Assert.True(EditorWindowSizingPolicy.ShouldGrowForOcr(EditorWindowSizeMode.FitToCapture));
    }
}
```

- [ ] **Step 2: Run the focused test and confirm the missing-type failure**

Run:

```powershell
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~EditorWindowSizingPolicyTests
```

Expected: FAIL at compile time because `EditorWindowSizingPolicy` and `EditorWindowSizeMode` do not exist.

- [ ] **Step 3: Add the persisted model types**

Create `src/ShinCapture/Models/EditorWindowSizeMode.cs`:

```csharp
namespace ShinCapture.Models;

public enum EditorWindowSizeMode
{
    RememberLast,
    Maximized,
    FitToCapture
}
```

Create `src/ShinCapture/Models/EditorSettings.cs`:

```csharp
namespace ShinCapture.Models;

public sealed class EditorSettings
{
    public EditorWindowSizeMode WindowSizeMode { get; set; } = EditorWindowSizeMode.RememberLast;
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 750;
}
```

Add this property to `AppSettings` immediately after `Capture`:

```csharp
public EditorSettings Editor { get; set; } = new();
```

- [ ] **Step 4: Implement the pure policy**

Create `src/ShinCapture/Editor/EditorWindowSizingPolicy.cs`:

```csharp
using System;
using ShinCapture.Models;

namespace ShinCapture.Editor;

public readonly record struct EditorWindowSize(double Width, double Height);

public static class EditorWindowSizingPolicy
{
    public const double DefaultWidth = 1100;
    public const double DefaultHeight = 750;
    public const double MinimumWidth = 760;
    public const double MinimumHeight = 520;

    public static EditorWindowSize NormalizeRememberedSize(
        double width,
        double height,
        double workAreaWidth,
        double workAreaHeight)
    {
        var safeWorkWidth = IsPositiveFinite(workAreaWidth) ? workAreaWidth : DefaultWidth;
        var safeWorkHeight = IsPositiveFinite(workAreaHeight) ? workAreaHeight : DefaultHeight;
        var safeWidth = IsPositiveFinite(width) ? width : DefaultWidth;
        var safeHeight = IsPositiveFinite(height) ? height : DefaultHeight;
        var minWidth = Math.Min(MinimumWidth, safeWorkWidth);
        var minHeight = Math.Min(MinimumHeight, safeWorkHeight);

        return new EditorWindowSize(
            Math.Clamp(safeWidth, minWidth, safeWorkWidth),
            Math.Clamp(safeHeight, minHeight, safeWorkHeight));
    }

    public static bool IsValidPersistedSize(double width, double height)
        => IsPositiveFinite(width) && IsPositiveFinite(height);

    public static bool ShouldKeepOuterSize(EditorWindowSizeMode mode)
        => mode == EditorWindowSizeMode.RememberLast;

    public static bool ShouldMaximize(EditorWindowSizeMode mode)
        => mode == EditorWindowSizeMode.Maximized;

    public static bool ShouldFitToCapture(EditorWindowSizeMode mode)
        => mode == EditorWindowSizeMode.FitToCapture;

    public static bool ShouldGrowForOcr(EditorWindowSizeMode mode)
        => mode == EditorWindowSizeMode.FitToCapture;

    private static bool IsPositiveFinite(double value)
        => double.IsFinite(value) && value > 0;
}
```

- [ ] **Step 5: Run the focused tests**

Run the command from Step 2.

Expected: all `EditorWindowSizingPolicyTests` PASS.

- [ ] **Step 6: Commit the model and policy**

```powershell
git add src/ShinCapture/Models/EditorWindowSizeMode.cs src/ShinCapture/Models/EditorSettings.cs src/ShinCapture/Models/AppSettings.cs src/ShinCapture/Editor/EditorWindowSizingPolicy.cs tests/ShinCapture.Tests/Editor/EditorWindowSizingPolicyTests.cs
git commit -m "feat: 편집기 창 크기 정책 모델 추가"
```

### Task 2: Backward-compatible and silent settings updates

**Files:**
- Modify: `src/ShinCapture/Services/SettingsManager.cs`
- Modify: `tests/ShinCapture.Tests/Services/SettingsManagerTests.cs`

- [ ] **Step 1: Add failing migration and update tests**

Append to `SettingsManagerTests`:

```csharp
[Fact]
public void Load_LegacyJsonWithoutEditor_UsesRememberLastDefaults()
{
    File.WriteAllText(Path.Combine(_tempDir, "settings.json"), "{\"general\":{\"autoStart\":true}}");

    var loaded = _manager.Load();

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
public void Update_Silently_PreservesOtherSettingsAndDoesNotRaiseEvent()
{
    var initial = _manager.Load();
    initial.Hotkeys.RegionCapture = "Ctrl+Alt+9";
    _manager.Save(initial);
    var eventCount = 0;
    _manager.SettingsChanged += (_, _) => eventCount++;

    _manager.Update(settings =>
    {
        settings.Editor.WindowWidth = 1234;
        settings.Editor.WindowHeight = 777;
    }, raiseChanged: false);

    var loaded = _manager.Load();
    Assert.Equal("Ctrl+Alt+9", loaded.Hotkeys.RegionCapture);
    Assert.Equal(1234, loaded.Editor.WindowWidth);
    Assert.Equal(777, loaded.Editor.WindowHeight);
    Assert.Equal(0, eventCount);
}
```

- [ ] **Step 2: Run the settings tests and confirm `Update` is missing**

Run:

```powershell
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~SettingsManagerTests
```

Expected: FAIL at compile time because `SettingsManager.Update` does not exist.

- [ ] **Step 3: Normalize the additive section and add read-modify-write update**

Refactor `SettingsManager` so `Load`, `Save`, and the new method use these bodies:

```csharp
private readonly object _sync = new();

public AppSettings Load()
{
    lock (_sync)
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
}

public void Save(AppSettings settings) => SaveCore(settings, raiseChanged: true);

public void Update(Action<AppSettings> update, bool raiseChanged = true)
{
    ArgumentNullException.ThrowIfNull(update);
    lock (_sync)
    {
        var settings = Load();
        update(settings);
        SaveCore(settings, raiseChanged);
    }
}

private void SaveCore(AppSettings settings, bool raiseChanged)
{
    lock (_sync)
    {
        Directory.CreateDirectory(_settingsDir);
        var json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
        File.WriteAllText(_filePath, json);
    }
    if (raiseChanged)
        SettingsChanged?.Invoke(this, EventArgs.Empty);
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
```

Keep `JsonOptions` unchanged so the enum continues to serialize as camel-case text.

- [ ] **Step 4: Run settings and full tests**

Run:

```powershell
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~SettingsManagerTests
dotnet test ShinCapture.sln
```

Expected: settings tests PASS, then all tests PASS.

- [ ] **Step 5: Commit settings migration support**

```powershell
git add src/ShinCapture/Services/SettingsManager.cs tests/ShinCapture.Tests/Services/SettingsManagerTests.cs
git commit -m "feat: 편집기 창 설정 마이그레이션 추가"
```

### Task 3: Current-monitor work area service

**Files:**
- Modify: `src/ShinCapture/Helpers/NativeMethods.cs`
- Create: `src/ShinCapture/Services/MonitorWorkAreaService.cs`
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs`

- [ ] **Step 1: Add the monitor and message declarations**

Add to `NativeMethods` before the structs:

```csharp
public const int WM_ENTERSIZEMOVE = 0x0231;
public const int WM_EXITSIZEMOVE = 0x0232;
public const uint MONITOR_DEFAULTTONEAREST = 2;
public const uint SWP_NOZORDER = 0x0004;
public const uint SWP_NOACTIVATE = 0x0010;

[DllImport("user32.dll")]
public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

[DllImport("user32.dll")]
public static extern uint GetDpiForWindow(IntPtr hwnd);

[DllImport("user32.dll", SetLastError = true)]
public static extern bool SetWindowPos(
    IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
```

Add this struct beside `RECT`:

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct MONITORINFO
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
}
```

- [ ] **Step 2: Implement the single conversion boundary**

Create `src/ShinCapture/Services/MonitorWorkAreaService.cs`:

```csharp
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ShinCapture.Helpers;

namespace ShinCapture.Services;

public readonly record struct MonitorWorkArea(
    int PixelLeft, int PixelTop, int PixelWidth, int PixelHeight, double DpiScale)
{
    public int PixelRight => PixelLeft + PixelWidth;
    public int PixelBottom => PixelTop + PixelHeight;
    public double DipWidth => PixelWidth / DpiScale;
    public double DipHeight => PixelHeight / DpiScale;
}

public static class MonitorWorkAreaService
{
    public static MonitorWorkArea GetForWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new NativeMethods.MONITORINFO
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };
            if (monitor != IntPtr.Zero && NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                var dpi = NativeMethods.GetDpiForWindow(hwnd);
                var scale = dpi > 0 ? dpi / 96d : 1d;
                return new MonitorWorkArea(
                    info.rcWork.Left,
                    info.rcWork.Top,
                    info.rcWork.Width,
                    info.rcWork.Height,
                    scale);
            }
        }

        var fallback = SystemParameters.WorkArea;
        return new MonitorWorkArea(
            (int)Math.Round(fallback.Left),
            (int)Math.Round(fallback.Top),
            (int)Math.Round(fallback.Width),
            (int)Math.Round(fallback.Height),
            1d);
    }

    public static void CenterWindow(Window window, MonitorWorkArea? knownArea = null)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var area = knownArea ?? GetForWindow(window);
        window.UpdateLayout();
        var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth * area.DpiScale));
        var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight * area.DpiScale));
        var x = area.PixelLeft + (area.PixelWidth - width) / 2;
        var y = area.PixelTop + (area.PixelHeight - height) / 2;
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    public static void ClampWindowToWorkArea(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var rect)) return;
        var area = GetForWindow(window);
        var width = Math.Min(rect.Width, area.PixelWidth);
        var height = Math.Min(rect.Height, area.PixelHeight);
        var x = Math.Clamp(rect.Left, area.PixelLeft, area.PixelRight - width);
        var y = Math.Clamp(rect.Top, area.PixelTop, area.PixelBottom - height);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }
}
```

- [ ] **Step 3: Replace current monitor arithmetic in `SizeWindowToImage`**

Replace the `Screen.FromHandle`, `WorkingArea`, and physical-pixel division block with:

```csharp
var workArea = MonitorWorkAreaService.GetForWindow(this);
double maxW = workArea.DipWidth;
double maxH = workArea.DipHeight;
```

Replace the final centering assignments with a physical-pixel placement call:

```csharp
MonitorWorkAreaService.CenterWindow(this, workArea);
```

Keep the image logical-size calculation based on `VisualTreeHelper.GetDpi(this)`; that calculation converts bitmap pixels, while the new service converts monitor geometry.

- [ ] **Step 4: Build and run existing zoom tests**

Run:

```powershell
dotnet build ShinCapture.sln
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~EditorZoomPolicyTests
```

Expected: build succeeds and all zoom policy tests PASS.

- [ ] **Step 5: Commit monitor work-area support**

```powershell
git add src/ShinCapture/Helpers/NativeMethods.cs src/ShinCapture/Services/MonitorWorkAreaService.cs src/ShinCapture/Views/EditorWindow.xaml.cs
git commit -m "fix: 편집기 크기 계산을 현재 모니터 DPI에 맞춤"
```

### Task 4: Centralize sizing in `EditorWindow`

**Files:**
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs`
- Modify: `src/ShinCapture/Views/MainWindow.xaml.cs`

- [ ] **Step 1: Add state and hook lifecycle fields**

Add these fields near the other editor fields:

```csharp
private EditorWindowSizeMode? _appliedWindowSizeMode;
private bool _applyingWindowSizingPolicy;
private HwndSource? _hwndSource;
```

In the constructor add:

```csharp
SourceInitialized += OnSourceInitialized;
Closed += OnEditorClosed;
```

Replace each direct `SizeWindowToImage()` call in the Loaded, new-capture, and history paths with:

```csharp
ApplyWindowSizingPolicy(imageChanged: true);
```

- [ ] **Step 2: Add one policy application method**

Add to `EditorWindow`:

```csharp
public void RefreshWindowSizingPolicy()
{
    if (!IsLoaded) return;
    ApplyWindowSizingPolicy(imageChanged: false);
    UpdateLayout();
    Canvas.ApplyInitialZoom();
}

private EditorSettings CurrentEditorSettings()
    => (_settingsManager?.Load() ?? _settings).Editor ?? new EditorSettings();

private void ApplyWindowSizingPolicy(bool imageChanged)
{
    var editorSettings = CurrentEditorSettings();
    var mode = editorSettings.WindowSizeMode;
    var changedMode = _appliedWindowSizeMode != mode;
    _applyingWindowSizingPolicy = true;
    try
    {
        if (mode == EditorWindowSizeMode.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
        else if (mode == EditorWindowSizeMode.FitToCapture)
        {
            WindowState = WindowState.Normal;
            if (imageChanged || changedMode)
                SizeWindowToImage();
        }
        else if (changedMode || _appliedWindowSizeMode is null)
        {
            WindowState = WindowState.Normal;
            var workArea = MonitorWorkAreaService.GetForWindow(this);
            var size = EditorWindowSizingPolicy.NormalizeRememberedSize(
                editorSettings.WindowWidth,
                editorSettings.WindowHeight,
                workArea.DipWidth,
                workArea.DipHeight);
            Width = size.Width;
            Height = size.Height;
            MonitorWorkAreaService.CenterWindow(this, workArea);
        }

        _appliedWindowSizeMode = mode;
    }
    finally
    {
        _applyingWindowSizingPolicy = false;
    }
}
```

Important: in `RememberLast`, do nothing on later image changes. That is the feature's core behavior.

- [ ] **Step 3: Persist only after an actual mouse size/move loop**

Add:

```csharp
private void OnSourceInitialized(object? sender, EventArgs e)
{
    _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
    _hwndSource?.AddHook(EditorWindowProc);
}

private void OnEditorClosed(object? sender, EventArgs e)
{
    _hwndSource?.RemoveHook(EditorWindowProc);
    _hwndSource = null;
}

private IntPtr EditorWindowProc(
    IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
{
    if (msg == NativeMethods.WM_EXITSIZEMOVE)
        PersistUserWindowSize();
    return IntPtr.Zero;
}

private void PersistUserWindowSize()
{
    if (_applyingWindowSizingPolicy || WindowState != WindowState.Normal)
        return;
    if (CurrentEditorSettings().WindowSizeMode != EditorWindowSizeMode.RememberLast)
        return;

    var bounds = RestoreBounds;
    if (!EditorWindowSizingPolicy.IsValidPersistedSize(bounds.Width, bounds.Height))
        return;

    try
    {
        _settings.Editor.WindowWidth = bounds.Width;
        _settings.Editor.WindowHeight = bounds.Height;
        _settingsManager?.Update(settings =>
        {
            settings.Editor ??= new EditorSettings();
            settings.Editor.WindowWidth = bounds.Width;
            settings.Editor.WindowHeight = bounds.Height;
        }, raiseChanged: false);
    }
    catch
    {
        // Window resizing must remain usable even if settings persistence fails.
    }
}
```

Do not add persistence to `SizeChanged`.

- [ ] **Step 4: Remove `MainWindow` state overrides and refresh on settings changes**

In both editor reuse paths, delete:

```csharp
_editorWindow.WindowState = WindowState.Normal;
```

After `Show()` in `ShowEditor()`, call:

```csharp
_editorWindow.RefreshWindowSizingPolicy();
```

In `OnExternalSettingsChanged`, after reloading `_settings`, add:

```csharp
_editorWindow?.RefreshWindowSizingPolicy();
```

Keep the existing temporary topmost/activate sequence.

- [ ] **Step 5: Build and run all tests**

Run:

```powershell
dotnet build ShinCapture.sln
dotnet test ShinCapture.sln
```

Expected: build succeeds; all existing and new tests PASS.

- [ ] **Step 6: Commit centralized policy application**

```powershell
git add src/ShinCapture/Views/EditorWindow.xaml.cs src/ShinCapture/Views/MainWindow.xaml.cs
git commit -m "feat: 편집기 창 크기 모드 적용"
```

### Task 5: Settings UI and OCR outer-size protection

**Files:**
- Modify: `src/ShinCapture/Views/SettingsWindow.xaml`
- Modify: `src/ShinCapture/Views/SettingsWindow.xaml.cs`
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs`

- [ ] **Step 1: Add the mode selector to the Capture tab**

Insert after `CmbAfterCapture` in `SettingsWindow.xaml`:

```xml
<TextBlock Text="편집기 창 크기"
           Foreground="{DynamicResource TextSecondaryBrush}"
           FontSize="{DynamicResource FontSizeSmall}"
           Margin="0,0,0,4"/>
<ComboBox x:Name="CmbEditorWindowSizeMode" Margin="0,0,0,4">
    <ComboBoxItem Content="마지막으로 조절한 크기 유지 (권장)" Tag="RememberLast"/>
    <ComboBoxItem Content="현재 모니터에 꽉 차게 (최대화)" Tag="Maximized"/>
    <ComboBoxItem Content="캡처 이미지 크기에 맞춤" Tag="FitToCapture"/>
</ComboBox>
<TextBlock Text="고정 모드에서는 새 캡처와 OCR이 창 바깥 크기를 바꾸지 않습니다."
           TextWrapping="Wrap"
           Foreground="{DynamicResource TextDisabledBrush}"
           FontSize="11"
           Margin="0,0,0,16"/>
```

The added content still fits the fixed 520 DIP settings window; verify no clipping at 125% and 200% text scaling.

- [ ] **Step 2: Load and save the selected tag**

In `LoadSettings()` add:

```csharp
SelectComboByTag(CmbEditorWindowSizeMode, _settings.Editor.WindowSizeMode.ToString());
```

In `ApplyToSettings()` add:

```csharp
_settings.Editor.WindowSizeMode = GetComboTag(CmbEditorWindowSizeMode) switch
{
    "Maximized" => EditorWindowSizeMode.Maximized,
    "FitToCapture" => EditorWindowSizeMode.FitToCapture,
    _ => EditorWindowSizeMode.RememberLast
};
```

- [ ] **Step 3: Prevent OCR from resizing fixed/maximized windows**

At the start of `OnOcrPanelVisibilityChanged`, after reading `isVisible`, add:

```csharp
var canGrowWindow = EditorWindowSizingPolicy.ShouldGrowForOcr(
    CurrentEditorSettings().WindowSizeMode);
if (!canGrowWindow)
{
    _editorHeightBeforeOcr = -1;
    if (isVisible) RefreshOcrBanner();
    return;
}
```

In `GrowWindowForOcrPanel`, replace `SystemParameters.WorkArea` with:

```csharp
var workArea = MonitorWorkAreaService.GetForWindow(this);
var maxHeight = workArea.DipHeight - 20;
```

After assigning the new `Height`, replace the old DIP `Top` correction with:

```csharp
UpdateLayout();
MonitorWorkAreaService.ClampWindowToWorkArea(this);
```

- [ ] **Step 4: Run tests and Release build**

Run:

```powershell
dotnet test ShinCapture.sln
dotnet build ShinCapture.sln -c Release
```

Expected: all tests PASS and Release build succeeds with zero errors.

- [ ] **Step 5: Perform the focused manual matrix**

Run the app and verify:

```text
RememberLast: resize → capture small/large/scroll → history switch → same outer size
Maximized: capture → history switch → OCR open/close → remains maximized
FitToCapture: small and large captures still resize as before
Settings change: save each mode while editor is open → applies immediately
Mixed DPI: move editor to secondary monitor → no off-screen placement
```

Expected: every row matches the text; record any failure before continuing.

- [ ] **Step 6: Commit the settings and OCR behavior**

```powershell
git add src/ShinCapture/Views/SettingsWindow.xaml src/ShinCapture/Views/SettingsWindow.xaml.cs src/ShinCapture/Views/EditorWindow.xaml.cs
git commit -m "feat: 편집기 창 크기 모드 설정 UI 추가"
```

### Task 6: Final sizing verification

**Files:**
- Modify only if verification finds a sizing defect.

- [ ] **Step 1: Run the complete automated suite without `--no-restore`**

```powershell
dotnet test ShinCapture.sln -c Release
```

Expected: all tests PASS.

- [ ] **Step 2: Check whitespace and the final diff**

```powershell
git diff --check
git status --short
git diff --stat HEAD~5..HEAD
```

Expected: no whitespace errors; only sizing-feature files are modified/committed; pre-existing untracked user files remain untouched.

- [ ] **Step 3: Record verification evidence in the implementation handoff**

Include the test count, Release build result, and which DPI/manual cases were actually exercised. Do not claim unexecuted manual cases passed.
