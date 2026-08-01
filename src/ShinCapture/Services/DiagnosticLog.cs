using System;
using System.IO;
using System.Threading;

namespace ShinCapture.Services;

public static class DiagnosticLog
{
    private const long MaxLogBytes = 2L * 1024 * 1024;
    private static readonly object Sync = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ShinCapture",
        "diagnostics.log");

    public static void Write(string area, string message, Exception? exception = null)
    {
        try
        {
            string detail = exception == null ? string.Empty : $" | {exception}";
            string line =
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{area}] {message}{detail}" +
                Environment.NewLine;
            _ = ThreadPool.QueueUserWorkItem(
                static state => Append((string)state!),
                line);
        }
        catch
        {
            // Diagnostics must never interfere with capture or drag-and-drop.
        }
    }

    private static void Append(string line)
    {
        try
        {
            lock (Sync)
            {
                string? directory = Path.GetDirectoryName(LogPath);
                if (directory != null) Directory.CreateDirectory(directory);

                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
                    File.Move(LogPath, LogPath + ".old", overwrite: true);

                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
            // Diagnostics must never interfere with capture or drag-and-drop.
        }
    }
}
