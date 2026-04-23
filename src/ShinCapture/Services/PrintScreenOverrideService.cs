using Microsoft.Win32;

namespace ShinCapture.Services;

/// <summary>
/// Windows 11의 "PrtSc 키로 캡쳐 도구 열기" 동작을 on/off 한다.
/// 레지스트리: HKCU\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled (DWORD)
///   1 = Windows 기본 (PrtSc → Snipping Tool)
///   0 = 비활성화 (PrtSc가 앱 핫키로 전달됨)
/// </summary>
public static class PrintScreenOverrideService
{
    private const string KeyPath = @"Control Panel\Keyboard";
    private const string ValueName = "PrintScreenKeyForSnippingEnabled";

    /// <summary>설정값에 맞춰 레지스트리를 동기화한다.</summary>
    /// <param name="overrideEnabled">true면 신캡쳐가 PrtSc 독점, false면 Windows 기본 복원.</param>
    public static bool Apply(bool overrideEnabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
            if (key == null) return false;

            int desired = overrideEnabled ? 0 : 1;
            var current = key.GetValue(ValueName);
            if (current is int v && v == desired) return true;

            key.SetValue(ValueName, desired, RegistryValueKind.DWord);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
