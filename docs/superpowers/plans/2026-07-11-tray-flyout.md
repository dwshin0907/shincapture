# Modern Tray Flyout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the dated emoji-based tray context menu with a polished WPF capture flyout while retaining a complete native high-contrast and failure fallback.

**Architecture:** Keep `NotifyIcon` only as the notification-area host. A pure catalog supplies labels, modes, icon keys, and live shortcuts; a pure physical-pixel positioner keeps the WPF flyout inside the cursor monitor; `TrayFlyoutWindow` owns presentation and raises commands back to `MainWindow`. The old menu path becomes an accessible text-only fallback instead of being deleted.

**Tech Stack:** C# 12, .NET 8, WPF, Windows Forms `NotifyIcon`, Win32 positioning/DWM, xUnit

**Dependency:** Execute `2026-07-11-editor-window-sizing.md` first so `NativeMethods` already contains monitor information types and `GetDpiForWindow`.

---

## File map

- Create `src/ShinCapture/Models/TrayMenuCommand.cs`: non-capture tray commands.
- Create `src/ShinCapture/Models/TrayCaptureAction.cs`: pure action descriptor.
- Create `src/ShinCapture/Services/TrayMenuCatalog.cs`: current action order and shortcut mapping.
- Create `tests/ShinCapture.Tests/Services/TrayMenuCatalogTests.cs`: completeness and live-shortcut tests.
- Create `src/ShinCapture/Services/TrayFlyoutPositioner.cs`: physical-pixel popup placement.
- Create `tests/ShinCapture.Tests/Services/TrayFlyoutPositionerTests.cs`: taskbar-edge and clamp cases.
- Create `src/ShinCapture/Views/Controls/TrayIconGeometryConverter.cs`: consistent 24×24 line geometries.
- Create `src/ShinCapture/Views/TrayFlyoutWindow.xaml`: branded WPF flyout layout and styles.
- Create `src/ShinCapture/Views/TrayFlyoutWindow.xaml.cs`: binding, focus, positioning, and command events.
- Modify `src/ShinCapture/Helpers/NativeMethods.cs`: point-monitor, foreground, position, and DWM APIs.
- Modify `src/ShinCapture/Views/MainWindow.xaml.cs`: right-click dispatch, fallback menu, and cleanup.
- Modify `src/ShinCapture/Themes/LightTheme.xaml`: tray-specific semantic brushes only if not kept local to the flyout.

### Task 1: Pure tray action catalog

**Files:**
- Create: `src/ShinCapture/Models/TrayMenuCommand.cs`
- Create: `src/ShinCapture/Models/TrayCaptureAction.cs`
- Create: `src/ShinCapture/Services/TrayMenuCatalog.cs`
- Test: `tests/ShinCapture.Tests/Services/TrayMenuCatalogTests.cs`

- [ ] **Step 1: Write failing catalog tests**

Create `tests/ShinCapture.Tests/Services/TrayMenuCatalogTests.cs`:

```csharp
using ShinCapture.Models;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class TrayMenuCatalogTests
{
    [Fact]
    public void CreateCaptureActions_ContainsEverySupportedCaptureExactlyOnce()
    {
        var actions = TrayMenuCatalog.CreateCaptureActions(new HotkeySettings());
        var expected = new[]
        {
            CaptureMode.Region, CaptureMode.Window, CaptureMode.Fullscreen,
            CaptureMode.Scroll, CaptureMode.SmartCut, CaptureMode.FixedSize,
            CaptureMode.Freeform, CaptureMode.Element, CaptureMode.Text,
            CaptureMode.Translate
        };

        Assert.Equal(expected, actions.Select(action => action.Mode));
        Assert.Equal(expected.Length, actions.Select(action => action.Mode).Distinct().Count());
        Assert.All(actions, action => Assert.False(string.IsNullOrWhiteSpace(action.IconKey)));
    }

    [Fact]
    public void CreateCaptureActions_UsesCurrentUserShortcuts()
    {
        var hotkeys = new HotkeySettings
        {
            RegionCapture = "Ctrl+Alt+1",
            TranslateCapture = "Ctrl+Alt+2"
        };

        var actions = TrayMenuCatalog.CreateCaptureActions(hotkeys);

        Assert.Equal("Ctrl+Alt+1", actions.Single(x => x.Mode == CaptureMode.Region).Shortcut);
        Assert.Equal("Ctrl+Alt+2", actions.Single(x => x.Mode == CaptureMode.Translate).Shortcut);
    }
}
```

