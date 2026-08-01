using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

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
    private readonly object _sync = new();
    private readonly HashSet<string> _protectedPaths = new(StringComparer.OrdinalIgnoreCase);

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
        lock (_sync)
        {
            return CreatePngCore(
                path => bitmap.Save(path, ImageFormat.Png),
                now);
        }
    }

    public string CreatePng(BitmapSource source, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (_sync)
        {
            return CreatePngCore(path =>
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                using var stream = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                encoder.Save(stream);
            }, now);
        }
    }

    public void Cleanup(DateTimeOffset? now = null)
    {
        lock (_sync)
            Cleanup(now, protectedPath: null);
    }

    public IDisposable Protect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        lock (_sync)
            _protectedPaths.Add(fullPath);
        return new ProtectedPathLease(this, fullPath);
    }

    private string CreatePngCore(Action<string> save, DateTimeOffset? now)
    {
        DateTimeOffset timestamp = now ?? DateTimeOffset.Now;
        Directory.CreateDirectory(_directory);
        Cleanup(timestamp, protectedPath: null);

        string fileName =
            $"ShinCapture_{timestamp:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png";
        string finalPath = Path.Combine(_directory, fileName);
        string partialPath = finalPath + ".tmp";

        try
        {
            save(partialPath);
            File.Move(partialPath, finalPath);
            Cleanup(timestamp, finalPath);
            return finalPath;
        }
        finally
        {
            TryDelete(partialPath);
        }
    }

    private void Cleanup(DateTimeOffset? now, string? protectedPath)
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

        foreach (FileInfo expired in files
                     .Where(file => file.LastWriteTimeUtc < cutoffUtc &&
                                    !IsProtected(file.FullName, protectedPath))
                     .ToList())
        {
            TryDelete(expired.FullName);
            files.Remove(expired);
        }

        long totalBytes = files.Sum(file => file.Length);
        while (files.Count > _maxFiles || totalBytes > _maxBytes)
        {
            FileInfo? oldest = files.FirstOrDefault(file =>
                !IsProtected(file.FullName, protectedPath));
            if (oldest == null) break;

            files.Remove(oldest);
            if (TryDelete(oldest.FullName))
                totalBytes -= oldest.Length;
        }
    }

    private static bool PathsEqual(string path, string? other) =>
        other != null && string.Equals(
            Path.GetFullPath(path),
            Path.GetFullPath(other),
            StringComparison.OrdinalIgnoreCase);

    private bool IsProtected(string path, string? additionallyProtectedPath) =>
        PathsEqual(path, additionallyProtectedPath) ||
        _protectedPaths.Contains(Path.GetFullPath(path));

    private void ReleaseProtection(string path)
    {
        lock (_sync)
            _protectedPaths.Remove(path);
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

    private sealed class ProtectedPathLease : IDisposable
    {
        private DragExportService? _owner;
        private readonly string _path;

        public ProtectedPathLease(DragExportService owner, string path)
        {
            _owner = owner;
            _path = path;
        }

        public void Dispose()
        {
            DragExportService? owner =
                System.Threading.Interlocked.Exchange(ref _owner, null);
            owner?.ReleaseProtection(_path);
        }
    }
}
