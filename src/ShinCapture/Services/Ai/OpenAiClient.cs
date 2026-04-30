using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShinCapture.Services.Ai;

/// <summary>
/// OpenAI HTTP 게이트웨이. 도메인 화이트리스트(api.openai.com) + TLS 1.2/1.3 + 키 헤더 처리.
/// HttpClient는 외부에서 주입(테스트 가능성). 기본 baseAddress는 https://api.openai.com.
/// </summary>
public sealed class OpenAiClient : IOpenAiClient
{
    private const string ExpectedHost = "api.openai.com";
    private static readonly Uri DefaultBase = new("https://api.openai.com");
    private readonly HttpClient _http;

    public OpenAiClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        if (_http.BaseAddress == null) _http.BaseAddress = DefaultBase;
    }

    /// <summary>
    /// 표준 사용 시 호출 — 자체 HttpClient 생성하면서 TLS 강제.
    /// </summary>
    public static OpenAiClient CreateDefault(int timeoutSeconds = 15)
    {
        var handler = new HttpClientHandler
        {
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        };
        var http = new HttpClient(handler)
        {
            BaseAddress = DefaultBase,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        return new OpenAiClient(http);
    }

    public async Task<bool> ValidateKeyAsync(AiKeyHandle key, CancellationToken ct = default)
    {
        EnsureWhitelist();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/v1/models");
        AttachAuth(req, key);
        try
        {
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch (TaskCanceledException) { return false; }
        catch (HttpRequestException) { return false; }
    }

    public async Task<ResponseEnvelope> PostResponseAsync(ResponseRequest request, AiKeyHandle key, CancellationToken ct = default)
    {
        EnsureWhitelist();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/responses")
        {
            Content = JsonContent.Create(request)
        };
        AttachAuth(req, key);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new OpenAiException(OpenAiErrorKind.Timeout, "OpenAI 요청 타임아웃", inner: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new OpenAiException(OpenAiErrorKind.Network, "OpenAI 네트워크 오류", inner: ex);
        }

        using (resp)
        {
            switch ((int)resp.StatusCode)
            {
                case 401:
                    throw new OpenAiException(OpenAiErrorKind.InvalidKey, "API 키가 유효하지 않습니다");
                case 404:
                    throw new OpenAiException(OpenAiErrorKind.ModelNotFound, "모델을 찾을 수 없습니다");
                case 429:
                    var retry = resp.Headers.RetryAfter?.Delta;
                    throw new OpenAiException(OpenAiErrorKind.RateLimited, "OpenAI 사용 한도 초과", retryAfter: retry);
                case >= 500 and < 600:
                    throw new OpenAiException(OpenAiErrorKind.ServerError, $"OpenAI 서버 오류({(int)resp.StatusCode})");
            }

            if (!resp.IsSuccessStatusCode)
                throw new OpenAiException(OpenAiErrorKind.Unknown, $"예상치 못한 응답({(int)resp.StatusCode})");

            try
            {
                var parsed = await resp.Content.ReadFromJsonAsync<ResponseEnvelope>(cancellationToken: ct).ConfigureAwait(false);
                return parsed ?? throw new OpenAiException(OpenAiErrorKind.ParseFailed, "빈 응답");
            }
            catch (JsonException ex)
            {
                throw new OpenAiException(OpenAiErrorKind.ParseFailed, "응답 JSON 파싱 실패", inner: ex);
            }
        }
    }

    private void EnsureWhitelist()
    {
        var host = _http.BaseAddress?.Host;
        if (!string.Equals(host, ExpectedHost, StringComparison.OrdinalIgnoreCase))
            throw new OpenAiException(OpenAiErrorKind.Unknown, $"허용되지 않은 호스트: {host}");
    }

    private static void AttachAuth(HttpRequestMessage req, AiKeyHandle key)
    {
        // SecureString → 평문 변환은 헤더 설정 직전 1회, 호출 종료와 함께 메모리 폐기
        key.WithPlaintext(plain =>
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", plain);
            return 0;
        });
    }
}
