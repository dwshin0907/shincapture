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
    private CaptureMode _lastCaptureMode = CaptureMode.Region;
    private bool _editorAutoTranslate;

    public MainWindow(SettingsManager settingsManager, SaveManager saveManager)
    {
        InitializeComponent();
        _settingsManager = settingsManager;
        _saveManager = saveManager;
        _settings = settingsManager.Load();
        _hotkeyManager = new HotkeyManager();
        _settingsManager.SettingsChanged += OnExternalSettingsChanged;

        Icon? trayIcon = null;
        try
        {
            var stream = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/icon.ico"))?.Stream;
            if (stream != null) trayIcon = new Icon(stream);
        }
        catch { }
        trayIcon ??= SystemIcons.Application;

        _trayIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "신캡쳐",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };
        _trayIcon.DoubleClick += (_, _) => ShowEditor();

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = v != null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "v?";

        _hotkeyManager.Initialize(this);
        RegisterHotkeys();
        PopulateCaptureGrid();

        // 시작 시 편집기 바로 열기
        ShowEditor();
        Hide(); // 메인 윈도우는 트레이로
    }

    private void RegisterHotkeys()
    {
        _hotkeyManager.UnregisterAll();

        // 설정에 따라 Windows의 PrtSc → Snipping Tool 바인딩을 해제/복원
        PrintScreenOverrideService.Apply(_settings.Hotkeys.OverridePrintScreen);

        // PrintScreen 등록
        int psResult = _hotkeyManager.Register(_settings.Hotkeys.RegionCapture, () => StartCapture(CaptureMode.Region));

        // 보조 핫키: PrintScreen이 안 되는 키보드(로지텍 등) 대응 + 사용자 커스터마이징 가능
        if (!string.IsNullOrWhiteSpace(_settings.Hotkeys.RegionCaptureAlt))
            _hotkeyManager.Register(_settings.Hotkeys.RegionCaptureAlt, () => StartCapture(CaptureMode.Region));

        _hotkeyManager.Register(_settings.Hotkeys.FreeformCapture, () => StartCapture(CaptureMode.Freeform));
        _hotkeyManager.Register(_settings.Hotkeys.WindowCapture, () => StartCapture(CaptureMode.Window));
        _hotkeyManager.Register(_settings.Hotkeys.ElementCapture, () => StartCapture(CaptureMode.Element));
        _hotkeyManager.Register(_settings.Hotkeys.FullscreenCapture, () => StartCapture(CaptureMode.Fullscreen));
        _hotkeyManager.Register(_settings.Hotkeys.ScrollCapture, () => StartCapture(CaptureMode.Scroll));
        _hotkeyManager.Register(_settings.Hotkeys.FixedSizeCapture, () => StartCapture(CaptureMode.FixedSize));
        _hotkeyManager.Register(_settings.Hotkeys.TextCapture, () => StartCapture(CaptureMode.Text));
        _hotkeyManager.Register(_settings.Hotkeys.TranslateCapture, () => StartCapture(CaptureMode.Translate));
    }

    private void StartCapture(CaptureMode mode)
    {
        _lastCaptureMode = mode;
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
            CaptureMode.Text => new RegionCaptureMode(),  // 영역 드래그 재사용, OCR 분기는 HandleCaptureResult
            _ => new RegionCaptureMode()
        };

        var overlay = new CaptureOverlay(_settings.Capture);
        overlay.Closed += (_, _) =>
        {
            if (overlay.Result != null)
            {
                HandleCaptureResult(overlay.Result);
            }
            else if (captureMode is ScrollCaptureMode scrollMode && scrollMode.IsComplete)
            {
                // Phase 2: 오버레이 닫힌 후 컨테이너 감지 + 스크롤 캡쳐
                System.Threading.Tasks.Task.Run(() => scrollMode.PerformScrollCapture())
                    .ContinueWith(_ =>
                    {
                        var stitched = scrollMode.GetStitchedBitmap();
                        if (stitched != null)
                            HandleCaptureResult(new CaptureResult
                            {
                                Image = stitched,
                                Region = new System.Drawing.Rectangle(0, 0, stitched.Width, stitched.Height)
                            });
                    }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            }
        };
        overlay.Start(captureMode);
    }

    private EditorWindow? _editorWindow;

    private void SubscribeCaptureRequested(EditorWindow editor)
    {
        editor.CaptureRequested += (mode, autoTranslate) =>
        {
            _editorAutoTranslate = autoTranslate;
            StartCapture(mode);
        };
    }

    private void HandleCaptureResult(CaptureResult result)
    {
        // 텍스트 캡쳐 모드: OCR → 클립보드(텍스트) → 토스트. 이미지 클립보드/편집기 분기 X.
        if (_lastCaptureMode == CaptureMode.Text)
        {
            RunOcrAndNotify(result.Image);
            return;
        }
        // 캡쳐 즉시 클립보드에 복사 (모든 모드 공통, PNG 형식만 — 자유형 알파 보존)
        BitmapHelper.SetClipboardPng(BitmapHelper.ToBitmapSource(result.Image));

        switch (_settings.Capture.AfterCapture)
        {
            case AfterCaptureAction.OpenEditor:
                bool autoOcr = (_lastCaptureMode == CaptureMode.Translate);
                bool autoTranslate = autoOcr && _editorAutoTranslate;
                _editorAutoTranslate = false;
                if (_editorWindow != null)
                {
                    _editorWindow.LoadNewCapture(result.Image, autoOcr, autoTranslate);
                    _editorWindow.Show();
                    _editorWindow.WindowState = WindowState.Normal;
                    _editorWindow.Topmost = true;
                    _editorWindow.Activate();
                    _editorWindow.Topmost = false;
                }
                else
                {
                    _editorWindow = new EditorWindow(result.Image, _saveManager, _settings, _settingsManager);
                    SubscribeCaptureRequested(_editorWindow);
                    if (autoOcr)
                    {
                        bool localAutoTranslate = autoTranslate;
                        _editorWindow.Loaded += (_, _) =>
                            _editorWindow.Dispatcher.BeginInvoke(
                                new Action(() => _editorWindow.TriggerAutoOcr(localAutoTranslate)),
                                System.Windows.Threading.DispatcherPriority.Background);
                    }
                    _editorWindow.Show();
                    _editorWindow.Activate();
                }
                break;
            case AfterCaptureAction.SaveDirectly:
                var savedPath = _saveManager.SaveAuto(result.Image, _settings);
                break;
            case AfterCaptureAction.ClipboardOnly:
                // 이미 위에서 복사됨
                break;
        }
    }

    private async void RunOcrAndNotify(System.Drawing.Bitmap image)
    {
        var langTag = Services.OcrService.ResolveLanguageOrFallback(_settings.Ocr.Language);
        if (langTag == null)
        {
            PromptInstallLanguagePack(_settings.Ocr.Language);
            return;
        }

        try
        {
            var text = await Services.OcrService.ExtractTextAsync(
                image, langTag, _settings.Ocr.UpscaleSmallImages);

            if (string.IsNullOrWhiteSpace(text))
            {
                _trayIcon.ShowBalloonTip(3000, "신캡쳐", "텍스트를 찾지 못했습니다", System.Windows.Forms.ToolTipIcon.Info);
                return;
            }

            System.Windows.Clipboard.SetText(text);
            var preview = text.Length > 40 ? text[..40] + "…" : text;
            _trayIcon.ShowBalloonTip(3000, "신캡쳐 — 텍스트 복사됨",
                $"{text.Length}자: {preview}", System.Windows.Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(4000, "신캡쳐 — OCR 실패",
                ex.Message, System.Windows.Forms.ToolTipIcon.Error);
        }
    }

    private void PromptInstallLanguagePack(string langTag)
    {
        var result = System.Windows.MessageBox.Show(
            $"OCR 언어팩이 설치되어 있지 않습니다 ({langTag}).\n\n" +
            "Windows 설정에서 언어팩을 설치하시겠습니까?\n" +
            "(예 → Windows 설정 '시간 및 언어 > 언어' 화면 열기)",
            "신캡쳐 — OCR 언어팩 필요",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Information);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:regionlanguage",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("✏ 영역지정 캡쳐\tPrtSc / Ctrl+Shift+C", null, (_, _) => StartCapture(CaptureMode.Region));
        menu.Items.Add("✧ 자유형 캡쳐\tCtrl+Shift+F", null, (_, _) => StartCapture(CaptureMode.Freeform));
        menu.Items.Add("☐ 창 캡쳐\tCtrl+Shift+W", null, (_, _) => StartCapture(CaptureMode.Window));
        menu.Items.Add("◫ 단위영역 캡쳐\tCtrl+Shift+D", null, (_, _) => StartCapture(CaptureMode.Element));
        menu.Items.Add("⊡ 전체화면 캡쳐\tCtrl+Shift+A", null, (_, _) => StartCapture(CaptureMode.Fullscreen));
        menu.Items.Add("↕ 스크롤 캡쳐\tCtrl+Shift+S", null, (_, _) => StartCapture(CaptureMode.Scroll));
        menu.Items.Add("⊞ 지정사이즈 캡쳐\tCtrl+Shift+Z", null, (_, _) => StartCapture(CaptureMode.FixedSize));
        menu.Items.Add("🔤 텍스트 캡쳐\tCtrl+Shift+T", null, (_, _) => StartCapture(CaptureMode.Text));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("✏ 편집기 열기", null, (_, _) => ShowEditor());
        menu.Items.Add("📁 저장 폴더 열기", null, (_, _) => OpenSaveFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("⚙︎ 환경설정", null, (_, _) => OpenSettings());
        menu.Items.Add("ℹ 신캡쳐 정보", null, (_, _) => ShowAbout());
        menu.Items.Add("🔑 API 키 발급 안내", null, (_, _) =>
        {
            var win = new Views.ApiKeyHelpWindow(_settingsManager);
            win.ShowDialog();
        });
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

    private void ShowEditor()
    {
        if (_editorWindow != null)
        {
            _editorWindow.Show();
            _editorWindow.WindowState = WindowState.Normal;
            _editorWindow.Topmost = true;
            _editorWindow.Activate();
            _editorWindow.Topmost = false;
        }
        else
        {
            // 빈 캔버스로 편집기 생성 (흰색 800x600)
            var blank = new System.Drawing.Bitmap(800, 600, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = System.Drawing.Graphics.FromImage(blank))
                g.Clear(System.Drawing.Color.White);
            _editorWindow = new EditorWindow(blank, _saveManager, _settings, _settingsManager);
            SubscribeCaptureRequested(_editorWindow);
            _editorWindow.Show();
            _editorWindow.Activate();
        }
    }

    private void OpenSaveFolder()
    {
        var path = _settings.Save.AutoSavePath;
        if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start("explorer.exe", path);
    }

    private void ShowAbout()
    {
        var w = new Window
        {
            Title = "신캡쳐 정보",
            Width = 420, Height = 340,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = (System.Windows.Media.Brush)FindResource("BackgroundPrimaryBrush")
        };

        var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(28, 20, 28, 20) };

        // 앱 이름
        sp.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "신캡쳐 (ShinCapture)",
            FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2)
        });
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var versionStr = v != null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "v?";
        sp.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = $"{versionStr} — 무료 스크린캡쳐 & 편집 도구",
            FontSize = 12, Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
            Margin = new Thickness(0, 0, 0, 16)
        });

        // 구분선
        sp.Children.Add(new System.Windows.Controls.Separator { Margin = new Thickness(0, 0, 0, 12) });

        // 개발자 정보
        sp.Children.Add(MakeInfoRow("개발", "신대우 수석"));
        sp.Children.Add(MakeInfoRow("필명", "웬비디아 / 스댕"));
        sp.Children.Add(MakeInfoRow("이메일", "popolong@naver.com", true));
        sp.Children.Add(MakeInfoRow("채널", "ChatGPT도 모르는 AI실전활용법"));

        // 네프콘 링크
        var link = new System.Windows.Controls.TextBlock { Margin = new Thickness(0, 4, 0, 16) };
        var hyperlink = new System.Windows.Documents.Hyperlink
        {
            NavigateUri = new Uri("https://contents.premium.naver.com/market/ai")
        };
        hyperlink.Inlines.Add("https://contents.premium.naver.com/market/ai");
        hyperlink.RequestNavigate += (_, args) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = args.Uri.ToString(), UseShellExecute = true
            });
        };
        link.Inlines.Add(hyperlink);
        sp.Children.Add(link);

        // 구분선
        sp.Children.Add(new System.Windows.Controls.Separator { Margin = new Thickness(0, 0, 0, 12) });

        sp.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "© 2026 신캡쳐. 광고 없는 깔끔한 캡쳐 도구.",
            FontSize = 11, Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush")
        });

        w.Content = sp;
        w.ShowDialog();
    }

    private static System.Windows.Controls.Grid MakeInfoRow(string label, string value, bool selectable = false)
    {
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(60) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lbl = new System.Windows.Controls.TextBlock
        {
            Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55))
        };
        System.Windows.Controls.Grid.SetColumn(lbl, 0);
        grid.Children.Add(lbl);

        if (selectable)
        {
            var tb = new System.Windows.Controls.TextBox
            {
                Text = value, FontSize = 12, IsReadOnly = true,
                BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent,
                Padding = new Thickness(0)
            };
            System.Windows.Controls.Grid.SetColumn(tb, 1);
            grid.Children.Add(tb);
        }
        else
        {
            var val = new System.Windows.Controls.TextBlock { Text = value, FontSize = 12 };
            System.Windows.Controls.Grid.SetColumn(val, 1);
            grid.Children.Add(val);
        }

        return grid;
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
            ("✏", "영역지정", "PrtSc / Ctrl+Shift+C", CaptureMode.Region),
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

    private void OnExternalSettingsChanged(object? sender, EventArgs e)
    {
        // SettingsManager.Save() raises this event after persisting to disk.
        // Marshal to UI thread (event source thread is whoever called Save).
        Dispatcher.Invoke(() =>
        {
            _settings = _settingsManager.Load();
            RegisterHotkeys();
        });
    }

    private void ExitApplication()
    {
        _editorWindow?.ForceClose();
        _editorWindow = null;
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _hotkeyManager.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}