- [ ] **Step 2: Run the focused tests and verify missing types**

```powershell
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~TrayMenuCatalogTests
```

Expected: FAIL at compile time because the tray catalog types do not exist.

- [ ] **Step 3: Add the pure action types**

Create `src/ShinCapture/Models/TrayMenuCommand.cs`:

```csharp
namespace ShinCapture.Models;

public enum TrayMenuCommand
{
    OpenEditor,
    OpenSaveFolder,
    OpenSettings,
    OpenApiKeyHelp,
    ShowAbout,
    Exit
}
```

Create `src/ShinCapture/Models/TrayCaptureAction.cs`:

```csharp
namespace ShinCapture.Models;

public sealed record TrayCaptureAction(
    CaptureMode Mode,
    string Label,
    string Shortcut,
    string IconKey,
    bool IsWide = false);
```

- [ ] **Step 4: Implement the ordered catalog**

Create `src/ShinCapture/Services/TrayMenuCatalog.cs`:

```csharp
using System.Collections.Generic;
using ShinCapture.Models;

namespace ShinCapture.Services;

public static class TrayMenuCatalog
{
    public static IReadOnlyList<TrayCaptureAction> CreateCaptureActions(HotkeySettings hotkeys)
        => new[]
        {
            new TrayCaptureAction(CaptureMode.Region, "영역 캡처", JoinPrimaryShortcuts(hotkeys), "region", true),
            new TrayCaptureAction(CaptureMode.Window, "창 캡처", hotkeys.WindowCapture, "window"),
            new TrayCaptureAction(CaptureMode.Fullscreen, "전체 화면", hotkeys.FullscreenCapture, "fullscreen"),
            new TrayCaptureAction(CaptureMode.Scroll, "스크롤", hotkeys.ScrollCapture, "scroll"),
            new TrayCaptureAction(CaptureMode.SmartCut, "스마트 컷", hotkeys.SmartCutCapture, "spark"),
            new TrayCaptureAction(CaptureMode.FixedSize, "지정 크기", hotkeys.FixedSizeCapture, "fixed"),
            new TrayCaptureAction(CaptureMode.Freeform, "자유형", hotkeys.FreeformCapture, "freeform"),
            new TrayCaptureAction(CaptureMode.Element, "단위 영역", hotkeys.ElementCapture, "element"),
            new TrayCaptureAction(CaptureMode.Text, "텍스트", hotkeys.TextCapture, "text"),
            new TrayCaptureAction(CaptureMode.Translate, "텍스트 + 번역", hotkeys.TranslateCapture, "translate", true)
        };

    private static string JoinPrimaryShortcuts(HotkeySettings hotkeys)
        => string.IsNullOrWhiteSpace(hotkeys.RegionCaptureAlt)
            ? hotkeys.RegionCapture
            : $"{hotkeys.RegionCapture}  ·  {hotkeys.RegionCaptureAlt}";
}
```

- [ ] **Step 5: Run tests and commit**

```powershell
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~TrayMenuCatalogTests
git add src/ShinCapture/Models/TrayMenuCommand.cs src/ShinCapture/Models/TrayCaptureAction.cs src/ShinCapture/Services/TrayMenuCatalog.cs tests/ShinCapture.Tests/Services/TrayMenuCatalogTests.cs
git commit -m "feat: 트레이 캡처 액션 카탈로그 추가"
```

Expected: focused tests PASS, then the commit succeeds.

### Task 2: Physical-pixel flyout positioning

**Files:**
- Create: `src/ShinCapture/Services/TrayFlyoutPositioner.cs`
- Test: `tests/ShinCapture.Tests/Services/TrayFlyoutPositionerTests.cs`

- [ ] **Step 1: Write failing placement tests**

Create `tests/ShinCapture.Tests/Services/TrayFlyoutPositionerTests.cs`:

