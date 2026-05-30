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
    private readonly struct Binding
    {
        public readonly uint Modifiers; // 등록에 쓰인 값(MOD_NOREPEAT 포함)
        public readonly uint Vk;
        public readonly Action Action;
        public Binding(uint modifiers, uint vk, Action action)
        {
            Modifiers = modifiers; Vk = vk; Action = action;
        }
    }

    private IntPtr _hwnd;
    private HwndSource? _source;
    private readonly Dictionary<int, Binding> _bindings = new();
    private int _nextId = 1;
    private bool _suspended;

    /// <summary>앱 전역에서 하나뿐인 인스턴스(설정창이 소유자 경로와 무관하게 접근). Initialize에서 설정됨.</summary>
    public static HotkeyManager? Current { get; private set; }

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _hwnd = helper.EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        Current = this;
    }

    public int Register(string hotkeyString, Action action)
    {
        ParseHotkeyString(hotkeyString, out uint modifiers, out uint vk);
        modifiers |= NativeMethods.MOD_NOREPEAT;
        var id = _nextId++;
        if (NativeMethods.RegisterHotKey(_hwnd, id, modifiers, vk))
        {
            _bindings[id] = new Binding(modifiers, vk, action);
            _suspended = false;
            return id;
        }
        return -1;
    }

    public void Unregister(int id)
    {
        if (_bindings.ContainsKey(id))
        {
            NativeMethods.UnregisterHotKey(_hwnd, id);
            _bindings.Remove(id);
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _bindings.Keys.ToList())
            NativeMethods.UnregisterHotKey(_hwnd, id);
        _bindings.Clear();
        _suspended = false;
    }

    /// <summary>등록 정보를 유지한 채 OS 단축키만 일시 해제(설정창 편집 중). Resume으로 복원.</summary>
    public void Suspend()
    {
        if (_suspended) return;
        foreach (var id in _bindings.Keys)
            NativeMethods.UnregisterHotKey(_hwnd, id);
        _suspended = true;
    }

    /// <summary>Suspend로 해제했던 단축키를 동일하게 재등록.</summary>
    public void Resume()
    {
        if (!_suspended) return;
        foreach (var kv in _bindings)
            NativeMethods.RegisterHotKey(_hwnd, kv.Key, kv.Value.Modifiers, kv.Value.Vk);
        _suspended = false;
    }

    /// <summary>이 조합을 지금 전역 등록 가능한지 프로브한다(성공 시 즉시 해제). UI 스레드에서 호출.
    /// 설정창은 Suspend 상태에서 호출하므로 자기 자신과 충돌하지 않는다.</summary>
    public bool IsAvailable(string hotkeyString)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString)) return true;
        ParseHotkeyString(hotkeyString, out uint modifiers, out uint vk);
        if (vk == 0) return false;
        modifiers |= NativeMethods.MOD_NOREPEAT;
        const int probeId = 0x7000; // 활성 등록 id 범위(_nextId)와 충돌하지 않는 높은 값
        if (NativeMethods.RegisterHotKey(_hwnd, probeId, modifiers, vk))
        {
            NativeMethods.UnregisterHotKey(_hwnd, probeId);
            return true;
        }
        return false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_bindings.TryGetValue(id, out var binding))
            {
                binding.Action();
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
        if (ReferenceEquals(Current, this)) Current = null;
    }
}
