using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShinCapture.Services.Ai;

/// <summary>
/// OCR 텍스트를 OpenAI Responses API로 번역. 키 없음/빈 텍스트/같은 언어 케이스는 호출 전후로 스킵 분기.
/// </summary>
public sealed class TranslationService
{
    private readonly IAiCredentialStore _store;
    private readonly IOpenAiClient _openAi;

    public TranslationService(IAiCredentialStore store, IOpenAiClient openAi)
    {
        _store = store;
        _openAi = openAi;
    }

    public async Task<TranslationResult> TranslateAsync(
        string text, string targetLanguage, string model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TranslationResult { Outcome = TranslationOutcome.SkippedEmpty, OriginalText = text ?? "", TargetLanguage = targetLanguage };

        // 클라이언트 측 언어 감지: 원문이 이미 대상 언어면 API 호출 없이 차단
        // (OpenAI가 "윤문" 식으로 살짝 바꾸는 케이스 방지 + 비용 0)
        var detected = LanguageDetector.DetectSimple(text);
        if (detected != null && LanguageDetector.IsSameLanguage(detected, targetLanguage))
        {
            return new TranslationResult
            {
                Outcome = TranslationOutcome.SkippedSameLanguage,
                OriginalText = text,
                TranslatedText = text,
                TargetLanguage = targetLanguage
            };
        }

        if (!_store.HasKey())
            return new TranslationResult { Outcome = TranslationOutcome.NoKey, OriginalText = text, TargetLanguage = targetLanguage };

        using var key = _store.AcquireKey();
        if (key == null)
            return new TranslationResult { Outcome = TranslationOutcome.NoKey, OriginalText = text, TargetLanguage = targetLanguage };

        var systemPrompt =
            $"You are a translator. Translate the user message into {targetLanguage}. " +
            "If the source is already in the target language, return the source text exactly as-is. " +
            "Output ONLY the translation, no commentary, no quotes.";

        var req = new ResponseRequest
        {
            Model = model,
            Instructions = systemPrompt,
            Input = text,
            Temperature = 0.0
        };

        var resp = await _openAi.PostResponseAsync(req, key, ct).ConfigureAwait(false);

        if (resp.Output == null || resp.Output.Count == 0)
            throw new OpenAiException(OpenAiErrorKind.ParseFailed, "응답에 output이 없습니다");

        var firstMsg = resp.Output.FirstOrDefault(o => o.Type == "message");
        if (firstMsg == null || firstMsg.Content == null || firstMsg.Content.Count == 0)
            throw new OpenAiException(OpenAiErrorKind.ParseFailed, "응답에 message content가 없습니다");

        var translated = string.Concat(firstMsg.Content
            .Where(c => c.Type == "output_text" && !string.IsNullOrEmpty(c.Text))
            .Select(c => c.Text))
            .Trim();

        if (string.IsNullOrWhiteSpace(translated))
            throw new OpenAiException(OpenAiErrorKind.ParseFailed, "응답 텍스트가 비어있습니다");

        if (string.Equals(translated, text.Trim(), StringComparison.Ordinal))
        {
            return new TranslationResult
            {
                Outcome = TranslationOutcome.SkippedSameLanguage,
                OriginalText = text,
                TranslatedText = translated,
                TargetLanguage = targetLanguage
            };
        }

        return new TranslationResult
        {
            Outcome = TranslationOutcome.Success,
            OriginalText = text,
            TranslatedText = translated,
            TargetLanguage = targetLanguage
        };
    }
}
