using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using ShinCapture.Services;

namespace ShinCapture.Views;

public partial class EditorWindow
{
    private bool _loadingImageFiles;

    private async void OnOpenImageClick(object sender, RoutedEventArgs e)
    {
        if (_loadingImageFiles) return;
        var dialog = new OpenFileDialog
        {
            Title = "편집할 이미지 열기", Filter = ImageFileLoader.DialogFilter,
            Multiselect = true, CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true) await OpenImageFilesAsync(dialog.FileNames);
    }

    private void OnImageFileDragOver(object sender, DragEventArgs e)
    {
        // Leave internal history drag/export behavior to its existing handlers.
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Effects = _loadingImageFiles || e.Data.GetDataPresent("ShinCapture.HistoryExport")
            ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void OnImageFileDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Handled = true;
        if (e.Data.GetDataPresent("ShinCapture.HistoryExport")) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) await OpenImageFilesAsync(paths);
    }

    private async Task OpenImageFilesAsync(IEnumerable<string> paths)
    {
        if (_loadingImageFiles) return;
        _loadingImageFiles = true;
        int loaded = 0;
        var failures = new List<string>();
        var files = paths.ToArray();
        try
        {
            foreach (string path in files.Take(MaxHistory))
            {
                if (_isClosed) break;
                SetStatus($"이미지 여는 중: {Path.GetFileName(path)}");
                try
                {
                    var image = await Task.Run(() => ImageFileLoader.Load(path));
                    if (_isClosed) break;
                    LoadNewCapture(image);
                    SelectTool("선택");
                    loaded++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                    or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException
                    or System.IO.FileFormatException)
                {
                    failures.Add(Path.GetFileName(path));
                }
            }
            if (_isClosed) return;
            SetStatus($"이미지 {loaded}개 열기 완료 · 원본 파일은 유지됩니다. GIF/TIFF는 첫 장만 엽니다.");
            if (files.Length > MaxHistory)
                SetStatus($"한 번에 최대 {MaxHistory}개까지 열 수 있습니다. 앞의 {MaxHistory}개 중 {loaded}개를 열었습니다.");
            if (failures.Count > 0)
                MessageBox.Show(this, "열 수 없는 파일:\n" + string.Join("\n", failures)
                    + "\n\nPNG, JPG, BMP, GIF, TIFF 형식과 파일 크기를 확인해주세요.",
                    "이미지 열기", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally { _loadingImageFiles = false; }
    }
}
