using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Models;
using ShinCapture.Services;
using ShinCapture.Services.Ai;

namespace ShinCapture.Tests.Services;

public class EditorOcrServiceTests
{
    [Fact]
    public async Task ExtractAsync_WhenLanguagePackMissing_ReturnsLanguagePackMissing()
    {
        var service = CreateService(resolveLanguage: _ => null);

        var result = await service.ExtractAsync(CreateBitmapSource(), new AppSettings());

        Assert.Equal(EditorOcrOutcome.LanguagePackMissing, result.Outcome);
        Assert.Equal("ko", result.RequestedLanguage);
    }

    [Fact]
    public async Task ExtractAsync_WhenOcrReturnsWhitespace_ReturnsNoText()
    {
        var service = CreateService(extractTextAsync: (_, _, _) => Task.FromResult("   "));

        var result = await service.ExtractAsync(CreateBitmapSource(), new AppSettings());

        Assert.Equal(EditorOcrOutcome.NoText, result.Outcome);
        Assert.Equal("ko", result.LanguageTag);
    }

    [Fact]
    public async Task ExtractAsync_WhenOcrReturnsText_ReturnsSuccessWithFallbackInfo()
    {
        var settings = new AppSettings();
        settings.Ocr.Language = "zz-ZZ";
        var service = CreateService(
            resolveLanguage: _ => "en-US",
            extractTextAsync: (_, lang, preprocess) =>
            {
                Assert.Equal("en-US", lang);
                Assert.True(preprocess);
                return Task.FromResult("hello");
            });

        var result = await service.ExtractAsync(CreateBitmapSource(), settings);

        Assert.Equal(EditorOcrOutcome.Success, result.Outcome);
        Assert.Equal("hello", result.Text);
        Assert.Equal("en-US", result.LanguageTag);
        Assert.True(result.UsedFallback);
    }

    [Fact]
    public async Task TranslateAsync_WhenAiDisabled_ReturnsDisabled()
    {
        var settings = new AppSettings();
        settings.Ai.Enabled = false;
        var service = CreateService();

        var result = await service.TranslateAsync("hello", settings, "ko");

        Assert.Equal(EditorTranslationOutcome.Disabled, result.Outcome);
    }

    [Fact]
    public async Task TranslateAsync_WhenNoKey_ReturnsNoKey()
    {
        var settings = new AppSettings();
        settings.Ai.Enabled = true;
        var service = CreateService(storeFactory: () => new FakeStore());

        var result = await service.TranslateAsync("hello", settings, "ko");

        Assert.Equal(EditorTranslationOutcome.NoKey, result.Outcome);
    }

    [Fact]
    public async Task TranslateAsync_WhenTranslationSucceeds_ReturnsTranslatedText()
    {
        var settings = new AppSettings();
        settings.Ai.Enabled = true;
        settings.Ai.Model = "gpt-test";
        var ai = new FakeOpenAi();
        var service = CreateService(
            storeFactory: () => new FakeStore { Plaintext = "sk-test" },
            openAiFactory: _ => ai);

        var result = await service.TranslateAsync("hello", settings, "ko");

        Assert.Equal(EditorTranslationOutcome.Success, result.Outcome);
        Assert.Equal("안녕", result.TranslatedText);
        Assert.Equal("ko", result.TargetLanguage);
        Assert.Equal("gpt-test", ai.LastRequest!.Model);
    }

    private static EditorOcrService CreateService(
        Func<string, string?>? resolveLanguage = null,
        Func<System.Drawing.Bitmap, string, bool, Task<string>>? extractTextAsync = null,
        Func<IAiCredentialStore>? storeFactory = null,
        Func<int, IOpenAiClient>? openAiFactory = null)
    {
        return new EditorOcrService(
            resolveLanguage ?? (_ => "ko"),
            extractTextAsync ?? ((_, _, _) => Task.FromResult("text")),
            storeFactory ?? (() => new FakeStore { Plaintext = "sk-test" }),
            openAiFactory ?? (_ => new FakeOpenAi()));
    }

    private static BitmapSource CreateBitmapSource()
    {
        var pixels = new byte[] { 255, 255, 255, 255 };
        var source = BitmapSource.Create(
            1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
        source.Freeze();
        return source;
    }

    private sealed class FakeStore : IAiCredentialStore
    {
        public string? Plaintext { get; set; }
        public bool HasKey() => Plaintext != null;
        public bool SaveKey(string plaintext) { Plaintext = plaintext; return true; }
        public AiKeyHandle? AcquireKey() => Plaintext == null ? null : new AiKeyHandle(Plaintext);
        public void DeleteKey() => Plaintext = null;
    }

    private sealed class FakeOpenAi : IOpenAiClient
    {
        public ResponseRequest? LastRequest { get; private set; }
        public Task<bool> ValidateKeyAsync(AiKeyHandle key, CancellationToken ct = default) => Task.FromResult(true);

        public Task<ResponseEnvelope> PostResponseAsync(ResponseRequest request, AiKeyHandle key, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(new ResponseEnvelope
            {
                Output = new List<ResponseOutputItem>
                {
                    new()
                    {
                        Type = "message",
                        Role = "assistant",
                        Content = new List<ResponseContentPart>
                        {
                            new() { Type = "output_text", Text = "안녕" }
                        }
                    }
                }
            });
        }
    }
}