```csharp
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class TrayFlyoutPositionerTests
{
    private static readonly PixelRect WorkArea = new(0, 0, 1920, 1040);
    private static readonly PixelSize Flyout = new(380, 560);

    [Fact]
    public void Calculate_OpensAboveBottomTaskbarCursor()
    {
        var result = TrayFlyoutPositioner.Calculate(new PixelPoint(1880, 1030), WorkArea, Flyout);

        Assert.True(result.Bottom <= WorkArea.Bottom - TrayFlyoutPositioner.Margin);
        Assert.True(result.Right <= WorkArea.Right - TrayFlyoutPositioner.Margin);
        Assert.True(result.Top < 1030);
    }

    [Fact]
    public void Calculate_OpensBelowTopTaskbarCursorWhenSpaceExists()
    {
        var result = TrayFlyoutPositioner.Calculate(new PixelPoint(1800, 10), WorkArea, Flyout);

        Assert.True(result.Top > 10);
        Assert.True(result.Bottom <= WorkArea.Bottom - TrayFlyoutPositioner.Margin);
    }

    [Fact]
    public void Calculate_ClampsInsideNegativeCoordinateMonitor()
    {
        var work = new PixelRect(-2560, -200, 2560, 1440);
        var result = TrayFlyoutPositioner.Calculate(new PixelPoint(-2500, 1100), work, Flyout);

        Assert.True(result.Left >= work.Left + TrayFlyoutPositioner.Margin);
        Assert.True(result.Right <= work.Right - TrayFlyoutPositioner.Margin);
        Assert.True(result.Top >= work.Top + TrayFlyoutPositioner.Margin);
        Assert.True(result.Bottom <= work.Bottom - TrayFlyoutPositioner.Margin);
    }
}
```

- [ ] **Step 2: Run tests and confirm the missing positioner failure**

```powershell
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~TrayFlyoutPositionerTests
```

Expected: FAIL at compile time.

- [ ] **Step 3: Implement the positioner**

Create `src/ShinCapture/Services/TrayFlyoutPositioner.cs`:

```csharp
using System;

namespace ShinCapture.Services;

public readonly record struct PixelPoint(int X, int Y);
public readonly record struct PixelSize(int Width, int Height);
public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

public static class TrayFlyoutPositioner
{
    public const int Margin = 8;
    private const int CursorGap = 10;

    public static PixelRect Calculate(
        PixelPoint cursor,
        PixelRect workArea,
        PixelSize flyout)
    {
        var availableWidth = Math.Max(1, workArea.Width - Margin * 2);
        var availableHeight = Math.Max(1, workArea.Height - Margin * 2);
        var width = Math.Min(flyout.Width, availableWidth);
        var height = Math.Min(flyout.Height, availableHeight);

        var x = cursor.X - width + 20;
        var y = cursor.Y + CursorGap;
        if (y + height > workArea.Bottom - Margin)
            y = cursor.Y - height - CursorGap;

        x = Math.Clamp(x, workArea.Left + Margin, workArea.Right - Margin - width);
        y = Math.Clamp(y, workArea.Top + Margin, workArea.Bottom - Margin - height);
        return new PixelRect(x, y, width, height);
    }
}
```

- [ ] **Step 4: Run tests and commit**

```powershell
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~TrayFlyoutPositionerTests
git add src/ShinCapture/Services/TrayFlyoutPositioner.cs tests/ShinCapture.Tests/Services/TrayFlyoutPositionerTests.cs
git commit -m "feat: 트레이 플라이아웃 배치 정책 추가"
```

Expected: all placement tests PASS.

### Task 3: WPF line-icon converter and flyout layout

**Files:**
- Create: `src/ShinCapture/Views/Controls/TrayIconGeometryConverter.cs`
- Create: `src/ShinCapture/Views/TrayFlyoutWindow.xaml`
- Create: `src/ShinCapture/Views/TrayFlyoutWindow.xaml.cs`

- [ ] **Step 1: Implement a single icon geometry source**

Create `src/ShinCapture/Views/Controls/TrayIconGeometryConverter.cs`:

