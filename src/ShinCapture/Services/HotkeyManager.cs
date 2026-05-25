using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ShinCapture.Helpers;

namespace ShinCapture.Services;

public class HotkeyManager : IDisposable
{
    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _nextId = 1;

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    public int Register(string hotkeyString, Action action)
    {
        ParseHotkeyString(hotkeyString, out uint modifiers, out uint vk);
        modifiers |= NativeMethods.MOD_NOREPEAT;
        var id = _nextId++;
        if (NativeMethods.RegisterHotKey(_hwnd, id, modifiers, vk))
        {
            _hotkeyActions[id] = action;
            return id;
        }
        return -1;
    }

    public void Unregister(int id)
    {
        if (_hotkeyActions.ContainsKey(id))
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
            _hotkeyActions.Remove(id);
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeyActions.Keys.ToList())
            Unregister(id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public static void ParseHotkeyString(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        var parts = hotkey.Split('+').Select(p => p.Trim()).ToList();
        foreach (var part in parts)
        {
            switch (part.ToLower())
            {
                case "ctrl": case "control":
                    modifiers |= NativeMethods.MOD_CONTROL; break;
                case "shift":
                    modifiers |= NativeMethods.MOD_SHIFT; break;
                case "alt":
                    modifiers |= NativeMethods.MOD_ALT; break;
                case "printscreen":
                    vk = (uint)KeyInterop.VirtualKeyFromKey(Key.PrintScreen); break;
                default:
                    // 단일 숫자: "1" → Key.D1, "0" → Key.D0 (WPF Key enum is D-prefixed)
                    if (part.Length == 1 && part[0] >= '0' && part[0] <= '9')
                    {
                        if (Enum.TryParse<Key>("D" + part, true, out var digitKey))
                            vk = (uint)KeyInterop.VirtualKeyFromKey(digitKey);
                        break;
                    }
                    // F-keys, alphabet, 등은 그대로 동작
                    if (Enum.TryParse<Key>(part, true, out var key))
                        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    break;
            }
        }
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }
}
