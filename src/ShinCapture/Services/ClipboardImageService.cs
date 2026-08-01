using System;
using System.Collections.Generic;
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

    public static Task<bool> SetImageAsync(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        BitmapSource frozenSource = EnsureFrozen(source);
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        lock (Sync)
        {
            while (Queue.Count >= MaxQueuedItems)
            {
                WorkItem superseded = Queue.Dequeue();
                superseded.Completion.TrySetResult(false);
            }

            Queue.Enqueue(new WorkItem(frozenSource, completion));
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

            try
            {
                bool copied = BitmapHelper.TrySetClipboardPng(item.Source, out Exception? error);
                if (!copied && error != null)
                    DiagnosticLog.Write("Clipboard", "이미지 복사 실패", error);
                item.Completion.TrySetResult(copied);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write("Clipboard", "클립보드 작업 스레드 오류", ex);
                item.Completion.TrySetException(ex);
            }
        }
    }

    private sealed record WorkItem(
        BitmapSource Source,
        TaskCompletionSource<bool> Completion);
}
