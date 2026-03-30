using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ShinCapture.Models;
using ShinCapture.Services;

namespace ShinCapture.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private AppSettings _settings;
    private ObservableCollection<FixedSizePreset> _fixedSizes = new();

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

        // 지정사이즈
        _fixedSizes = new ObservableCollection<FixedSizePreset>(
            _settings.FixedSizes?.Select(p => new FixedSizePreset
            {
                Name   = p.Name,
                Width  = p.Width,
                Height = p.Height
            }) ?? Enumerable.Empty<FixedSizePreset>());
        GridFixedSizes.ItemsSource = _fixedSizes;
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
}
