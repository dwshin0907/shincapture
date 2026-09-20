using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;

namespace ShinCapture.Services;

/// <summary>Protects every file until the entire batch has been saved or handed to another app.</summary>
public sealed class HistoryExportBatch : IDisposable
{
    private readonly DragExportService _exports;
    private readonly List<string> _paths = new();
    private readonly List<IDisposable> _leases = new();

    public HistoryExportBatch(DragExportService exports) => _exports = exports;

    public IReadOnlyList<string> Paths => _paths;

    // One line, without a trailing Enter: safe to drop into an interactive coding prompt.
    public string PathText => string.Join(" ", _paths.Select(path => $"\"{path}\""));

    public void Add(string path)
    {
        string fullPath = Path.GetFullPath(path);
        _leases.Add(_exports.Protect(fullPath));
        _paths.Add(fullPath);
    }

    public DataObject CreateDragData()
    {
        var files = new StringCollection();
        files.AddRange(_paths.ToArray());
        var data = new DataObject();
        data.SetData("ShinCapture.HistoryExport", true);
        data.SetFileDropList(files);
        data.SetText(PathText, TextDataFormat.UnicodeText);
        return data;
    }

    public HistoryBatchSaveResult SaveToDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        string batchName = $"신캡쳐_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}";
        var savedPaths = new List<string>();
        int failures = 0;
        for (int i = 0; i < _paths.Count; i++)
        {
            string destination = Path.Combine(directory, $"{batchName}_{i + 1:00}.png");
            string partialPath = destination + ".tmp";
            try
            {
                File.Copy(_paths[i], partialPath, overwrite: false);
                File.Move(partialPath, destination, overwrite: false);
                savedPaths.Add(destination);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures++;
                DiagnosticLog.Write("HistorySave", "캡처 이미지 저장 실패", ex);
            }
            finally
            {
                try { File.Delete(partialPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        return new HistoryBatchSaveResult(savedPaths, failures);
    }

    public void Dispose()
    {
        foreach (IDisposable lease in _leases) lease.Dispose();
        _leases.Clear();
    }
}

public sealed record HistoryBatchSaveResult(IReadOnlyList<string> SavedPaths, int FailedCount);
