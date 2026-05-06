using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ShinCapture.Models;
using ShinCapture.Services.Ai;

namespace ShinCapture.Services;

public enum EditorOcrOutcome
{
    MissingImage,
    LanguagePackMissing,
    NoText,
    Success,
    Failed
}

public sealed class EditorOcrResult
{
    public EditorOcrOutcome Outcome { get; init; }
    public string Text { get; init; } = "";
    public string? LanguageTag { get; init; }
    public string RequestedLanguage { get; init; } = "ko";
    public string? ErrorMessage { get; init; }

    public bool UsedFallback =>
        !string.IsNullOrWhiteSpace(LanguageTag) &&
        !string.Equals(LanguageTag, RequestedLanguage, StringComparison.OrdinalIgnoreCase);
}

public enum EditorTranslationOutcome
{
    Disabled,
    NoKey,
    Success,
    SkippedSameLanguage,
    NoResult
}

public sealed class EditorTranslationResult
{
    public EditorTranslationOutcome Outcome { get; init; }
    public string OriginalText { get; init; } = "";
    public string TranslatedText { get; init; } = "";
    public string TargetLanguage { get; init; } = "ko";
}

public sealed class EditorOcrService
{
    private readonly Func<string, string?> _resolveLanguage;
    private readonly Func<Bitmap, string, bool, Task<string>> _extractTextAsync;
    private readonly Func<IAiCredentialStore> _credentialStoreFactory;
    private readonly Func<int, IOpenAiClient> _openAiClientFactory;

    public EditorOcrService()
        : this(
            OcrService.ResolveLanguageOrFallback,
            OcrService.ExtractTextAsync,
            () => new DpapiCredentialStore(),
            OpenAiClient.CreateDefault)
    {
    }

    public EditorOcrService(
        Func<string, string?> resolveLanguage,
        Func<Bitmap, string, bool, Task<string>> extractTextAsync,
        Func<IAiCredentialStore> credentialStoreFactory,
        Func<int, IOpenAiClient> openAiClientFactory)
    {
        _resolveLanguage = resolveLanguage ?? throw new ArgumentNullException(nameof(resolveLanguage));
        _extractTextAsync = extractTextAsync ?? throw new ArgumentNullException(nameof(extractTextAsync));
        _credentialStoreFactory = credentialStoreFactory ?? throw new ArgumentNullException(nameof(credentialStoreFactory));
        _openAiClientFactory = openAiClientFactory ?? throw new ArgumentNullException(nameof(openAiClientFactory));
    }

    public bool HasApiKey() => _credentialStoreFactory().HasKey();

    public async Task<EditorOcrResult> ExtractAsync(BitmapSource? source, AppSettings settings)
    {
        if (source == null)
        {
            return new EditorOcrResult
            {
                Outcome = EditorOcrOutcome.MissingImage,
                RequestedLanguage = settings.Ocr.Language
            };
        }

        var requestedLanguage = settings.Ocr.Language;
        try
        {
            var langTag = _resolveLanguage(requestedLanguage);
            if (langTag == null)
            {
                return new EditorOcrResult
                {
                    Outcome = EditorOcrOutcome.LanguagePackMissing,
                    RequestedLanguage = requestedLanguage
                };
            }

            using var bitmap = BitmapSourceToBitmap(source);
            var text = await _extractTextAsync(bitmap, langTag, settings.Ocr.UpscaleSmallImages);
            if (string.IsNullOrWhiteSpace(text))
            {
                return new EditorOcrResult
                {
                    Outcome = EditorOcrOutcome.NoText,
                    LanguageTag = langTag,
                    RequestedLanguage = requestedLanguage
                };
            }

            return new EditorOcrResult
            {
                Outcome = EditorOcrOutcome.Success,
                Text = text,
                LanguageTag = langTag,
                RequestedLanguage = requestedLanguage
            };
        }
        catch (Exception ex)
        {
            return new EditorOcrResult
            {
                Outcome = EditorOcrOutcome.Failed,
                RequestedLanguage = requestedLanguage,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<EditorTranslationResult> TranslateAsync(string text, AppSettings settings, string targetLanguage)
    {
        if (!settings.Ai.Enabled)
        {
            return new EditorTranslationResult
            {
                Outcome = EditorTranslationOutcome.Disabled,
                OriginalText = text,
                TargetLanguage = targetLanguage
            };
        }

        var store = _credentialStoreFactory();
        if (!store.HasKey())
        {
            return new EditorTranslationResult
            {
                Outcome = EditorTranslationOutcome.NoKey,
                OriginalText = text,
                TargetLanguage = targetLanguage
            };
        }

        var openAi = _openAiClientFactory(settings.Ai.TimeoutSeconds);
        var translation = await new TranslationService(store, openAi)
            .TranslateAsync(text, targetLanguage, settings.Ai.Model);

        return translation.Outcome switch
        {
            TranslationOutcome.Success => new EditorTranslationResult
            {
                Outcome = EditorTranslationOutcome.Success,
                OriginalText = translation.OriginalText,
                TranslatedText = translation.TranslatedText,
                TargetLanguage = translation.TargetLanguage
            },
            TranslationOutcome.SkippedSameLanguage => new EditorTranslationResult
            {
                Outcome = EditorTranslationOutcome.SkippedSameLanguage,
                OriginalText = translation.OriginalText,
                TranslatedText = translation.TranslatedText,
                TargetLanguage = translation.TargetLanguage
            },
            TranslationOutcome.NoKey => new EditorTranslationResult
            {
                Outcome = EditorTranslationOutcome.NoKey,
                OriginalText = translation.OriginalText,
                TargetLanguage = translation.TargetLanguage
            },
            _ => new EditorTranslationResult
            {
                Outcome = EditorTranslationOutcome.NoResult,
                OriginalText = translation.OriginalText,
                TargetLanguage = translation.TargetLanguage
            }
        };
    }

    private static Bitmap BitmapSourceToBitmap(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }
}
