using ShinCapture.Services.Ai;

namespace ShinCapture.Tests.Services.Ai;

public class LanguageDetectorTests
{
    [Theory]
    [InlineData("안녕하세요. 오늘 날씨가 좋네요.", "ko")]
    [InlineData("이것은 한국어 텍스트입니다", "ko")]
    public void DetectSimple_Korean(string text, string expected)
    {
        Assert.Equal(expected, LanguageDetector.DetectSimple(text));
    }

    [Theory]
    [InlineData("Hello, this is sample text.", "en")]
    [InlineData("The quick brown fox jumps over the lazy dog", "en")]
    public void DetectSimple_English(string text, string expected)
    {
        Assert.Equal(expected, LanguageDetector.DetectSimple(text));
    }

    [Theory]
    [InlineData("こんにちは、今日はいい天気ですね", "ja")]
    [InlineData("カタカナとひらがなの混合", "ja")]
    public void DetectSimple_Japanese(string text, string expected)
    {
        Assert.Equal(expected, LanguageDetector.DetectSimple(text));
    }

    [Fact]
    public void DetectSimple_Chinese_NoKana()
    {
        // 가나가 없는 한자 텍스트
        Assert.Equal("zh", LanguageDetector.DetectSimple("你好世界这是中文文本"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345 !@#$%")]
    public void DetectSimple_NoScript_ReturnsNull(string text)
    {
        Assert.Null(LanguageDetector.DetectSimple(text));
    }

    [Fact]
    public void DetectSimple_Null_ReturnsNull()
    {
        Assert.Null(LanguageDetector.DetectSimple(null));
    }

    [Theory]
    [InlineData("ko", "ko", true)]
    [InlineData("ko", "ko-KR", true)]
    [InlineData("ko-KR", "ko", true)]
    [InlineData("KO", "ko", true)]
    [InlineData("ko", "en", false)]
    [InlineData("zh-CN", "zh-TW", true)]   // primary subtag만 비교
    [InlineData("", "ko", false)]
    [InlineData("ko", null, false)]
    public void IsSameLanguage(string? a, string? b, bool expected)
    {
        Assert.Equal(expected, LanguageDetector.IsSameLanguage(a, b));
    }
}
