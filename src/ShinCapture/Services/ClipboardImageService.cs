using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ShinCapture.Helpers;

namespace ShinCapture.Services;

public static class ClipboardImageService
{
    private const int MaxQueuedItems = 2;
    private static readonly object Sync = new();
    private static readonly Queue<WorkItem> Queue = new();
    private static readonly Thread Worker;
    private static readonly DragExportService ClipboardExporter = new(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShinCapture",
            "Temp",
            "Clipboard"));
    private static IDisposable? _latestExportLease;

    static ClipboardImageService()
    {
        Worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "ShinCapture Clipboard STA"
        };
        Worker.SetApartmentState(ApartmentState.STA);
        Worker.Start();
    }

    public static async Task<bool> SetImageAsync(BitmapSource source)
    {
        ClipboardImageResult result = await EnqueueAsync(source, includeFile: false);
        return result.Success;
    }

    public static Task<ClipboardImageResult> SetImageWithFileAsync(BitmapSource source)
        => EnqueueAsync(source, includeFile: true);

    private static Task<ClipboardImageResult> EnqueueAsync(
        BitmapSource source,
        bool includeFile)
    {
        ArgumentNullException.ThrowIfNull(source);
        BitmapSource frozenSource = EnsureFrozen(source);
        var completion = new TaskCompletionSource<ClipboardImageResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        lock (Sync)
        {
            while (Queue.Count >= MaxQueuedItems)
            {
                WorkItem superseded = Queue.Dequeue();
                superseded.Completion.TrySetResult(
                    new ClipboardImageResult(false, null, null));
            }

            Queue.Enqueue(new WorkItem(frozenSource, includeFile, completion));
            Monitor.Pulse(Sync);
        }

        return completion.Task;
    }

    private static BitmapSource EnsureFrozen(BitmapSource source)
    {
        if (source.IsFrozen) return source;
        BitmapSource clone = source.Clone();
        if (!clone.CanFreeze)
            throw new InvalidOperationException("클립보드 이미지를 작업 스레드로 전달할 수 없습니다.");
        clone.Freeze();
        return clone;
    }

    private static void Run()
    {
        while (true)
        {
            WorkItem item;
            lock (Sync)
            {
                while (Queue.Count == 0) Monitor.Wait(Sync);
                item = Queue.Dequeue();
            }

            IDisposable? newLease = null;
            try
            {
                string? filePath = null;
                bool copied;
                Exception? error;

                if (item.IncludeFile)
                {
                    filePath = ClipboardExporter.CreatePng(item.Source);
                    newLease = ClipboardExporter.Protect(filePath);
                    copied = BitmapHelper.TrySetClipboardPngWithFile(
                        item.Source,
                        filePath,
                        out error);
                }
                else
                {
                    copied = BitmapHelper.TrySetClipboardPng(item.Source, out error);
                }

                if (!copied && error != null)
                    DiagnosticLog.Write("Clipboard", "이미지 복사 실패", error);

                if (copied && item.IncludeFile)
                {
                    IDisposable? oldLease = _latestExportLease;
                    _latestExportLease = newLease;
                    newLease = null;
                    oldLease?.Dispose();
                }
                item.Completion.TrySetResult(new ClipboardImageResult(
                    copied,
                    copied ? filePath : null,
                    error));
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write("Clipboard", "클립보드 작업 스레드 오류", ex);
                item.Completion.TrySetResult(
                    new ClipboardImageResult(false, null, ex));
            }
            finally
            {
                newLease?.Dispose();
            }
        }
    }

    private sealed record WorkItem(
        BitmapSource Source,
        bool IncludeFile,
        TaskCompletionSource<ClipboardImageResult> Completion);

    public sealed record ClipboardImageResult(
        bool Success,
        string? FilePath,
        Exception? Error);
}
