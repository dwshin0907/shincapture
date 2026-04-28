using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ShinCapture.Models;
using ShinCapture.Services;
using ShinCapture.Services.Ai;

namespace ShinCapture.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private AppSettings _settings;
    private ObservableCollection<FixedSizePreset> _fixedSizes = new();
    private readonly DpapiCredentialStore _aiStore = new();

    public SettingsWindow(SettingsManager settingsManager)
    {
        InitializeComponent();
        _settingsManager = settingsManager;
        _settings = settingsManager.Load();
        LoadSettings();
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

        // 단축키
        TxtHkRegion.Text    = _settings.Hotkeys.RegionCapture;
        TxtHkFreeform.Text  = _settings.Hotkeys.FreeformCapture;
        TxtHkWindow.Text    = _settings.Hotkeys.WindowCapture;
        TxtHkElement.Text   = _settings.Hotkeys.ElementCapture;
        TxtHkFullscreen.Text = _settings.Hotkeys.FullscreenCapture;
        TxtHkScroll.Text    = _settings.Hotkeys.ScrollCapture;
        TxtHkFixedSize.Text = _settings.Hotkeys.FixedSizeCapture;
        TxtHkTextCapture.Text = _settings.Hotkeys.TextCapture;
        TxtHkTranslateCapture.Text = _settings.Hotkeys.TranslateCapture;
        ChkOverridePrintScreen.IsChecked = _settings.Hotkeys.OverridePrintScreen;

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
        _settings.Hotkeys.RegionCapture    = TxtHkRegion.Text;
        _settings.Hotkeys.FreeformCapture  = TxtHkFreeform.Text;
        _settings.Hotkeys.WindowCapture    = TxtHkWindow.Text;
        _settings.Hotkeys.ElementCapture   = TxtHkElement.Text;
        _settings.Hotkeys.FullscreenCapture = TxtHkFullscreen.Text;
        _settings.Hotkeys.ScrollCapture    = TxtHkScroll.Text;
        _settings.Hotkeys.FixedSizeCapture = TxtHkFixedSize.Text;
        _settings.Hotkeys.TextCapture = TxtHkTextCapture.Text;
        _settings.Hotkeys.TranslateCapture = TxtHkTranslateCapture.Text;
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
}
