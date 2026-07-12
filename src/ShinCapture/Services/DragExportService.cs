using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace ShinCapture.Services;

public sealed class DragExportService
{
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromHours(24);
    public const int DefaultMaxFiles = 100;
    public const long DefaultMaxBytes = 250L * 1024 * 1024;

    private readonly string _directory;
    private readonly TimeSpan _retention;
    private readonly int _maxFiles;
    private readonly long _maxBytes;

    public DragExportService(
        string? directory = null,
        TimeSpan? retention = null,
        int maxFiles = DefaultMaxFiles,
        long maxBytes = DefaultMaxBytes)
    {
        if (maxFiles <= 0) throw new ArgumentOutOfRangeException(nameof(maxFiles));
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));

        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShinCapture",
            "Temp",
            "DragDrop");
        _retention = retention ?? DefaultRetention;
        _maxFiles = maxFiles;
        _maxBytes = maxBytes;
    }

    public string CreatePng(Bitmap bitmap, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        DateTimeOffset timestamp = now ?? DateTimeOffset.Now;
        Directory.CreateDirectory(_directory);
        Cleanup(timestamp);

        string fileName =
            $"ShinCapture_{timestamp:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png";
        string finalPath = Path.Combine(_directory, fileName);
        string partialPath = finalPath + ".tmp";

        try
        {
            bitmap.Save(partialPath, ImageFormat.Png);
            File.Move(partialPath, finalPath);
            return finalPath;
        }
        finally
        {
            TryDelete(partialPath);
        }
    }

    public void Cleanup(DateTimeOffset? now = null)
    {
        if (!Directory.Exists(_directory)) return;

        DateTime cutoffUtc = (now ?? DateTimeOffset.Now).UtcDateTime - _retention;
        foreach (string partialPath in SafeGetFiles("*.tmp"))
        {
            if (TryGetLastWriteTimeUtc(partialPath, out DateTime lastWrite) &&
                lastWrite < cutoffUtc)
            {
                TryDelete(partialPath);
            }
        }

        var files = SafeGetFiles("ShinCapture_*.png")
            .Select(TryCreateFileInfo)
            .Where(file => file != null)
            .Cast<FileInfo>()
            .OrderBy(file => file.LastWriteTimeUtc)
            .ToList();

        foreach (FileInfo expired in files.Where(file => file.LastWriteTimeUtc < cutoffUtc).ToList())
        {
            TryDelete(expired.FullName);
            files.Remove(expired);
        }

        long totalBytes = files.Sum(file => file.Length);
        while (files.Count > _maxFiles || totalBytes > _maxBytes)
        {
            FileInfo oldest = files[0];
            files.RemoveAt(0);
            if (TryDelete(oldest.FullName))
                totalBytes -= oldest.Length;
        }
    }

    private IEnumerable<string> SafeGetFiles(string pattern)
    {
        try
        {
            return Directory.GetFiles(_directory, pattern, SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static FileInfo? TryCreateFileInfo(string path)
    {
        try { return new FileInfo(path); }
        catch { return null; }
    }

    private static bool TryGetLastWriteTimeUtc(string path, out DateTime value)
    {
        try
        {
            value = File.GetLastWriteTimeUtc(path);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