```csharp
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ShinCapture.Views.Controls;

public sealed class TrayIconGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Geometry.Parse(value as string switch
        {
            "region" => "M3,8 V3 H8 M16,3 H21 V8 M21,16 V21 H16 M8,21 H3 V16",
            "window" => "M3,5 H21 V19 H3 Z M3,9 H21",
            "fullscreen" => "M3,9 V3 H9 M15,3 H21 V9 M21,15 V21 H15 M9,21 H3 V15",
            "scroll" => "M12,3 V21 M8,7 L12,3 16,7 M8,17 L12,21 16,17",
            "spark" => "M12,2 L14.2,9.8 22,12 14.2,14.2 12,22 9.8,14.2 2,12 9.8,9.8 Z",
            "fixed" => "M4,6 H20 V18 H4 Z M7,3 V6 M17,3 V6 M7,18 V21 M17,18 V21",
            "freeform" => "M4,17 C7,6 12,22 20,7 M17,7 H20 V10",
            "element" => "M3,4 H21 V20 H3 Z M7,8 H17 V16 H7 Z",
            "text" => "M5,5 H19 M12,5 V19 M8,19 H16",
            "translate" => "M4,5 H13 M8.5,3 V5 C8.5,10 6,13 3,15 M6,10 C8,13 10,14 12,15 M14,19 L18,9 22,19 M15.5,16 H20.5",
            "editor" => "M4,20 L8,19 19,8 16,5 5,16 Z M14,7 L17,10",
            "folder" => "M3,6 H10 L12,9 H21 V19 H3 Z",
            "settings" => "M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8 M12,2 V5 M12,19 V22 M2,12 H5 M19,12 H22 M5,5 L7,7 M17,17 L19,19 M19,5 L17,7 M7,17 L5,19",
            "help" => "M12,22 A10,10 0 1 0 12,2 A10,10 0 1 0 12,22 M9,9 C9,6 15,6 15,10 C15,12 12,12 12,15 M12,18 L12,18.1",
            "key" => "M14,7 A5,5 0 1 0 9,12 L12,15 H15 V18 H18 V21 H21 V16 L14,9",
            "exit" => "M10,4 H5 V20 H10 M14,8 L18,12 14,16 M8,12 H18",
            _ => "M4,4 H20 V20 H4 Z"
        });

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 2: Create the flyout XAML shell and reusable tile template**

Create `src/ShinCapture/Views/TrayFlyoutWindow.xaml` with this structure. Keep all colors semantic and all interactive items as `Button`:

```xml
<Window x:Class="ShinCapture.Views.TrayFlyoutWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:ShinCapture.Views.Controls"
        Title="신캡쳐 빠른 메뉴" Width="388" SizeToContent="Height"
        MaxHeight="620" WindowStyle="None" ResizeMode="NoResize"
        ShowInTaskbar="False" Topmost="True" Background="#FCFCFF"
        FontFamily="{DynamicResource AppFont}" PreviewKeyDown="OnPreviewKeyDown"
        Deactivated="OnDeactivated">
    <Window.Resources>
        <controls:TrayIconGeometryConverter x:Key="TrayIcon"/>
        <Style x:Key="TrayTile" TargetType="Button">
            <Setter Property="MinHeight" Value="54"/>
            <Setter Property="Margin" Value="3"/>
            <Setter Property="Padding" Value="12,9"/>
            <Setter Property="Background" Value="#F5F6FB"/>
            <Setter Property="BorderBrush" Value="#E7E8F0"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Card" Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="10" Padding="{TemplateBinding Padding}">
                            <ContentPresenter/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Card" Property="Background" Value="#EEF0FF"/>
                                <Setter TargetName="Card" Property="BorderBrush" Value="#C9CDFB"/>
                            </Trigger>
                            <Trigger Property="IsKeyboardFocused" Value="True">
                                <Setter TargetName="Card" Property="BorderBrush" Value="#5B5BD6"/>
                                <Setter TargetName="Card" Property="BorderThickness" Value="2"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="Card" Property="Background" Value="#E2E5FF"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key="TrayFooterButton" TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding" Value="8,7"/>
            <Setter Property="Foreground" Value="#55586B"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="FooterCard" Background="{TemplateBinding Background}"
                                CornerRadius="7" Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="FooterCard" Property="Background" Value="#EEF0F6"/>
                            </Trigger>
                            <Trigger Property="IsKeyboardFocused" Value="True">
                                <Setter TargetName="FooterCard" Property="Background" Value="#E7E9FF"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <DataTemplate x:Key="CaptureActionTemplate">
            <Button Style="{StaticResource TrayTile}" Tag="{Binding}"
                    Click="OnCaptureClick"
                    AutomationProperties.Name="{Binding Label}">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="28"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <Path Data="{Binding IconKey, Converter={StaticResource TrayIcon}}"
                          Stroke="#4C4F69" StrokeThickness="1.8"
                          StrokeStartLineCap="Round" StrokeEndLineCap="Round"
                          StrokeLineJoin="Round" Width="20" Height="20" Stretch="Uniform"/>
                    <StackPanel Grid.Column="1">
                        <TextBlock Text="{Binding Label}" FontSize="13" FontWeight="SemiBold"/>
                        <TextBlock Text="{Binding Shortcut}" FontSize="10" Foreground="#7B7E91"
                                   TextTrimming="CharacterEllipsis"/>
                    </StackPanel>
                </Grid>
            </Button>
        </DataTemplate>
    </Window.Resources>

    <Border BorderBrush="#D9DBE8" BorderThickness="1" Background="#FCFCFF"
            KeyboardNavigation.TabNavigation="Cycle"
            KeyboardNavigation.DirectionalNavigation="Contained">
        <Grid Margin="14">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <Grid Margin="4,2,4,12">
                <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                <StackPanel>
                    <TextBlock Text="신캡쳐" FontSize="18" FontWeight="Bold" Foreground="#171827"/>
                    <TextBlock x:Name="WindowModeText" FontSize="11" Foreground="#72758A"/>
                </StackPanel>
                <Border Grid.Column="1" Background="#E9F9F3" CornerRadius="10" Padding="9,4"
                        VerticalAlignment="Top">
                    <TextBlock Text="●  준비됨" Foreground="#17805C" FontSize="10" FontWeight="SemiBold"/>
                </Border>
            </Grid>

            <ContentControl x:Name="PrimaryActionHost" Grid.Row="1"
                            ContentTemplate="{StaticResource CaptureActionTemplate}"/>

            <ItemsControl x:Name="SecondaryActions" Grid.Row="2" Margin="0,2"
                          ItemTemplate="{StaticResource CaptureActionTemplate}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate><UniformGrid Columns="2"/></ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
            </ItemsControl>

            <ContentControl x:Name="TranslateActionHost" Grid.Row="3"
                            ContentTemplate="{StaticResource CaptureActionTemplate}"/>

            <StackPanel Grid.Row="4" Margin="3,10,3,0">
                <Separator Margin="0,0,0,9" Background="#E4E5EC"/>
                <UniformGrid Columns="2" Margin="0,0,0,8">
                    <Button Style="{StaticResource TrayTile}" Tag="OpenEditor" Click="OnCommandClick" Content="편집기 열기"/>
                    <Button Style="{StaticResource TrayTile}" Tag="OpenSaveFolder" Click="OnCommandClick" Content="저장 폴더"/>
                </UniformGrid>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition/><ColumnDefinition/><ColumnDefinition/><ColumnDefinition/>
                    </Grid.ColumnDefinitions>
                    <Button Grid.Column="0" Style="{StaticResource TrayFooterButton}" Content="설정" Tag="OpenSettings" Click="OnCommandClick"/>
                    <Button Grid.Column="1" Style="{StaticResource TrayFooterButton}" Content="API 키" Tag="OpenApiKeyHelp" Click="OnCommandClick"/>
                    <Button Grid.Column="2" Style="{StaticResource TrayFooterButton}" Content="정보" Tag="ShowAbout" Click="OnCommandClick"/>
                    <Button Grid.Column="3" Style="{StaticResource TrayFooterButton}" Content="종료" Tag="Exit" Click="OnCommandClick" Foreground="#B42318"/>
                </Grid>
            </StackPanel>
        </Grid>
    </Border>
