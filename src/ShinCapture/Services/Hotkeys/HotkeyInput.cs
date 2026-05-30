using System.Collections.Generic;
using System.Windows.Input;

namespace ShinCapture.Services.Hotkeys;

/// <summary>키 입력을 백엔드 단축키 문자열로 변환하고 전역 단축키 유효성을 검사하는 순수 로직.</summary>
public static class HotkeyInput
{
    public static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.System or Key.LWin or Key.RWin;

    /// <summary>수정자 없이도 전역 등록이 자연스러운 키(기능키/PrintScreen).</summary>
    public static bool AllowsNoModifier(Key key) =>
        key == Key.PrintScreen || (key >= Key.F1 && key <= Key.F24);

    /// <summary>(수정자, 키) → "Ctrl+Shift+G". ParseHotkeyString이 그대로 해석 가능한 정규형.</summary>
    public static string Format(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(KeyToken(key));
        return string.Join("+", parts);
    }

    private static string KeyToken(Key key)
    {
        // WPF에서 Key.PrintScreen == Key.Snapshot(동일 값)이라 ToString()이 "Snapshot"을 반환.
        // 백엔드/기본값은 "PrintScreen"을 쓰므로 명시 보정.
        if (key == Key.PrintScreen) return "PrintScreen";
        if (key >= Key.D0 && key <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();
        return key.ToString();
    }

    public static bool IsValidGlobalHotkey(ModifierKeys modifiers, Key key, out string? error)
    {
        if (key == Key.None || IsModifierKey(key))
        {
            error = "일반 키를 함께 눌러주세요.";
            return false;
        }
        if (modifiers == ModifierKeys.None && !AllowsNoModifier(key))
        {
            error = "Ctrl/Alt/Shift와 함께 눌러주세요.";
            return false;
        }
        error = null;
        return true;
    }
}
