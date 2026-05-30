using System;
using System.Collections.Generic;
using System.Windows.Input;
using ShinCapture.Services;

namespace ShinCapture.Services.Hotkeys;

/// <summary>단축키 충돌 탐지/대안 추천 (순수 로직, OS 가용성은 주입된 함수로).</summary>
public static class HotkeyConflicts
{
    /// <summary>순서/대소문자 무시 비교용 정규 키. 빈/무효는 null.</summary>
    public static string? Normalize(string? hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return null;
        HotkeyManager.ParseHotkeyString(hotkey, out uint mods, out uint vk);
        if (vk == 0) return null;
        return $"{mods}:{vk}";
    }

    /// <summary>candidate가 (selfKey 제외) bindings 중 무엇과 겹치면 그 키, 없으면 null.</summary>
    public static string? FindInternalConflict(
        string candidate, IReadOnlyDictionary<string, string> bindings, string selfKey)
    {
        var norm = Normalize(candidate);
        if (norm == null) return null;
        foreach (var kv in bindings)
        {
            if (kv.Key == selfKey) continue;
            if (Normalize(kv.Value) == norm) return kv.Key;
        }
        return null;
    }

    /// <summary>taken과 같은 키를 유지하며 수정자 조합을 바꿔 isAvailable인 첫 조합 반환. 없으면 null.</summary>
    public static string? SuggestAlternative(string taken, Func<string, bool> isAvailable)
    {
        HotkeyManager.ParseHotkeyString(taken, out _, out uint vk);
        if (vk == 0) return null;
        var key = KeyInterop.KeyFromVirtualKey((int)vk);

        var candidates = new[]
        {
            ModifierKeys.Control | ModifierKeys.Shift,
            ModifierKeys.Control | ModifierKeys.Alt,
            ModifierKeys.Alt | ModifierKeys.Shift,
            ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift,
        };
        foreach (var mods in candidates)
        {
            var combo = HotkeyInput.Format(mods, key);
            if (isAvailable(combo)) return combo;
        }
        return null;
    }
}
