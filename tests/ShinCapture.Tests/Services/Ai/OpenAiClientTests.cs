using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ShinCapture.Services.Ai;

namespace ShinCapture.Tests.Services.Ai;

public class OpenAiClientTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(Responder(request));
        }
    }

    private static (OpenAiClient client, FakeHandler handler) CreateClient()
    {
        var handler = new FakeHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com") };
        return (new OpenAiClient(http), handler);
    }

    [Fact]
    public async Task PostResponse_AttachesAuthorizationHeader()
    {
        var (client, handler) = CreateClient();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(@"{
                ""id"": ""resp_x"",
                ""object"": ""response"",
                ""output"": [
                    {
                        ""type"": ""message"",
                        ""role"": ""assistant"",
                        ""content"": [{""type"":""output_text"",""text"":""hi""}]
                    }
                ]
            }")
        };

        using var key = new AiKeyHandle("sk-abc-XYZ");
        var resp = await client.PostResponseAsync(new ResponseRequest { Model = "gpt-4o-mini" }, key);

        Assert.NotNull(resp.Output);
        Assert.Equal("hi", resp.Output![0].Content![0].Text);
        var auth = handler.LastRequest!.Headers.Authorization!;
        Assert.Equal("Bearer", auth.Scheme);
        Assert.Equal("sk-abc-XYZ", auth.Parameter);
    }

    [Fact]
    public async Task PostResponse_RejectsNonOpenAiHost()
    {
        var handler = new FakeHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://evil.example.com") };
        var client = new OpenAiClient(http);

        using var key = new AiKeyHandle("sk-abc");
        var ex = await Assert.ThrowsAsync<OpenAiException>(
            () => client.PostResponseAsync(new ResponseRequest(), key));
        Assert.Equal(OpenAiErrorKind.Unknown, ex.Kind);
    }

    [Fact]
    public async Task PostResponse_401_ThrowsInvalidKey()
    {
        var (client, handler) = CreateClient();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"bad key\"}")
        };

        using var key = new AiKeyHandle("sk-bad");
        var ex = await Assert.ThrowsAsync<OpenAiException>(
            () => client.PostResponseAsync(new ResponseRequest(), key));
        Assert.Equal(OpenAiErrorKind.InvalidKey, ex.Kind);
    }

    [Fact]
    public async Task PostResponse_429_ThrowsRateLimitedWithRetryAfter()
    {
        var (client, handler) = CreateClient();
        handler.Responder = _ =>
        {
            var resp = new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("{}")
            };
            resp.Headers.Add("Retry-After", "30");
            return resp;
        };

        using var key = new AiKeyHandle("sk-abc");
        var ex = await Assert.ThrowsAsync<OpenAiException>(
            () => client.PostResponseAsync(new ResponseRequest(), key));
        Assert.Equal(OpenAiErrorKind.RateLimited, ex.Kind);
        Assert.Equal(30, ex.RetryAfter!.Value.TotalSeconds);
    }

    [Fact]
    public async Task PostResponse_404_ThrowsModelNotFound()
    {
        var (client, handler) = CreateClient();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}")
        };

        using var key = new AiKeyHandle("sk-abc");
        var ex = await Assert.ThrowsAsync<OpenAiException>(
            () => client.PostResponseAsync(new ResponseRequest { Model = "nonexistent" }, key));
        Assert.Equal(OpenAiErrorKind.ModelNotFound, ex.Kind);
    }

    [Fact]
    public async Task PostResponse_500_ThrowsServerError()
    {
        var (client, handler) = CreateClient();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{}")
        };

        using var key = new AiKeyHandle("sk-abc");
        var ex = await Assert.ThrowsAsync<OpenAiException>(
            () => client.PostResponseAsync(new ResponseRequest(), key));
        Assert.Equal(OpenAiErrorKind.ServerError, ex.Kind);
    }

    [Fact]
    public async Task ValidateKey_GETsModelsEndpoint()
    {
        var (client, handler) = CreateClient();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":[]}")
        };

        using var key = new AiKeyHandle("sk-abc");
        var ok = await client.ValidateKeyAsync(key);
        Assert.True(ok);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.EndsWith("/v1/models", handler.LastRequest!.RequestUri!.AbsolutePath);
    }
}
