using System;
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

    private void OpenSettings() { /* Task 20 */ }
    private void OnSettingsClick(object sender, RoutedEventArgs e) => OpenSettings();

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _hotkeyManager.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}
