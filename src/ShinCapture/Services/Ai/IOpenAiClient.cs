using System.Threading;
using System.Threading.Tasks;

namespace ShinCapture.Services.Ai;

/// <summary>
/// OpenAI API의 단일 진입점. v1.3.0/v1.4.0에서 비전/이미지 메서드가 추가될 자리.
/// </summary>
public interface IOpenAiClient
{
    /// <summary>키가 유효한지(GET /v1/models) 비용 발생 없이 확인.</summary>
    Task<bool> ValidateKeyAsync(AiKeyHandle key, CancellationToken ct = default);

    /// <summary>Chat Completions 호출.</summary>
    Task<ChatResponse> PostChatAsync(ChatRequest request, AiKeyHandle key, CancellationToken ct = default);
}
