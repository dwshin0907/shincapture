using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShinCapture.Services;

/// <summary>Owns a responsive message loop independent of capture/PNG encoding and WPF rendering.</summary>
internal sealed class AdaptiveImageClipboard : IDisposable
{
    private readonly TaskCompletionSource<Control> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task Ready => _ready.Task;
    internal Task Stopped => _stopped.Task;
    private readonly Func<long> _currentGeneration;
    private readonly WinEventProc _foregroundCallback;
    private readonly object _lifetimeSync = new();
    private readonly HashSet<TaskCompletionSource<ClipboardWriteResult>> _requests = new();
    private AdaptiveClipboardSession? _session;
    private System.Windows.Forms.Timer? _retryTimer;
    private PendingPublication? _pending;
    private long _retryUntil;
    private int _disposed;
    private bool _processing;
    private bool _refreshQueued;

    public AdaptiveImageClipboard(Func<long> currentGeneration)
    {
        _currentGeneration = currentGeneration;
        _foregroundCallback = OnForegroundChanged;
        var thread = new Thread(Run) { IsBackground = true, Name = "ShinCapture Clipboard Target" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public async Task<ClipboardWriteResult> PublishAsync(ClipboardImagePayload payload, long generation)
    {
        Control control = await _ready.Task.ConfigureAwait(false);
        var completion = new TaskCompletionSource<ClipboardWriteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            lock (_lifetimeSync)
            {
                if (_disposed != 0)
                    return new(ClipboardWriteStatus.Failed, Error: new ObjectDisposedException(nameof(AdaptiveImageClipboard)));
                _requests.Add(completion);
                control.BeginInvoke(new Action(() =>
                {
                    _pending?.Completion.TrySetResult(new(ClipboardWriteStatus.Superseded));
                    _pending = new(payload, generation, completion);
                    _retryUntil = Environment.TickCount64 + 250;
                    Process();
                }));
            }
        }
        catch (InvalidOperationException ex)
        {
            completion.TrySetResult(new(ClipboardWriteStatus.Failed, Error: ex));
        }
        try { return await completion.Task.ConfigureAwait(false); }
        finally { lock (_lifetimeSync) _requests.Remove(completion); }
    }

    private void Run()
    {
        IntPtr hook = IntPtr.Zero;
        using var control = new Control();
        try
        {
            _session = new AdaptiveClipboardSession(
                new WindowsImageClipboardWriter(control.Handle), IsKakaoTalkForeground, _currentGeneration);
            _retryTimer = new System.Windows.Forms.Timer { Interval = 20 };
            _retryTimer.Tick += (_, _) => Process();
            // Include own-process transitions so leaving KakaoTalk for the editor also restores paths.
            hook = SetWinEventHook(3, 3, IntPtr.Zero, _foregroundCallback, 0, 0, 0);
            if (hook == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            _ready.TrySetResult(control);
            System.Windows.Forms.Application.Run();
        }
        catch (Exception ex)
        {
            _ready.TrySetException(ex);
            DiagnosticLog.Write("Clipboard", "붙여넣기 대상 감지 시작 실패", ex);
        }
        finally
        {
            lock (_lifetimeSync)
            {
                _disposed = 1;
                foreach (var request in _requests)
                    request.TrySetResult(new(ClipboardWriteStatus.Failed,
                        Error: new ObjectDisposedException(nameof(AdaptiveImageClipboard))));
            }
            _retryTimer?.Dispose();
            if (hook != IntPtr.Zero) UnhookWinEvent(hook);
            _pending?.Completion.TrySetResult(new(ClipboardWriteStatus.Failed,
                Error: new ObjectDisposedException(nameof(AdaptiveImageClipboard))));
            _stopped.TrySetResult(true);
        }
    }

    private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd,
        int objectId, int childId, uint eventThread, uint eventTime)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        // Ignore the event's possibly stale hwnd; Process reads the current foreground every time.
        _retryUntil = Environment.TickCount64 + 250;
        Process();
    }

    private void Process()
    {
        if (_processing) { _refreshQueued = true; return; }
        _processing = true;
        _retryTimer?.Stop();
        try
        {
            if (_session == null || Volatile.Read(ref _disposed) != 0) return;
            ClipboardWriteResult result = _pending is { } pending
                ? _session.Publish(pending.Payload, pending.Generation)
                : _session.Refresh();

            if (result.Status == ClipboardWriteStatus.Busy)
            {
                if (Environment.TickCount64 < _retryUntil)
                {
                    _retryTimer?.Start();
                    return;
                }
                result = new(ClipboardWriteStatus.Failed,
                    Error: new ExternalException("다른 프로그램이 클립보드를 사용 중입니다."));
            }
            if (result.Status == ClipboardWriteStatus.Failed)
                DiagnosticLog.Write("Clipboard", "클립보드 게시 또는 카카오톡 형식 전환 실패", result.Error);
            _pending?.Completion.TrySetResult(result);
            _pending = null;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Clipboard", "붙여넣기 형식 전환 오류", ex);
            _pending?.Completion.TrySetResult(new(ClipboardWriteStatus.Failed, Error: ex));
            _pending = null;
        }
        finally
        {
            _processing = false;
            if (_refreshQueued && Volatile.Read(ref _disposed) == 0)
            {
                _refreshQueued = false;
                _retryTimer?.Start();
            }
        }
    }

    private static bool IsKakaoTalkForeground()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == 0) return false;
            using Process process = System.Diagnostics.Process.GetProcessById((int)processId);
            return AdaptiveClipboardSession.IsKakaoTalkProcess(process.ProcessName);
        }
        catch (Exception) { return false; } // Exited/inaccessible processes keep the default formats.
    }

    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            if (_disposed != 0) return;
            _disposed = 1;
            _ = StopAsync();
        }
    }

    private async Task StopAsync()
    {
        try
        {
            Control control = await _ready.Task.ConfigureAwait(false);
            control.BeginInvoke(new Action(System.Windows.Forms.Application.ExitThread));
        }
        catch (Exception) { }
    }

    private sealed record PendingPublication(ClipboardImagePayload Payload, long Generation,
        TaskCompletionSource<ClipboardWriteResult> Completion);

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd,
        int objectId, int childId, uint eventThread, uint eventTime);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module,
        WinEventProc callback, uint processId, uint threadId, uint flags);
    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
}