</Window>
```

- [ ] **Step 3: Add binding and command behavior in code-behind**

Create `src/ShinCapture/Views/TrayFlyoutWindow.xaml.cs` initially with the non-positioning behavior:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShinCapture.Models;
using ShinCapture.Services;

namespace ShinCapture.Views;

public partial class TrayFlyoutWindow : Window
{
    public event Action<CaptureMode>? CaptureRequested;
    public event Action<TrayMenuCommand>? CommandRequested;

    public ObservableCollection<TrayCaptureAction> SecondaryCaptureActions { get; } = new();

    public TrayFlyoutWindow()
    {
        InitializeComponent();
        SecondaryActions.ItemsSource = SecondaryCaptureActions;
    }

    public void UpdateSettings(AppSettings settings)
    {
        var actions = TrayMenuCatalog.CreateCaptureActions(settings.Hotkeys);
        PrimaryActionHost.Content = actions.Single(x => x.Mode == CaptureMode.Region);
        TranslateActionHost.Content = actions.Single(x => x.Mode == CaptureMode.Translate);
        SecondaryCaptureActions.Clear();
        foreach (var action in actions.Where(x => !x.IsWide))
            SecondaryCaptureActions.Add(action);
        WindowModeText.Text = settings.Editor.WindowSizeMode switch
        {
            EditorWindowSizeMode.Maximized => "편집기 크기 · 현재 모니터에 최대화",
            EditorWindowSizeMode.FitToCapture => "편집기 크기 · 캡처 이미지에 맞춤",
            _ => "편집기 크기 · 마지막 크기 유지"
        };
    }

    private void OnCaptureClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TrayCaptureAction action) return;
        Hide();
        Dispatcher.BeginInvoke(new Action(() => CaptureRequested?.Invoke(action.Mode)));
    }

    private void OnCommandClick(object sender, RoutedEventArgs e)
    {
        if (!Enum.TryParse((sender as FrameworkElement)?.Tag?.ToString(), out TrayMenuCommand command)) return;
        Hide();
        Dispatcher.BeginInvoke(new Action(() => CommandRequested?.Invoke(command)));
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Hide();
        e.Handled = true;
    }

    private void OnDeactivated(object? sender, EventArgs e) => Hide();
}
```

