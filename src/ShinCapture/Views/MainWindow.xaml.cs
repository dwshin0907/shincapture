using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using ShinCapture.Capture;
using ShinCapture.Helpers;
using ShinCapture.Models;
using ShinCapture.Services;
using ShinCapture.Views.Overlay;

namespace ShinCapture.Views;

public partial class MainWindow : Window
{
    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyManager _hotkeyManager;
    private readonly SettingsManager _settingsManager;
    private readonly SaveManager _saveManager;
    private AppSettings _settings;

    public MainWindow(SettingsManager settingsManager, SaveManager saveManager)
    {
        InitializeComponent();
        _settingsManager = settingsManager;
        _saveManager = saveManager;
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
        PopulateCaptureGrid();
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
        if (mode == CaptureMode.Fullscreen)
        {
            var bitmap = ScreenHelper.CaptureFullScreen();
            var result = new CaptureResult
            {
                Image = bitmap,
                Region = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height)
            };
            HandleCaptureResult(result);
            return;
        }

        ICaptureMode captureMode = mode switch
        {
            CaptureMode.Region    => new RegionCaptureMode(),
            CaptureMode.Freeform  => new FreeformCaptureMode(),
            CaptureMode.Window    => new WindowCaptureMode(),
            CaptureMode.Element   => new ElementCaptureMode(),
            CaptureMode.Fullscreen => new FullscreenCaptureMode(),
            CaptureMode.Scroll    => new ScrollCaptureMode(),
            CaptureMode.FixedSize => new FixedSizeCaptureMode(
                _settings.FixedSizes?.FirstOrDefault()?.Width  ?? 1280,
                _settings.FixedSizes?.FirstOrDefault()?.Height ?? 720),
            _ => new RegionCaptureMode()
        };

        var overlay = new CaptureOverlay(_settings.Capture);
        overlay.Closed += (_, _) =>
        {
            if (overlay.Result != null)
            {
                HandleCaptureResult(overlay.Result);
            }
            else if (captureMode is ScrollCaptureMode scrollMode
                     && scrollMode.GetStitchedBitmap() is { } stitched)
            {
                HandleCaptureResult(new CaptureResult
                {
                    Image  = stitched,
                    Region = new System.Drawing.Rectangle(0, 0, stitched.Width, stitched.Height)
                });
            }
        };
        overlay.Start(captureMode);
    }

    private void HandleCaptureResult(CaptureResult result)
    {
        switch (_settings.Capture.AfterCapture)
        {
            case AfterCaptureAction.OpenEditor:
                var editor = new EditorWindow(result.Image, _saveManager, _settings);
                editor.Show();
                break;
            case AfterCaptureAction.SaveDirectly:
                var savedPath = _saveManager.SaveAuto(result.Image, _settings);
                break;
            case AfterCaptureAction.ClipboardOnly:
                System.Windows.Clipboard.SetImage(BitmapHelper.ToBitmapSource(result.Image));
                break;
        }
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
        if (Visibility == Visibility.Visible) { Hide(); }
        else { Show(); WindowState = WindowState.Normal; Activate(); }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_settings.General.MinimizeToTray) { e.Cancel = true; Hide(); }
    }

    private void OpenSaveFolder()
    {
        var path = _settings.Save.AutoSavePath;
        if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(_settingsManager);
        window.Owner = this;
        window.ShowDialog();
        // Reload settings and re-register hotkeys
        _settings = _settingsManager.Load();
        RegisterHotkeys();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettings();

    private void PopulateCaptureGrid()
    {
        CaptureModesPanel.Items.Clear();
        var modes = new (string icon, string name, string shortcut, CaptureMode mode)[]
        {
            ("✏", "영역지정", "PrtSc",        CaptureMode.Region),
            ("✧", "자유형",   "Ctrl+Shift+F", CaptureMode.Freeform),
            ("☐", "창 캡쳐",  "Ctrl+Shift+W", CaptureMode.Window),
            ("◫", "단위영역", "Ctrl+Shift+D", CaptureMode.Element),
            ("⊡", "전체화면", "Ctrl+Shift+A", CaptureMode.Fullscreen),
            ("↕", "스크롤",   "Ctrl+Shift+S", CaptureMode.Scroll),
            ("⊞", "지정사이즈", "Ctrl+Shift+Z", CaptureMode.FixedSize),
        };

        foreach (var (icon, name, shortcut, captureMode) in modes)
        {
            var iconBlock = new System.Windows.Controls.TextBlock
            {
                Text = icon,
                FontSize = 18,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            var nameBlock = new System.Windows.Controls.TextBlock
            {
                Text = name,
                FontSize = 11,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush")
            };
            var shortcutBlock = new System.Windows.Controls.TextBlock
            {
                Text = shortcut,
                FontSize = 10,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("TextDisabledBrush")
            };

            var stack = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Vertical,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };
            stack.Children.Add(iconBlock);
            stack.Children.Add(nameBlock);
            stack.Children.Add(shortcutBlock);

            var btn = new System.Windows.Controls.Button
            {
                Content = stack,
                Padding = new Thickness(4, 8, 4, 8),
                Margin = new Thickness(3),
                BorderThickness = new Thickness(1),
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                Background = (System.Windows.Media.Brush)FindResource("BackgroundSecondaryBrush"),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            var localMode = captureMode;
            btn.Click += (_, _) => StartCapture(localMode);
            CaptureModesPanel.Items.Add(btn);
        }
    }

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _hotkeyManager.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}
