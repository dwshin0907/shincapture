using System.Drawing;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class OcrServiceTests
{
    [Fact]
    public void GetAvailableLanguages_ReturnsAtLeastOne()
    {
        // 모든 현대 Windows는 최소 1개의 OCR 언어팩을 포함
        var langs = OcrService.GetAvailableLanguages();
        Assert.NotEmpty(langs);
    }

    [Fact]
    public void IsLanguageAvailable_FirstAvailable_ReturnsTrue()
    {
        var first = OcrService.GetAvailableLanguages().FirstOrDefault();
        Assert.NotNull(first);
        Assert.True(OcrService.IsLanguageAvailable(first));
    }

    [Fact]
    public void IsLanguageAvailable_UnknownLanguage_ReturnsFalse()
    {
        Assert.False(OcrService.IsLanguageAvailable("zz-ZZ"));
    }

    [Fact]
    public async Task ExtractTextAsync_BlankImage_ReturnsEmptyOrWhitespace()
    {
        var lang = OcrService.GetAvailableLanguages().First();
        using var bitmap = new Bitmap(200, 200);
        using (var g = Graphics.FromImage(bitmap))
            g.Clear(Color.White);
        var text = await OcrService.ExtractTextAsync(bitmap, lang);
        Assert.True(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public async Task ExtractTextAsync_NonEmptyTextImage_ReturnsNonEmpty()
    {
        // 설치된 언어에 맞는 간단한 텍스트 이미지로 테스트
        var lang = OcrService.GetAvailableLanguages().First();
        var sample = SampleTextFor(lang);
        using var bitmap = CreateTextImage(sample, width: 500, height: 100);
        var text = await OcrService.ExtractTextAsync(bitmap, lang);
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public async Task ExtractTextAsync_MissingLanguagePack_ThrowsInvalidOperation()
    {
        using var bitmap = new Bitmap(100, 100);
        using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.White);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => OcrService.ExtractTextAsync(bitmap, "zz-ZZ"));
    }

    [Fact]
    public async Task ExtractTextAsync_NullImage_ThrowsArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => OcrService.ExtractTextAsync(null!, "en-US"));
    }

    [Fact]
    public async Task ExtractTextAsync_EmptyLangTag_ThrowsArgument()
    {
        using var bitmap = new Bitmap(50, 50);
        await Assert.ThrowsAsync<ArgumentException>(
            () => OcrService.ExtractTextAsync(bitmap, ""));
    }

    private static string SampleTextFor(string langTag)
    {
        // OCR 엔진이 쉽게 인식하는 간단한 문자열
        if (langTag.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return "안녕 세계";
        if (langTag.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "こんにちは";
        if (langTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "你好世界";
        return "Hello World";
    }

    private static Bitmap CreateTextImage(string text, int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        // 한국어는 Malgun Gothic, 나머지는 Arial. 없으면 기본 폰트
        var fontName = System.IO.File.Exists(@"C:\Windows\Fonts\malgun.ttf") ? "Malgun Gothic" : "Arial";
        using var font = new Font(fontName, 32);
        using var brush = new SolidBrush(Color.Black);
        g.DrawString(text, font, brush, 10, 10);
        return bmp;
    }
}
