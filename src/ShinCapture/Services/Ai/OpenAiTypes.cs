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

public sealed class ChatMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "user";
    [JsonPropertyName("content")] public string Content { get; set; } = "";
}

public sealed class ChatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = "gpt-4o-mini";
    [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = new();
    [JsonPropertyName("temperature")] public double Temperature { get; set; } = 0.0;
}

public sealed class ChatResponse
{
    [JsonPropertyName("choices")] public List<ChatChoice>? Choices { get; set; }
}

public sealed class ChatChoice
{
    [JsonPropertyName("message")] public ChatMessage? Message { get; set; }
}