- [ ] **Step 4: Build to validate XAML and bindings**

```powershell
dotnet build ShinCapture.sln
```

Expected: build succeeds. Fix XAML parser errors before continuing; do not defer them to MainWindow integration.

- [ ] **Step 5: Commit the visual shell**

```powershell
git add src/ShinCapture/Views/Controls/TrayIconGeometryConverter.cs src/ShinCapture/Views/TrayFlyoutWindow.xaml src/ShinCapture/Views/TrayFlyoutWindow.xaml.cs
git commit -m "feat: WPF 트레이 플라이아웃 UI 추가"
```

### Task 4: Win32 positioning, focus, and rounded corners

**Files:**
- Modify: `src/ShinCapture/Helpers/NativeMethods.cs`
- Modify: `src/ShinCapture/Views/TrayFlyoutWindow.xaml.cs`

- [ ] **Step 1: Add required Win32 declarations**

Add to `NativeMethods`:

```csharp
public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
public const int DWMWCP_ROUND = 2;

[DllImport("user32.dll")]
public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

[DllImport("user32.dll")]
public static extern bool SetForegroundWindow(IntPtr hWnd);

[DllImport("dwmapi.dll")]
public static extern int DwmSetWindowAttribute(
    IntPtr hwnd, int attribute, ref int value, int valueSize);
```

- [ ] **Step 2: Add `ShowNearCursor` to the flyout**

Add these usings:

```csharp
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using ShinCapture.Helpers;
```

Add this method:

