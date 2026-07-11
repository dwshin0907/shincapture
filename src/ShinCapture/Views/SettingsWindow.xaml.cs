using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ShinCapture.Models;
using ShinCapture.Services;
using ShinCapture.Services.Ai;
using System.Windows.Controls;
using System.Windows.Media;
using ShinCapture.Services.Hotkeys;
using ShinCapture.Views.Controls;

namespace ShinCapture.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private AppSettings _settings;
    private ObservableCollection<FixedSizePreset> _fixedSizes = new();
    private readonly DpapiCredentialStore _aiStore = new();
    private readonly HotkeyManager? _hotkeyManager;
    private readonly System.Collections.Generic.List<HotkeyRow> _hotkeyRows = new();

    private sealed class HotkeyDef
    {
        public string Label = "";
        public bool Advanced;
        public Func<HotkeySettings, string> Get = _ => "";
        public Action<HotkeySettings, string> Set = (_, _) => { };
    }

    private sealed class HotkeyRow
    {
        public HotkeyDef Def = null!;
        public HotkeyCaptureBox Box = null!;
        public TextBlock Badge = null!;
        public TextBlock Suggestion = null!;
        public string? SuggestedValue;
    }

    private static readonly HotkeyDef[] HotkeyDefs =
    {
        new() { Label = "영역지정",      Get = h => h.RegionCapture,     Set = (h, v) => h.RegionCapture = v },
        new() { Label = "영역지정 (보조)", Get = h => h.RegionCaptureAlt,  Set = (h, v) => h.RegionCaptureAlt = v },
        new() { Label = "전체화면",      Get = h => h.FullscreenCapture, Set = (h, v) => h.FullscreenCapture = v },
        new() { Label = "창 캡쳐",       Get = h => h.WindowCapture,     Set = (h, v) => h.WindowCapture = v },
        new() { Label = "스크롤",        Get = h => h.ScrollCapture,     Set = (h, v) => h.ScrollCapture = v },
        new() { Label = "스마트 컷",     Get = h => h.SmartCutCapture,   Set = (h, v) => h.SmartCutCapture = v },
        new() { Advanced = true, Label = "자유형",     Get = h => h.FreeformCapture,  Set = (h, v) => h.FreeformCapture = v },
        new() { Advanced = true, Label = "단위영역",   Get = h => h.ElementCapture,   Set = (h, v) => h.ElementCapture = v },
        new() { Advanced = true, Label = "지정사이즈", Get = h => h.FixedSizeCapture, Set = (h, v) => h.FixedSizeCapture = v },
        new() { Advanced = true, Label = "텍스트 캡쳐", Get = h => h.TextCapture,      Set = (h, v) => h.TextCapture = v },
        new() { Advanced = true, Label = "텍스트+번역", Get = h => h.TranslateCapture, Set = (h, v) => h.TranslateCapture = v },
    };

    public SettingsWindow(SettingsManager settingsManager, HotkeyManager? hotkeyManager = null, int initialTabIndex = 0)
    {
        InitializeComponent();
        _settingsManager = settingsManager;
        // 명시 전달이 없으면 앱 전역 인스턴스를 사용(편집기/도움말 경로에서도 동작).
        _hotkeyManager = hotkeyManager ?? HotkeyManager.Current;
        _settings = settingsManager.Load();

        // 설정창이 열려 있는 동안은 전역 단축키를 일시 해제한다(어느 경로로 열리든).
        // → 이미 쓰는 조합도 입력칸에 들어오고, OS 충돌 프로브가 자기 자신과 충돌하지 않음.
        _hotkeyManager?.Suspend();
        Closed += (_, _) => { if (DialogResult != true) _hotkeyManager?.Resume(); };

        BuildHotkeyRows();
        LoadSettings();
        if (initialTabIndex >= 0 && SettingsTabControl != null
            && initialTabIndex < SettingsTabControl.Items.Count)
        {
            SettingsTabControl.SelectedIndex = initialTabIndex;
        }
    }

    private void LoadSettings()
    {
        // 일반
        ChkAutoStart.IsChecked = _settings.General.AutoStart;
        ChkMinimizeToTray.IsChecked = _settings.General.MinimizeToTray;

        // 캡쳐
        SelectComboByTag(CmbAfterCapture, _settings.Capture.AfterCapture switch
        {
            AfterCaptureAction.OpenEditor   => "OpenEditor",
            AfterCaptureAction.SaveDirectly => "SaveDirectly",
            AfterCaptureAction.ClipboardOnly => "ClipboardOnly",
            _ => "OpenEditor"
        });
        switch (_settings.Editor.WindowSizeMode)
        {
            case EditorWindowSizeMode.Maximized:
                RbEditorMaximized.IsChecked = true;
                break;
            case EditorWindowSizeMode.FitToCapture:
                RbEditorFit.IsChecked = true;
                break;
            default:
                RbEditorRemember.IsChecked = true;
                break;
        }
        SldMagnifierZoom.Value = _settings.Capture.MagnifierZoom;
        ChkShowCrosshair.IsChecked = _settings.Capture.ShowCrosshair;
        ChkShowColorCode.IsChecked = _settings.Capture.ShowColorCode;

        // 저장
        SelectComboByTag(CmbDefaultFormat, _settings.Save.DefaultFormat.ToLower());
        SldJpgQuality.Value = _settings.Save.JpgQuality;
        TxtAutoSavePath.Text = _settings.Save.AutoSavePath;
        TxtFileNamePattern.Text = _settings.Save.FileNamePattern;
        ChkAutoSave.IsChecked = _settings.Save.AutoSave;
        ChkCopyToClipboard.IsChecked = _settings.Save.CopyToClipboard;

        // 단축키 — 행 값 채우기 (행은 생성자에서 BuildHotkeyRows로 생성됨)
        foreach (var row in _hotkeyRows)
            row.Box.Value = row.Def.Get(_settings.Hotkeys);
        ChkOverridePrintScreen.IsChecked = _settings.Hotkeys.OverridePrintScreen;
        RefreshHotkeyConflicts();

        // OCR 언어 드롭다운 채우기
        PopulateOcrLanguages(_settings.Ocr.Language);
        ChkOcrUpscale.IsChecked = _settings.Ocr.UpscaleSmallImages;

        // 지정사이즈
        _fixedSizes = new ObservableCollection<FixedSizePreset>(
            _settings.FixedSizes?.Select(p => new FixedSizePreset
            {
                Name   = p.Name,
                Width  = p.Width,
                Height = p.Height
            }) ?? Enumerable.Empty<FixedSizePreset>());
        GridFixedSizes.ItemsSource = _fixedSizes;

        // AI
        AiEnabledCheckBox.IsChecked = _settings.Ai.Enabled;
        AiModelBox.Text = _settings.Ai.Model;
        foreach (System.Windows.Controls.ComboBoxItem item in AiTargetLangBox.Items)
        {
            if ((string)item.Tag == _settings.Ai.TargetLanguage)
            {
                AiTargetLangBox.SelectedItem = item;
                break;
            }
        }
        if (AiTargetLangBox.SelectedItem == null && AiTargetLangBox.Items.Count > 0)
            AiTargetLangBox.SelectedIndex = 0;
        UpdateAiKeyStatus();
    }

    private void PopulateOcrLanguages(string selectedTag)
    {
        CmbOcrLanguage.Items.Clear();
        var langs = ShinCapture.Services.OcrService.GetAvailableLanguages();
        foreach (var tag in langs)
        {
            string display;
            try { display = $"{System.Globalization.CultureInfo.GetCultureInfo(tag).DisplayName} ({tag})"; }
            catch { display = tag; }
            var item = new System.Windows.Controls.ComboBoxItem { Content = display, Tag = tag };
            CmbOcrLanguage.Items.Add(item);
            if (string.Equals(tag, selectedTag, StringComparison.OrdinalIgnoreCase))
                CmbOcrLanguage.SelectedItem = item;
        }
        if (CmbOcrLanguage.SelectedItem == null && CmbOcrLanguage.Items.Count > 0)
            CmbOcrLanguage.SelectedIndex = 0;
        if (CmbOcrLanguage.Items.Count == 0)
        {
            CmbOcrLanguage.Items.Add(new System.Windows.Controls.ComboBoxItem
            {
                Content = "(설치된 OCR 언어 없음)", Tag = ""
            });
            CmbOcrLanguage.SelectedIndex = 0;
        }
    }

    private void ApplyToSettings()
    {
        // 일반
        _settings.General.AutoStart       = ChkAutoStart.IsChecked == true;
        _settings.General.MinimizeToTray  = ChkMinimizeToTray.IsChecked == true;

        // 캡쳐
        _settings.Capture.AfterCapture = GetComboTag(CmbAfterCapture) switch
        {
            "SaveDirectly"  => AfterCaptureAction.SaveDirectly,
            "ClipboardOnly" => AfterCaptureAction.ClipboardOnly,
            _               => AfterCaptureAction.OpenEditor
        };
        _settings.Editor.WindowSizeMode = RbEditorMaximized.IsChecked == true
            ? EditorWindowSizeMode.Maximized
            : RbEditorFit.IsChecked == true
                ? EditorWindowSizeMode.FitToCapture
                : EditorWindowSizeMode.RememberLast;
        _settings.Capture.MagnifierZoom  = (int)SldMagnifierZoom.Value;
        _settings.Capture.ShowCrosshair  = ChkShowCrosshair.IsChecked == true;
        _settings.Capture.ShowColorCode  = ChkShowColorCode.IsChecked == true;

        // 저장
        _settings.Save.DefaultFormat   = GetComboTag(CmbDefaultFormat) ?? "png";
        _settings.Save.JpgQuality      = (int)SldJpgQuality.Value;
        _settings.Save.AutoSavePath    = TxtAutoSavePath.Text;
        _settings.Save.FileNamePattern = TxtFileNamePattern.Text;
        _settings.Save.AutoSave        = ChkAutoSave.IsChecked == true;
        _settings.Save.CopyToClipboard = ChkCopyToClipboard.IsChecked == true;

        // 단축키
        foreach (var row in _hotkeyRows)
            row.Def.Set(_settings.Hotkeys, row.Box.Value);
        _settings.Hotkeys.OverridePrintScreen = ChkOverridePrintScreen.IsChecked == true;

        var ocrLangTag = (CmbOcrLanguage.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString();
        if (!string.IsNullOrEmpty(ocrLangTag))
            _settings.Ocr.Language = ocrLangTag;
        _settings.Ocr.UpscaleSmallImages = ChkOcrUpscale.IsChecked == true;

        // AI
        _settings.Ai.Enabled = AiEnabledCheckBox.IsChecked == true;
        _settings.Ai.Model = string.IsNullOrWhiteSpace(AiModelBox.Text) ? "gpt-4o-mini" : AiModelBox.Text.Trim();
        if (AiTargetLangBox.SelectedItem is System.Windows.Controls.ComboBoxItem aiItem && aiItem.Tag is string aiTag)
            _settings.Ai.TargetLanguage = aiTag;

        // 지정사이즈
        _settings.FixedSizes = _fixedSizes.ToList();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        ApplyToSettings();
        _settingsManager.Save(_settings);
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnBrowsePath(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "자동 저장 경로 선택",
            SelectedPath = TxtAutoSavePath.Text
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtAutoSavePath.Text = dialog.SelectedPath;
    }

    private void OnAddFixedSize(object sender, RoutedEventArgs e)
    {
        _fixedSizes.Add(new FixedSizePreset { Name = "새 사이즈", Width = 800, Height = 600 });
    }

    private void OnDeleteFixedSize(object sender, RoutedEventArgs e)
    {
        if (GridFixedSizes.SelectedItem is FixedSizePreset preset)
            _fixedSizes.Remove(preset);
    }

    // --- Helpers ---

    private static void SelectComboByTag(System.Windows.Controls.ComboBox combo, string tag)
    {
        foreach (System.Windows.Controls.ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static string? GetComboTag(System.Windows.Controls.ComboBox combo)
        => (combo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString();

    private void OnOcrLanguageHelp(object sender, RoutedEventArgs e)
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

    private void UpdateAiKeyStatus()
    {
        if (_aiStore.HasKey())
        {
            AiKeyStatusText.Text = "✓ 저장된 키 있음 (검증 버튼으로 확인)";
            AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Green;
        }
        else
        {
            AiKeyStatusText.Text = "키 미설정";
            AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Gray;
        }
    }

    private void OnAiKeyShowClick(object sender, RoutedEventArgs e)
    {
        // PasswordBox 평문 토글: ToolTip으로 3초 노출
        AiKeyBox.ToolTip = AiKeyBox.Password;
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        t.Tick += (_, _) => { AiKeyBox.ToolTip = null; t.Stop(); };
        t.Start();
    }

    private async void OnAiKeyValidateClick(object sender, RoutedEventArgs e)
    {
        var key = AiKeyBox.Password;
        AiKeyStatusText.Text = "검증 중…";
        AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Gray;

        AiKeyHandle? handle;
        bool persistFromBox;
        if (string.IsNullOrWhiteSpace(key))
        {
            handle = _aiStore.AcquireKey();
            persistFromBox = false;
            if (handle == null)
            {
                AiKeyStatusText.Text = "검증할 키가 없습니다";
                AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }
        }
        else
        {
            handle = new AiKeyHandle(key);
            persistFromBox = true;
        }

        try
        {
            var client = OpenAiClient.CreateDefault(timeoutSeconds: 10);
            var ok = await client.ValidateKeyAsync(handle);
            if (ok)
            {
                if (persistFromBox)
                {
                    _aiStore.SaveKey(AiKeyBox.Password);
                    AiKeyBox.Password = ""; // 평문 흔적 제거
                }
                AiKeyStatusText.Text = $"✓ 키 유효 (검증: {DateTime.Now:HH:mm:ss})";
                AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Green;
                UpdateAiKeyStatus();
            }
            else
            {
                AiKeyStatusText.Text = "✗ 키 검증 실패";
                AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
        finally
        {
            handle.Dispose();
        }
    }

    private void OnAiKeyDeleteClick(object sender, RoutedEventArgs e)
    {
        var r = MessageBox.Show("저장된 OpenAI 키를 삭제할까요?", "신캡쳐",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        _aiStore.DeleteKey();
        AiKeyBox.Password = "";
        AiEnabledCheckBox.IsChecked = false;
        UpdateAiKeyStatus();
    }

    private void OnHyperlinkRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }

    private void BuildHotkeyRows()
    {
        BasicHotkeyPanel.Children.Clear();
        AdvancedHotkeyPanel.Children.Clear();
        _hotkeyRows.Clear();

        foreach (var def in HotkeyDefs)
        {
            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock { Text = def.Label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 0);

            var box = new HotkeyCaptureBox();
            Grid.SetColumn(box, 1);

            var badge = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(badge, 2);

            var clear = new Button
            {
                Content = "✕",
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = "비우기"
            };
            Grid.SetColumn(clear, 3);

            grid.Children.Add(label);
            grid.Children.Add(box);
            grid.Children.Add(badge);
            grid.Children.Add(clear);

            var suggestion = new TextBlock
            {
                Margin = new Thickness(124, 2, 0, 0),
                Foreground = Brushes.OrangeRed,
                Visibility = Visibility.Collapsed,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            container.Children.Add(grid);
            container.Children.Add(suggestion);

            var row = new HotkeyRow { Def = def, Box = box, Badge = badge, Suggestion = suggestion };
            box.ValueCommitted += (_, _) => RefreshHotkeyConflicts();
            clear.Click += (_, _) => { box.Value = ""; RefreshHotkeyConflicts(); };
            suggestion.MouseLeftButtonUp += (_, _) =>
            {
                if (row.SuggestedValue != null) { box.Value = row.SuggestedValue; RefreshHotkeyConflicts(); }
            };

            _hotkeyRows.Add(row);
            (def.Advanced ? AdvancedHotkeyPanel : BasicHotkeyPanel).Children.Add(container);
        }
    }

    private void RefreshHotkeyConflicts()
    {
        var bindings = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var r in _hotkeyRows) bindings[r.Def.Label] = r.Box.Value;

        foreach (var row in _hotkeyRows)
        {
            var val = row.Box.Value;
            row.Suggestion.Visibility = Visibility.Collapsed;
            row.SuggestedValue = null;

            if (string.IsNullOrWhiteSpace(val))
            {
                row.Badge.Text = "";
                row.Badge.ToolTip = "미설정";
                continue;
            }

            var conflict = HotkeyConflicts.FindInternalConflict(val, bindings, row.Def.Label);
            if (conflict != null)
            {
                row.Badge.Text = "🟡";
                row.Badge.ToolTip = $"'{conflict}' 기능과 겹쳐요";
                continue;
            }

            bool available = _hotkeyManager?.IsAvailable(val) ?? true;
            if (!available)
            {
                row.Badge.Text = "🔴";
                row.Badge.ToolTip = "다른 프로그램이 사용 중";
                var alt = HotkeyConflicts.SuggestAlternative(val, c =>
                    HotkeyConflicts.FindInternalConflict(c, bindings, row.Def.Label) == null
                    && (_hotkeyManager?.IsAvailable(c) ?? true));
                if (alt != null)
                {
                    row.SuggestedValue = alt;
                    row.Suggestion.Text = $"↳ 추천: {alt}  (클릭하여 적용)";
                    row.Suggestion.Visibility = Visibility.Visible;
                }
            }
            else
            {
                row.Badge.Text = "🟢";
                row.Badge.ToolTip = "사용 가능";
            }
        }
    }

    private void OnResetHotkeys(object sender, RoutedEventArgs e)
    {
        var defaults = new HotkeySettings();
        foreach (var row in _hotkeyRows)
            row.Box.Value = row.Def.Get(defaults);
        ChkOverridePrintScreen.IsChecked = defaults.OverridePrintScreen;
        RefreshHotkeyConflicts();
    }
}
