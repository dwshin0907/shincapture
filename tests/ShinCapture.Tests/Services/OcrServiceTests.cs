using System.Drawing;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class OcrServiceTests
{
    [Fact]
    public async Task ExtractTextAsync_EnglishTextImage_ContainsExpectedWord()
    {
        using var bitmap = CreateTextImage("Hello World", width: 400, height: 80);
        var text = await OcrService.ExtractTextAsync(bitmap, "en-US");
        Assert.Contains("Hello", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractTextAsync_BlankImage_ReturnsEmptyOrWhitespace()
    {
        using var bitmap = new Bitmap(200, 200);
        using (var g = Graphics.FromImage(bitmap))
            g.Clear(Color.White);
        var text = await OcrService.ExtractTextAsync(bitmap, "en-US");
        Assert.True(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public void IsLanguageAvailable_English_ReturnsTrue()
    {
        Assert.True(OcrService.IsLanguageAvailable("en-US"));
    }

    [Fact]
    public void IsLanguageAvailable_UnknownLanguage_ReturnsFalse()
    {
        Assert.False(OcrService.IsLanguageAvailable("zz-ZZ"));
    }

    [Fact]
    public void GetAvailableLanguages_ReturnsAtLeastEnglish()
    {
        var langs = OcrService.GetAvailableLanguages();
        Assert.Contains(langs, l => l.StartsWith("en", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExtractTextAsync_MissingLanguagePack_ThrowsInvalidOperation()
    {
        using var bitmap = new Bitmap(100, 100);
        using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.White);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => OcrService.ExtractTextAsync(bitmap, "zz-ZZ"));
    }

    private static Bitmap CreateTextImage(string text, int width, int height)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        using var font = new Font("Arial", 32);
        using var brush = new SolidBrush(Color.Black);
        g.DrawString(text, font, brush, 10, 10);
        return bmp;
    }
}
