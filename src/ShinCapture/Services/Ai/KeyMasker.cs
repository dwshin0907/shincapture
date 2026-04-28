using System.Text.RegularExpressions;

namespace ShinCapture.Services.Ai;

/// <summary>
/// API 키 로깅/예외 메시지 노출 방지용 마스킹 유틸.
/// </summary>
public static class KeyMasker
{
    private static readonly Regex KeyPattern = new(@"sk-[A-Za-z0-9_\-]{6,}", RegexOptions.Compiled);

    /// <summary>키 자체를 prefix(3) + *** + suffix(3) 형태로 마스킹.</summary>
    public static string Mask(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 8) return "***";
        return $"{key[..3]}***{key[^3..]}";
    }

    /// <summary>임의 텍스트 안의 sk- 패턴을 모두 마스킹.</summary>
    public static string MaskInText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        return KeyPattern.Replace(text, m => Mask(m.Value));
    }
}
