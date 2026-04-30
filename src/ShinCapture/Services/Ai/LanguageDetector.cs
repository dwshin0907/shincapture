namespace ShinCapture.Services.Ai;

/// <summary>
/// 유니코드 범위 기반 휴리스틱으로 텍스트의 주 언어를 추정.
/// 정확하지 않지만 "한국어→한국어" 같은 명백한 같은 언어 케이스를 사전 차단하기에 충분.
/// </summary>
public static class LanguageDetector
{
    /// <summary>주 언어 추정. 판단 불가하면 null. 반환값: "ko" | "en" | "ja" | "zh" | null.</summary>
    public static string? DetectSimple(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        int hangul = 0, latin = 0, hiragana = 0, katakana = 0, cjk = 0, scripted = 0;
        foreach (var c in text)
        {
            // 한글 음절 + 한글 자모
            if ((c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x3131 && c <= 0x318E))
            { hangul++; scripted++; }
            else if (c >= 0x3040 && c <= 0x309F) { hiragana++; scripted++; }
            else if (c >= 0x30A0 && c <= 0x30FF) { katakana++; scripted++; }
            else if (c >= 0x4E00 && c <= 0x9FFF) { cjk++; scripted++; }
            else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) { latin++; scripted++; }
            // 공백/숫자/기호는 비율 산정에서 제외 (scripted 카운트 안 함)
        }

        if (scripted == 0) return null;

        // 한글이 30% 이상이면 한국어로 판정 (한자 섞여도 한국어로 본다)
        if (hangul * 100 / scripted > 30) return "ko";

        // 가나(히라가나/가타카나)가 20% 이상이면 일본어
        int kana = hiragana + katakana;
        if (kana * 100 / scripted > 20) return "ja";

        // CJK가 많고 가나가 거의 없으면 중국어
        if (cjk * 100 / scripted > 30 && kana == 0) return "zh";

        // 라틴 알파벳 50% 이상이면 영어로 가정 (실제로 다른 라틴계 언어일 수 있음)
        if (latin * 100 / scripted > 50) return "en";

        return null;
    }

    /// <summary>두 언어 태그가 같은 언어를 가리키는지 (대소문자/지역코드 무시: "ko" == "ko-KR").</summary>
    public static bool IsSameLanguage(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        var aPrimary = a.Split('-', '_')[0].ToLowerInvariant();
        var bPrimary = b.Split('-', '_')[0].ToLowerInvariant();
        return aPrimary == bPrimary;
    }
}
