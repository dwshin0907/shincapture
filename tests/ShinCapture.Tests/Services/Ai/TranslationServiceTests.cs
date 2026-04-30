using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShinCapture.Services.Ai;

namespace ShinCapture.Tests.Services.Ai;

public class TranslationServiceTests
{
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
        public Func<ResponseRequest, ResponseEnvelope> Responder { get; set; } = req =>
            new ResponseEnvelope
            {
                Output = new List<ResponseOutputItem>
                {
                    new() { Type = "message", Role = "assistant",
                        Content = new List<ResponseContentPart>
                        {
                            new() { Type = "output_text", Text = "[translated]" }
                        }
                    }
                }
            };
        public ResponseRequest? LastRequest { get; private set; }
        public Task<bool> ValidateKeyAsync(AiKeyHandle key, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResponseEnvelope> PostResponseAsync(ResponseRequest request, AiKeyHandle key, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(Responder(request));
        }
    }

    [Fact]
    public async Task Translate_NoKey_ReturnsNoKeyOutcome()
    {
        var s = new TranslationService(new FakeStore(), new FakeOpenAi());
        var r = await s.TranslateAsync("hello", "ko", "gpt-4o-mini");
        Assert.Equal(TranslationOutcome.NoKey, r.Outcome);
    }

    [Fact]
    public async Task Translate_EmptyText_SkipsAndReturnsEmpty()
    {
        var store = new FakeStore { Plaintext = "sk-x" };
        var s = new TranslationService(store, new FakeOpenAi());
        var r = await s.TranslateAsync("   ", "ko", "gpt-4o-mini");
        Assert.Equal(TranslationOutcome.SkippedEmpty, r.Outcome);
    }

    [Fact]
    public async Task Translate_NormalCase_ReturnsTranslated()
    {
        var store = new FakeStore { Plaintext = "sk-x" };
        var ai = new FakeOpenAi
        {
            Responder = req => new ResponseEnvelope
            {
                Output = new List<ResponseOutputItem>
                {
                    new() { Type = "message", Role = "assistant",
                        Content = new List<ResponseContentPart>
                        {
                            new() { Type = "output_text", Text = "안녕" }
                        }
                    }
                }
            }
        };
        var s = new TranslationService(store, ai);
        var r = await s.TranslateAsync("hello", "ko", "gpt-4o-mini");
        Assert.Equal(TranslationOutcome.Success, r.Outcome);
        Assert.Equal("안녕", r.TranslatedText);
        Assert.Equal("hello", r.OriginalText);
    }

    [Fact]
    public async Task Translate_SameLanguageEcho_ReturnsSkippedSameLanguage()
    {
        var store = new FakeStore { Plaintext = "sk-x" };
        var ai = new FakeOpenAi
        {
            // 모델이 원문 그대로 돌려보내는 시나리오
            Responder = req => new ResponseEnvelope
            {
                Output = new List<ResponseOutputItem>
                {
                    new() { Type = "message", Content = new List<ResponseContentPart>
                    {
                        new() { Type = "output_text", Text = req.Input }
                    }}
                }
            }
        };
        var s = new TranslationService(store, ai);
        var r = await s.TranslateAsync("안녕하세요", "ko", "gpt-4o-mini");
        Assert.Equal(TranslationOutcome.SkippedSameLanguage, r.Outcome);
    }

    [Fact]
    public async Task Translate_PassesModelAndTargetLangThrough()
    {
        var store = new FakeStore { Plaintext = "sk-x" };
        var ai = new FakeOpenAi();
        var s = new TranslationService(store, ai);
        await s.TranslateAsync("hello", "ja", "gpt-4o");
        Assert.Equal("gpt-4o", ai.LastRequest!.Model);
        Assert.Contains("ja", ai.LastRequest.Instructions); // 시스템 프롬프트(Instructions)에 대상 언어 포함
    }

    [Fact]
    public async Task Translate_EmptyOutput_ThrowsParseFailed()
    {
        var store = new FakeStore { Plaintext = "sk-x" };
        var ai = new FakeOpenAi
        {
            Responder = req => new ResponseEnvelope { Output = new List<ResponseOutputItem>() }
        };
        var s = new TranslationService(store, ai);
        var ex = await Assert.ThrowsAsync<OpenAiException>(
            () => s.TranslateAsync("hello", "ko", "gpt-4o-mini"));
        Assert.Equal(OpenAiErrorKind.ParseFailed, ex.Kind);
    }
}
