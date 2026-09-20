using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Editor;
using ShinCapture.Services;

namespace ShinCapture.Views;

public partial class EditorWindow
{
    private readonly CaptureHistorySelection<BitmapSource> _historySelection = new();
    private readonly Dictionary<BitmapSource, CheckBox> _historyCheckBoxes = new();
    private bool _historyExportInProgress;

    private BitmapSource[] GetSelectedHistoryImages() => _historySelection.InDisplayOrder(_captureHistory);

    private void OnHistorySelectAll(object sender, RoutedEventArgs e)
    {
        if (_historySelection.Count == _captureHistory.Count) _historySelection.Clear();
        else _historySelection.SelectAll(_captureHistory);
        UpdateHistorySelection();
    }

    private void ToggleHistorySelection(BitmapSource image, bool extendRange)
    {
        if (extendRange) _historySelection.SelectRange(_captureHistory, image);
        else _historySelection.Toggle(image);
        UpdateHistorySelection();
        if (_historySelection.Contains(image)) _ = ScheduleDragExportPreparation(image);
    }

    private void UpdateHistoryBatchActions()
    {
        int count = _historySelection.Count;
        HistorySelectionCount.Text = $"{count}개 선택";
        HistorySelectAllCheckBox.IsChecked = count == 0 ? false
            : count == _captureHistory.Count ? true : null;
        HistorySelectAllCheckBox.IsEnabled = _captureHistory.Count > 0;
        HistorySaveSelectedBtn.IsEnabled = HistoryCopyPathsBtn.IsEnabled = count > 0 && !_historyExportInProgress;
        HistorySaveAllBtn.IsEnabled = _captureHistory.Count > 0 && !_historyExportInProgress;
        HistoryClearBtn.IsEnabled = _captureHistory.Count > 0 && !_historyExportInProgress;
        HistorySaveSelectedBtn.Opacity = HistoryCopyPathsBtn.Opacity = HistorySaveSelectedBtn.IsEnabled ? 1 : 0.45;
    }

    private bool HandleHistorySelectionKey(KeyEventArgs e)
    {
        if (!HistoryPanel.IsKeyboardFocusWithin) return false;
        ModifierKeys modifiers = Keyboard.Modifiers;
        BitmapSource current = GetFocusedHistoryImage() ?? _sourceImage;
        if (e.Key == Key.A && modifiers == ModifierKeys.Control)
        {
            _historySelection.SelectAll(_captureHistory);
            UpdateHistorySelection();
        }
        else if (e.Key == Key.Space && modifiers is ModifierKeys.None or ModifierKeys.Control or ModifierKeys.Shift)
        {
            ToggleHistorySelection(current, modifiers == ModifierKeys.Shift);
        }
        else if (e.Key == Key.C && modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            OnHistoryCopyPaths(this, e);
        }
        else if (e.Key is Key.Up or Key.Down)
        {
            if ((modifiers & ~(ModifierKeys.Control | ModifierKeys.Shift)) != 0)
            {
                e.Handled = true;
                return true;
            }
            int index = CaptureHistoryNavigationPolicy.GetTargetIndex(
                _captureHistory.IndexOf(current), _captureHistory.Count,
                e.Key == Key.Up ? CaptureHistoryDirection.Up : CaptureHistoryDirection.Down);
            if (index >= 0)
            {
                BitmapSource target = _captureHistory[index];
                if (modifiers.HasFlag(ModifierKeys.Shift))
                    _historySelection.SelectRange(_captureHistory, target, modifiers.HasFlag(ModifierKeys.Control));
                else if (modifiers == ModifierKeys.None)
                    _historySelection.SelectOnly(target);

                if (modifiers == ModifierKeys.Control)
                {
                    _historyCards[target].Focus();
                    _historyCards[target].BringIntoView();
                }
                else
                    LoadFromHistory(target, focusHistoryItem: true);
                UpdateHistorySelection();
            }
        }
        else return false;
        e.Handled = true;
        return true;
    }