```csharp
public void ShowNearCursor()
{
    if (!NativeMethods.GetCursorPos(out var cursor))
        throw new InvalidOperationException("Unable to read the tray cursor position.");

    if (!IsVisible) Show();
    UpdateLayout();

    var hwnd = new WindowInteropHelper(this).Handle;
    var monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
    var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
    if (hwnd == IntPtr.Zero || monitor == IntPtr.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info))
        throw new InvalidOperationException("Unable to resolve the tray monitor work area.");

    var dpi = NativeMethods.GetDpiForWindow(hwnd);
    var scale = dpi > 0 ? dpi / 96d : 1d;
    var desired = new PixelSize(
        Math.Max(1, (int)Math.Ceiling(ActualWidth * scale)),
        Math.Max(1, (int)Math.Ceiling(ActualHeight * scale)));
    var workArea = new PixelRect(
        info.rcWork.Left, info.rcWork.Top, info.rcWork.Width, info.rcWork.Height);
    var target = TrayFlyoutPositioner.Calculate(
        new PixelPoint(cursor.X, cursor.Y), workArea, desired);

    NativeMethods.SetWindowPos(hwnd, new IntPtr(-1),
        target.Left, target.Top, target.Width, target.Height, 0);
    var corner = NativeMethods.DWMWCP_ROUND;
    _ = NativeMethods.DwmSetWindowAttribute(hwnd,
        NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
        ref corner, sizeof(int));
    NativeMethods.SetForegroundWindow(hwnd);
    Activate();
    Dispatcher.BeginInvoke(new Action(() =>
        MoveFocus(new TraversalRequest(FocusNavigationDirection.First))),
        DispatcherPriority.Input);
}
```

Do not use `SWP_NOACTIVATE`; the flyout needs keyboard focus. Keep `MoveFocus` rather than adding brittle visual-tree recursion into the data template.

- [ ] **Step 3: Make DWM failure non-fatal**

Wrap only `DwmSetWindowAttribute` in a `try/catch (DllNotFoundException)` and `try/catch (EntryPointNotFoundException)`. Keep monitor/position failures visible to the caller so `MainWindow` can invoke the native fallback.

- [ ] **Step 4: Build and run position tests**

```powershell
dotnet build ShinCapture.sln
dotnet test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter FullyQualifiedName~TrayFlyoutPositionerTests
```

Expected: build succeeds and all position tests PASS.

- [ ] **Step 5: Commit positioning**

```powershell
git add src/ShinCapture/Helpers/NativeMethods.cs src/ShinCapture/Views/TrayFlyoutWindow.xaml.cs
git commit -m "feat: 트레이 플라이아웃 DPI 배치와 포커스 추가"
```

### Task 5: `MainWindow` integration and native fallback

**Files:**
- Modify: `src/ShinCapture/Views/MainWindow.xaml.cs`

- [ ] **Step 1: Replace the automatic ContextMenuStrip attachment**

Add fields:

```csharp
private ContextMenuStrip _nativeTrayMenu;
private TrayFlyoutWindow? _trayFlyout;
```

Before constructing `_trayIcon`, initialize:

```csharp
_nativeTrayMenu = BuildNativeTrayMenu();
```

Remove `ContextMenuStrip = BuildTrayMenu()` from the `NotifyIcon` initializer. Add:

```csharp
_trayIcon.MouseUp += OnTrayMouseUp;
```

- [ ] **Step 2: Replace `BuildTrayMenu` with the complete text fallback**

Rename it to `BuildNativeTrayMenu` and use the catalog so shortcuts are live:

```csharp
private ContextMenuStrip BuildNativeTrayMenu()
{
    var menu = new ContextMenuStrip
    {
        ShowImageMargin = false,
        ShowCheckMargin = false
    };
    foreach (var action in TrayMenuCatalog.CreateCaptureActions(_settings.Hotkeys))
    {
        var item = new ToolStripMenuItem(action.Label)
        {
            ShortcutKeyDisplayString = action.Shortcut
        };
        item.Click += (_, _) => StartCapture(action.Mode);
        menu.Items.Add(item);
    }
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add("편집기 열기", null, (_, _) => ShowEditor());
    menu.Items.Add("저장 폴더 열기", null, (_, _) => OpenSaveFolder());
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add("환경설정", null, (_, _) => OpenSettings());
    menu.Items.Add("API 키 발급 안내", null, (_, _) => OpenApiKeyHelp());
    menu.Items.Add("신캡쳐 정보", null, (_, _) => ShowAbout());
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add("종료", null, (_, _) => ExitApplication());
    return menu;
}
```

Extract the current API key lambda into:

```csharp
private void OpenApiKeyHelp()
{
    var win = new ApiKeyHelpWindow(_settingsManager);
    win.ShowDialog();
}
```

