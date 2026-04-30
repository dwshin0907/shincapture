namespace ShinCapture.Services.Ai;

public enum TranslationOutcome
{
    Success,
    SkippedEmpty,
    SkippedSameLanguage,
    NoKey
}

public sealed class TranslationResult
{
    public TranslationOutcome Outcome { get; init; }
    public string OriginalText { get; init; } = "";
    public string TranslatedText { get; init; } = "";
    public string TargetLanguage { get; init; } = "ko";
}
