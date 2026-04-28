using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShinCapture.Services.Ai;

/// <summary>
/// OCR 텍스트를 OpenAI Chat API로 번역. 키 없음/빈 텍스트/같은 언어 케이스는 호출 전후로 스킵 분기.
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

        if (!_store.HasKey())
            return new TranslationResult { Outcome = TranslationOutcome.NoKey, OriginalText = text, TargetLanguage = targetLanguage };

        using var key = _store.AcquireKey();
        if (key == null)
            return new TranslationResult { Outcome = TranslationOutcome.NoKey, OriginalText = text, TargetLanguage = targetLanguage };

        var systemPrompt =
            $"You are a translator. Translate the user message into {targetLanguage}. " +
            "If the source is already in the target language, return the source text exactly as-is. " +
            "Output ONLY the translation, no commentary, no quotes.";

        var req = new ChatRequest
        {
            Model = model,
            Temperature = 0.0,
            Messages = new List<ChatMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = text }
            }
        };

        var resp = await _openAi.PostChatAsync(req, key, ct).ConfigureAwait(false);
        var translated = resp.Choices?[0]?.Message?.Content?.Trim() ?? "";

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