- [ ] **Step 3: Add WPF flyout creation and command routing**

```csharp
private void OnTrayMouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
{
    if (e.Button != System.Windows.Forms.MouseButtons.Right) return;
    Dispatcher.BeginInvoke(new Action(ShowTrayFlyout));
}

private void ShowTrayFlyout()
{
    if (SystemParameters.HighContrast)
    {
        _nativeTrayMenu.Show(System.Windows.Forms.Cursor.Position);
        return;
    }

    try
    {
        _trayFlyout ??= CreateTrayFlyout();
        _trayFlyout.UpdateSettings(_settings);
        _trayFlyout.ShowNearCursor();
    }
    catch
    {
        _trayFlyout?.Hide();
        _nativeTrayMenu.Show(System.Windows.Forms.Cursor.Position);
    }
}

private TrayFlyoutWindow CreateTrayFlyout()
{
    var flyout = new TrayFlyoutWindow();
    flyout.CaptureRequested += StartCapture;
    flyout.CommandRequested += command =>
    {
        switch (command)
        {
            case TrayMenuCommand.OpenEditor: ShowEditor(); break;
            case TrayMenuCommand.OpenSaveFolder: OpenSaveFolder(); break;
            case TrayMenuCommand.OpenSettings: OpenSettings(); break;
            case TrayMenuCommand.OpenApiKeyHelp: OpenApiKeyHelp(); break;
            case TrayMenuCommand.ShowAbout: ShowAbout(); break;
            case TrayMenuCommand.Exit: ExitApplication(); break;
        }
    };
    return flyout;
}
```

- [ ] **Step 4: Refresh fallback data and dispose both menu paths**

At the end of `OnExternalSettingsChanged` and after the explicit settings dialog reload, call a new method:

```csharp
private void RefreshTrayMenus()
{
    var oldMenu = _nativeTrayMenu;
    _nativeTrayMenu = BuildNativeTrayMenu();
    oldMenu.Dispose();
    _trayFlyout?.UpdateSettings(_settings);
}
```

In `ExitApplication()` add before disposing `_trayIcon`:

```csharp
_trayFlyout?.Close();
_trayFlyout = null;
_nativeTrayMenu.Dispose();
```

- [ ] **Step 5: Build and run the complete suite**

```powershell
dotnet build ShinCapture.sln -c Release
dotnet test ShinCapture.sln -c Release
```

Expected: Release build succeeds and all tests PASS.

- [ ] **Step 6: Commit integration**

```powershell
git add src/ShinCapture/Views/MainWindow.xaml.cs
git commit -m "feat: 트레이 우클릭을 WPF 플라이아웃으로 교체"
```

### Task 6: Visual, accessibility, and fallback QA

**Files:**
- Modify only the flyout XAML/code if QA exposes a defect.

- [ ] **Step 1: Exercise interaction behavior**

Verify each row:

```text
Right click tray → flyout opens once near cursor
Right click repeatedly → same instance moves, no duplicate windows
Escape → closes
Click elsewhere → closes
Tab/Shift+Tab → visits every command with visible focus
Enter/Space → invokes focused command once
Capture command → flyout hides before overlay starts
Custom shortcut change → next open shows new shortcut
Smart Cut and Translate → both are present and launch
```

- [ ] **Step 2: Exercise placement matrix**

Test taskbar on bottom, top, left, and right when available; test a secondary monitor and at least one non-100% DPI. Expected: the full flyout remains inside that monitor's work area.

- [ ] **Step 3: Exercise accessibility fallback**

Enable Windows high contrast, right-click the tray icon, and confirm the system-colored native text menu appears with every action and current shortcut.

- [ ] **Step 4: Inspect the flyout at 100%, 150%, and 200%**

Expected: no clipped labels, 2-column tiles remain aligned, shortcuts ellipsize instead of wrapping, and the overall height remains under 620 DIP.

- [ ] **Step 5: Run final verification and diff checks**

```powershell
dotnet test ShinCapture.sln -c Release
git diff --check
git status --short
```

Expected: all tests PASS, no whitespace errors, and pre-existing untracked user files remain untouched.