    private BitmapSource? GetFocusedHistoryImage()
    {
        for (DependencyObject? element = Keyboard.FocusedElement as DependencyObject;
             element != null; element = GetHistoryInputParent(element))
        {
            if (element is FrameworkElement { Tag: BitmapSource image }) return image;
        }
        return null;
    }

    private static DependencyObject? GetHistoryInputParent(DependencyObject element) =>
        element is FrameworkContentElement content ? content.Parent : VisualTreeHelper.GetParent(element);

    private static bool IsHistoryCheckBoxInput(object source)
    {
        for (DependencyObject? element = source as DependencyObject;
             element != null; element = GetHistoryInputParent(element))
        {
            if (element is CheckBox) return true;
            if (element is Border { Tag: BitmapSource }) break;
        }
        return false;
    }

    private void AttachHistoryCardInput(Border card, BitmapSource image)
    {
        Point dragStart = default;
        bool tracking = false;
        bool collapseOnClick = false;

        card.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (_historyExportInProgress || IsHistoryCheckBoxInput(e.OriginalSource)) return;
            ModifierKeys modifiers = Keyboard.Modifiers;
            collapseOnClick = modifiers == ModifierKeys.None && _historySelection.Contains(image);
            if (modifiers.HasFlag(ModifierKeys.Shift))
                _historySelection.SelectRange(_captureHistory, image, modifiers.HasFlag(ModifierKeys.Control));
            else if (modifiers.HasFlag(ModifierKeys.Control))
                _historySelection.Toggle(image);
            else if (!_historySelection.Contains(image))
                _historySelection.SelectOnly(image);
            UpdateHistorySelection();

            dragStart = e.GetPosition(card);
            tracking = true;
            card.Focus();
            card.CaptureMouse();
            if (image == _sourceImage) SaveCurrentObjects();
            _ = ScheduleDragExportPreparation(image);
            e.Handled = true;
        };
        card.MouseEnter += (_, _) => _ = ScheduleDragExportPreparation(image);
        card.LostMouseCapture += (_, _) => tracking = false;
        card.PreviewMouseMove += (_, e) =>
        {
            if (!tracking || e.LeftButton != MouseButtonState.Pressed) return;
            Point current = e.GetPosition(card);
            if (!HistoryCardDragPolicy.ShouldStart(current.X - dragStart.X, current.Y - dragStart.Y,
                    SystemParameters.MinimumHorizontalDragDistance, SystemParameters.MinimumVerticalDragDistance)) return;

            tracking = false;
            if (Mouse.Captured == card) card.ReleaseMouseCapture();
            e.Handled = true;
            if (_historySelection.Contains(image)) StartHistoryCardDrag(card);
        };
        card.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!tracking) return;
            tracking = false;
            if (Mouse.Captured == card) card.ReleaseMouseCapture();
            if (collapseOnClick) _historySelection.SelectOnly(image);
            UpdateHistorySelection();
            if (_historySelection.Contains(image)) LoadFromHistory(image, focusHistoryItem: true);
            e.Handled = true;
        };
    }

    private async Task<HistoryExportBatch> PrepareHistoryExportBatchAsync(IReadOnlyList<BitmapSource> images)
    {
        SaveCurrentObjects();
        var batch = new HistoryExportBatch(_dragExportService);
        try
        {
            foreach (BitmapSource image in images)
            {
                Task<string?>? preparation = ScheduleDragExportPreparation(image);
                string? path = preparation == null ? null : await preparation;
                if (_isClosed || path == null || !File.Exists(path))
                    throw new IOException("선택한 캡처의 이미지 파일을 준비하지 못했습니다.");
                batch.Add(path);
            }
            return batch;
        }
        catch
        {
            batch.Dispose();
            throw;
        }
    }

    private async void StartHistoryCardDrag(Border card)
    {
        BitmapSource[] images = GetSelectedHistoryImages();
        if (_historyExportInProgress || images.Length == 0) return;
        _historyExportInProgress = true;
        UpdateHistoryBatchActions();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            StatusText.Text = $"선택한 {images.Length}개 드래그 파일 준비 중...";
            using HistoryExportBatch batch = await PrepareHistoryExportBatchAsync(images);
            if (_isClosed) return;
            if (Mouse.LeftButton != MouseButtonState.Pressed)
            {
                StatusText.Text = $"{images.Length}개 파일 준비 완료 · 마우스를 누른 채 다시 드래그하세요";
                return;
            }
            card.Opacity = 0.62;
            DragDropEffects effect = System.Windows.DragDrop.DoDragDrop(card, batch.CreateDragData(), DragDropEffects.Copy);
            StatusText.Text = effect == DragDropEffects.None
                ? "드래그가 취소되었습니다"
                : $"선택한 {images.Length}개 이미지를 외부 앱으로 보냈습니다";
            DiagnosticLog.Write("Drag", $"effect={effect}, count={images.Length}, elapsed={stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            if (!_isClosed) StatusText.Text = "이미지를 보내지 못했습니다. 다시 시도해 주세요.";
            DiagnosticLog.Write("Drag", "여러 캡처 드래그 실패", ex);
        }
        finally
        {
            card.Opacity = 1;
            _historyExportInProgress = false;
            UpdateHistoryBatchActions();
        }
    }

    private async void OnHistoryCopyPaths(object sender, RoutedEventArgs e)
    {
        BitmapSource[] images = GetSelectedHistoryImages();
        if (_historyExportInProgress || images.Length == 0) return;
        _historyExportInProgress = true;
        UpdateHistoryBatchActions();
        try
        {
            StatusText.Text = $"선택한 {images.Length}개 경로 준비 중...";
            using HistoryExportBatch batch = await PrepareHistoryExportBatchAsync(images);
            if (_isClosed) return;
            System.Windows.Clipboard.SetDataObject(batch.PathText, copy: true);
            StatusText.Text = $"{images.Length}개 이미지 경로 복사됨 · 터미널에 붙여넣으세요";
        }
        catch (Exception ex)
        {
            if (!_isClosed) StatusText.Text = "경로를 복사하지 못했습니다. 다시 시도해 주세요.";
            DiagnosticLog.Write("HistoryPaths", "선택한 캡처 경로 복사 실패", ex);
        }
        finally
        {
            _historyExportInProgress = false;
            UpdateHistoryBatchActions();
        }
    }

    private async void OnHistorySaveSelected(object sender, RoutedEventArgs e) =>
        await SaveHistoryImagesAsync(GetSelectedHistoryImages());

    private async void OnHistorySaveAll(object sender, RoutedEventArgs e) =>
        await SaveHistoryImagesAsync(_captureHistory.ToArray());

    private async Task SaveHistoryImagesAsync(BitmapSource[] images)
    {
        if (_historyExportInProgress || images.Length == 0) return;
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = $"{images.Length}개 캡처를 저장할 폴더 선택"
        };
        if (dialog.ShowDialog(this) != true) return;

        _historyExportInProgress = true;
        UpdateHistoryBatchActions();
        try
        {
            StatusText.Text = $"{images.Length}개 이미지 저장 중...";
            using HistoryExportBatch batch = await PrepareHistoryExportBatchAsync(images);
            if (_isClosed) return;
            HistoryBatchSaveResult result = await Task.Run(() => batch.SaveToDirectory(dialog.FolderName));
            if (_isClosed) return;
            StatusText.Text = result.FailedCount == 0
                ? $"{result.SavedPaths.Count}개 이미지 저장됨: {dialog.FolderName}"
                : $"{result.SavedPaths.Count}개 저장, {result.FailedCount}개 실패 · 폴더 권한과 여유 공간을 확인해 주세요";
        }
        catch (Exception ex)
        {
            if (!_isClosed) StatusText.Text = "이미지 저장 실패 · 폴더 권한과 여유 공간을 확인해 주세요";
            DiagnosticLog.Write("HistorySave", "캡처 일괄 저장 실패", ex);
        }
        finally
        {
            _historyExportInProgress = false;
            UpdateHistoryBatchActions();
        }
    }
}
