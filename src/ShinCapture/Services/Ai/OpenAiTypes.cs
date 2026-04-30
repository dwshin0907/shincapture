using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ShinCapture.Services.Ai;

public enum OpenAiErrorKind
{
    Unknown,
    NoKey,
    InvalidKey,        // 401
    RateLimited,       // 429
    ModelNotFound,     // 404
    ServerError,       // 5xx
    Network,
    Timeout,
    ParseFailed
}

public sealed class OpenAiException : Exception
{
    public OpenAiErrorKind Kind { get; }
    public TimeSpan? RetryAfter { get; }

    public OpenAiException(OpenAiErrorKind kind, string message, TimeSpan? retryAfter = null, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        RetryAfter = retryAfter;
    }
}

// ── Responses API DTOs (/v1/responses) ──────────────────────────────────────

public sealed class ResponseRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "gpt-4o-mini";
    [JsonPropertyName("instructions")] public string? Instructions { get; set; }
    [JsonPropertyName("input")] public string Input { get; set; } = "";
    [JsonPropertyName("temperature")] public double? Temperature { get; set; } = 0.0;
}

public sealed class ResponseEnvelope
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("object")] public string? ObjectType { get; set; }
    [JsonPropertyName("output")] public List<ResponseOutputItem>? Output { get; set; }
}

public sealed class ResponseOutputItem
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("content")] public List<ResponseContentPart>? Content { get; set; }
}

public sealed class ResponseContentPart
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
}
