using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ShinCapture.Services.Hotkeys;

/// <summary>Never registers Ctrl+Shift+C with RegisterHotKey, so Ctrl-first reaches the foreground app.</summary>
internal sealed class OrderedCaptureHotkey : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action _action;
    private readonly HookProc _callback;
    private readonly OrderedCaptureKeyState _state = new();
    private IntPtr _hook;
    private volatile bool _enabled;
    private volatile bool _disposed;
    private Dispatcher? _hookDispatcher;
    private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _generation;

    public OrderedCaptureHotkey(Dispatcher dispatcher, Action action)
    {
        _dispatcher = dispatcher;
        _action = action;
        _callback = OnKeyboard;
    }

    public bool Start()
    {
        // A dedicated message loop keeps slow capture/render work on the UI thread
        // from causing Windows to silently remove the low-level hook on timeout.
        var thread = new Thread(() =>
        {
            _hookDispatcher = Dispatcher.CurrentDispatcher;
            try
            {
                _hook = SetWindowsHookEx(13, _callback, GetModuleHandle(null), 0);
                foreach (int key in OrderedCaptureKeyState.ModifierKeys)
                    if ((GetAsyncKeyState(key) & 0x8000) != 0) _state.SeedHeldModifier(key);
                _started.TrySetResult(_hook != IntPtr.Zero);
                if (_hook != IntPtr.Zero && !_disposed) Dispatcher.Run();
            }
            finally
            {
                if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
                _started.TrySetResult(false);
            }
        }) { IsBackground = true, Name = "ShinCapture ordered hotkey" };
        thread.Start();
        if (!_started.Task.Wait(TimeSpan.FromSeconds(5)) || !_started.Task.Result) return false;
        SetEnabled(true);
        return true;
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled && _disposed) return;
        _enabled = enabled;
        Interlocked.Increment(ref _generation);
    }

    private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message.ToInt32() is 0x100 or 0x101 or 0x104 or 0x105)
        {
            int key = Marshal.ReadInt32(data);
            bool down = message.ToInt32() is 0x100 or 0x104;
            // The current key's async state is not updated yet. Reconcile other keys
            // to recover releases missed while a secure desktop was active.
            foreach (int modifier in OrderedCaptureKeyState.ModifierKeys)
                if (modifier != key && (GetAsyncKeyState(modifier) & 0x8000) == 0)
                    _state.ReleaseModifier(modifier);
            if (_state.Process(key, down, _enabled, out bool trigger))
            {
                if (trigger)
                {
                    int generation = Volatile.Read(ref _generation);
                    // Keep the hook short; capture and window work happen after it returns.
                    _dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                    {
                        if (_enabled && generation == Volatile.Read(ref _generation)) _action();
                    }));
                }
                return new IntPtr(1);
            }
        }
        return CallNextHookEx(_hook, code, message, data);
    }

    public void Dispose()
    {
        SetEnabled(false);
        _disposed = true;
        _hookDispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
    }

    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? name);
}
