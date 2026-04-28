# 신캡쳐 v1.2.0 OCR 번역 + AI 인프라 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** v1.1.0의 OCR 결과를 OpenAI BYOK로 번역하는 기능을 추가하고, v1.3.0/v1.4.0에서 재사용할 보안 키 저장 + OpenAI 게이트웨이 인프라를 구축한다.

**Architecture:** `Services/Ai/` 폴더에 `IAiCredentialStore` + `DpapiCredentialStore`(DPAPI 키 저장), `OpenAiClient`(HTTP 게이트웨이, 도메인 화이트리스트), `TranslationService`(번역 도메인) 4개 모듈을 추가. 키는 `apikey.dat` 파일에 DPAPI/CurrentUser scope로 암호화 저장하고 `SecureString`으로 메모리 라이프사이클 관리.

**Tech Stack:** .NET 8 (`net8.0-windows10.0.19041.0`), WPF, WinForms, `System.Security.Cryptography.ProtectedData`(DPAPI), `System.Net.Http`, `System.Text.Json`, xUnit.

**Spec:** `docs/superpowers/specs/2026-04-28-shincapture-ai-translation-design.md`

---

## 사전 안내

- **로컬 커밋만**, push는 v1.2.0 코드 검증 후 사용자 결정. 각 task 끝에 로컬 commit.
- 빌드 실행 시 `C:\Users\popol\dotnet-sdk2\dotnet.exe`를 사용 (non-standard path).
- `.csproj`에는 `System.Security.Cryptography.ProtectedData` 패키지 추가가 필요함 (DPAPI 호출용, .NET 8에서는 별도 패키지).
- 통합 테스트(실제 OpenAI 호출)는 환경변수 `OPENAI_API_KEY`가 설정됐을 때만 실행되도록 조건부 처리.

---

## Task 1: AppSettings에 AiSettings 추가 + 라운드트립 테스트

**Files:**
- Modify: `src/ShinCapture/Models/AppSettings.cs`
- Test: `tests/ShinCapture.Tests/Services/SettingsManagerTests.cs`

- [ ] **Step 1: 실패 테스트 추가**

`tests/ShinCapture.Tests/Services/SettingsManagerTests.cs`에 추가:

```csharp
[Fact]
public void RoundTrip_PreservesAiSettings()
{
    var dir = Path.Combine(Path.GetTempPath(), "shincap_test_" + Guid.NewGuid());
    try
    {
        var mgr = new SettingsManager(dir);
        var s = new AppSettings();
        s.Ai.Enabled = true;
        s.Ai.Model = "gpt-4o-mini";
        s.Ai.TargetLanguage = "ko";
        mgr.Save(s);

        var loaded = new SettingsManager(dir).Load();
        Assert.True(loaded.Ai.Enabled);
        Assert.Equal("gpt-4o-mini", loaded.Ai.Model);
        Assert.Equal("ko", loaded.Ai.TargetLanguage);
    }
    finally
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }
}
```

- [ ] **Step 2: 테스트 실행해서 컴파일 실패 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter "RoundTrip_PreservesAiSettings"`
Expected: COMPILATION FAIL — `AppSettings.Ai` 속성 없음

- [ ] **Step 3: AppSettings.cs에 AiSettings 추가**

`src/ShinCapture/Models/AppSettings.cs` 수정 — 클래스 `AppSettings`에 속성 추가:

```csharp
public AiSettings Ai { get; set; } = new();
```

같은 파일 끝에 클래스 추가:

```csharp
public class AiSettings
{
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "openai";
    public string Model { get; set; } = "gpt-4o-mini";
    public string TargetLanguage { get; set; } = "ko";
    public int TimeoutSeconds { get; set; } = 15;
    public DateTime? LastValidatedAt { get; set; } = null;
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test tests/ShinCapture.Tests/ShinCapture.Tests.csproj --filter "RoundTrip_PreservesAiSettings"`
Expected: PASS

- [ ] **Step 5: 전체 테스트 회귀 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test tests/ShinCapture.Tests/ShinCapture.Tests.csproj`
Expected: 모든 기존 테스트 + 새 테스트 통과

- [ ] **Step 6: 커밋**

```bash
git add src/ShinCapture/Models/AppSettings.cs tests/ShinCapture.Tests/Services/SettingsManagerTests.cs
git commit -m "feat: add AiSettings to AppSettings model"
```

---

## Task 2: System.Security.Cryptography.ProtectedData 패키지 추가

**Files:**
- Modify: `src/ShinCapture/ShinCapture.csproj`

- [ ] **Step 1: csproj에 패키지 추가**

`src/ShinCapture/ShinCapture.csproj`의 `<ItemGroup>` 안 (다른 PackageReference 옆)에 추가:

```xml
<PackageReference Include="System.Security.Cryptography.ProtectedData" Version="8.*" />
```

- [ ] **Step 2: 빌드 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe build src/ShinCapture/ShinCapture.csproj`
Expected: PASS, 새 패키지 다운로드/복원 메시지

- [ ] **Step 3: 커밋**

```bash
git add src/ShinCapture/ShinCapture.csproj
git commit -m "build: add ProtectedData package for DPAPI key storage"
```

---

## Task 3: KeyMasker 유틸 + 단위 테스트

**Files:**
- Create: `src/ShinCapture/Services/Ai/KeyMasker.cs`
- Test: `tests/ShinCapture.Tests/Services/Ai/KeyMaskerTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`tests/ShinCapture.Tests/Services/Ai/KeyMaskerTests.cs` 신규:

```csharp
using ShinCapture.Services.Ai;

namespace ShinCapture.Tests.Services.Ai;

public class KeyMaskerTests
{
    [Fact]
    public void Mask_TypicalKey_ShowsPrefixAndSuffix()
    {
        var s = KeyMasker.Mask("sk-1234567890abcdefXYZ");
        Assert.StartsWith("sk-", s);
        Assert.EndsWith("XYZ", s);
        Assert.Contains("***", s);
        Assert.DoesNotContain("1234567890", s);
    }

    [Fact]
    public void Mask_ShortKey_ReturnsAllAsterisks()
    {
        var s = KeyMasker.Mask("abc");
        Assert.Equal("***", s);
    }

    [Fact]
    public void Mask_Null_ReturnsAsterisks()
    {
        Assert.Equal("***", KeyMasker.Mask(null));
    }

    [Fact]
    public void MaskInText_ReplacesEmbeddedKey()
    {
        var input = "Bearer sk-1234567890abcdefXYZ failed";
        var masked = KeyMasker.MaskInText(input);
        Assert.DoesNotContain("1234567890", masked);
        Assert.Contains("sk-***", masked);
    }
}
```

- [ ] **Step 2: 테스트 실행해서 컴파일 실패 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test --filter "FullyQualifiedName~KeyMaskerTests"`
Expected: COMPILATION FAIL — `KeyMasker` 없음

- [ ] **Step 3: KeyMasker 구현**

`src/ShinCapture/Services/Ai/KeyMasker.cs` 신규:

```csharp
using System.Text.RegularExpressions;

namespace ShinCapture.Services.Ai;

/// <summary>
/// API 키 로깅/예외 메시지 노출 방지용 마스킹 유틸.
/// </summary>
public static class KeyMasker
{
    private static readonly Regex KeyPattern = new(@"sk-[A-Za-z0-9_\-]{6,}", RegexOptions.Compiled);

    /// <summary>키 자체를 prefix(3) + *** + suffix(3) 형태로 마스킹.</summary>
    public static string Mask(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 8) return "***";
        return $"{key[..3]}***{key[^3..]}";
    }

    /// <summary>임의 텍스트 안의 sk- 패턴을 모두 마스킹.</summary>
    public static string MaskInText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        return KeyPattern.Replace(text, m => Mask(m.Value));
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test --filter "FullyQualifiedName~KeyMaskerTests"`
Expected: PASS

- [ ] **Step 5: 커밋**

```bash
git add src/ShinCapture/Services/Ai/KeyMasker.cs tests/ShinCapture.Tests/Services/Ai/KeyMaskerTests.cs
git commit -m "feat: add KeyMasker for safe API key logging"
```

---

## Task 4: IAiCredentialStore 인터페이스 + AiKeyHandle (SecureString 래퍼)

**Files:**
- Create: `src/ShinCapture/Services/Ai/AiKeyHandle.cs`
- Create: `src/ShinCapture/Services/Ai/IAiCredentialStore.cs`

- [ ] **Step 1: AiKeyHandle 작성 (SecureString을 래핑하고 Dispose 시 0-fill)**

`src/ShinCapture/Services/Ai/AiKeyHandle.cs` 신규:

```csharp
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace ShinCapture.Services.Ai;

/// <summary>
/// SecureString을 감싸는 일회성 핸들. Dispose 시 메모리 0-fill로 평문 흔적 제거.
/// `using` 블록 안에서만 평문(<see cref="WithPlaintext"/>)을 짧게 사용하고 즉시 폐기한다.
/// </summary>
public sealed class AiKeyHandle : IDisposable
{
    private SecureString? _secure;

    public AiKeyHandle(string plaintext)
    {
        if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
        _secure = new SecureString();
        foreach (var c in plaintext) _secure.AppendChar(c);
        _secure.MakeReadOnly();
    }

    /// <summary>평문이 필요한 짧은 영역에서만 사용. 콜백 종료 시 즉시 메모리에서 폐기.</summary>
    public T WithPlaintext<T>(Func<string, T> action)
    {
        if (_secure == null) throw new ObjectDisposedException(nameof(AiKeyHandle));
        IntPtr ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.SecureStringToGlobalAllocUnicode(_secure);
            var plain = Marshal.PtrToStringUni(ptr) ?? string.Empty;
            return action(plain);
        }
        finally
        {
            if (ptr != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(ptr);
        }
    }

    public void Dispose()
    {
        _secure?.Dispose();
        _secure = null;
    }
}
```

- [ ] **Step 2: IAiCredentialStore 작성**

`src/ShinCapture/Services/Ai/IAiCredentialStore.cs` 신규:

```csharp
namespace ShinCapture.Services.Ai;

/// <summary>
/// API 키 저장소 추상화. DPAPI 구현은 Windows 의존이라 단위 테스트는 fake로 대체.
/// </summary>
public interface IAiCredentialStore
{
    /// <summary>키 존재 여부 (디스크 또는 메모리).</summary>
    bool HasKey();

    /// <summary>키를 저장(평문은 즉시 암호화). 성공 시 true.</summary>
    bool SaveKey(string plaintext);

    /// <summary>키를 일회성 핸들로 로드. 키 없으면 null.</summary>
    AiKeyHandle? AcquireKey();

    /// <summary>키 파일/메모리를 삭제.</summary>
    void DeleteKey();
}
```

- [ ] **Step 3: 빌드 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe build src/ShinCapture/ShinCapture.csproj`
Expected: PASS

- [ ] **Step 4: 커밋**

```bash
git add src/ShinCapture/Services/Ai/AiKeyHandle.cs src/ShinCapture/Services/Ai/IAiCredentialStore.cs
git commit -m "feat: add IAiCredentialStore interface and AiKeyHandle"
```

---

## Task 5: DpapiCredentialStore 구현 + 테스트

**Files:**
- Create: `src/ShinCapture/Services/Ai/DpapiCredentialStore.cs`
- Create: `tests/ShinCapture.Tests/Services/Ai/DpapiCredentialStoreTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`tests/ShinCapture.Tests/Services/Ai/DpapiCredentialStoreTests.cs` 신규:

```csharp
using System.IO;
using ShinCapture.Services.Ai;

namespace ShinCapture.Tests.Services.Ai;

public class DpapiCredentialStoreTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "shincap_dpapi_" + Guid.NewGuid() + ".dat");

    [Fact]
    public void HasKey_NoFile_ReturnsFalse()
    {
        var p = TempPath();
        var s = new DpapiCredentialStore(p);
        Assert.False(s.HasKey());
    }

    [Fact]
    public void SaveAndAcquire_RoundTrip()
    {
        var p = TempPath();
        try
        {
            var s = new DpapiCredentialStore(p);
            Assert.True(s.SaveKey("sk-test-XYZ"));
            Assert.True(s.HasKey());

            using var h = s.AcquireKey();
            Assert.NotNull(h);
            var plain = h!.WithPlaintext(plain => plain);
            Assert.Equal("sk-test-XYZ", plain);
        }
        finally
        {
            if (File.Exists(p)) File.Delete(p);
        }
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        var p = TempPath();
        var s = new DpapiCredentialStore(p);
        s.SaveKey("sk-temp");
        Assert.True(File.Exists(p));
        s.DeleteKey();
        Assert.False(File.Exists(p));
        Assert.False(s.HasKey());
    }

    [Fact]
    public void AcquireKey_NoFile_ReturnsNull()
    {
        var p = TempPath();
        var s = new DpapiCredentialStore(p);
        Assert.Null(s.AcquireKey());
    }

    [Fact]
    public void SavedFile_IsNotPlaintext()
    {
        var p = TempPath();
        try
        {
            var s = new DpapiCredentialStore(p);
            s.SaveKey("sk-plaintext-leak-check-XYZ");
            var bytes = File.ReadAllBytes(p);
            // 평문이 그대로 디스크에 있으면 안 됨
            var asText = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.DoesNotContain("plaintext-leak-check", asText);
        }
        finally
        {
            if (File.Exists(p)) File.Delete(p);
        }
    }
}
```

- [ ] **Step 2: 테스트 컴파일 실패 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test --filter "FullyQualifiedName~DpapiCredentialStoreTests"`
Expected: COMPILATION FAIL — `DpapiCredentialStore` 없음

- [ ] **Step 3: 구현**

`src/ShinCapture/Services/Ai/DpapiCredentialStore.cs` 신규:

```csharp
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ShinCapture.Services.Ai;

/// <summary>
/// Windows DPAPI(CurrentUser scope)로 API 키를 파일에 암호화 저장.
/// 같은 PC + 같은 Windows 사용자 계정에서만 복호화 가능.
/// </summary>
public sealed class DpapiCredentialStore : IAiCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ShinCapture.Ai.v1");
    private readonly string _filePath;

    /// <param name="filePath">기본값: %APPDATA%\ShinCapture\apikey.dat</param>
    public DpapiCredentialStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShinCapture", "apikey.dat");
    }

    public bool HasKey() => File.Exists(_filePath);

    public bool SaveKey(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return false;
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var raw = Encoding.UTF8.GetBytes(plaintext);
            var encrypted = ProtectedData.Protect(raw, Entropy, DataProtectionScope.CurrentUser);
            // 평문 raw를 즉시 0으로 덮어씀
            Array.Clear(raw, 0, raw.Length);

            File.WriteAllBytes(_filePath, encrypted);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public AiKeyHandle? AcquireKey()
    {
        if (!File.Exists(_filePath)) return null;
        try
        {
            var encrypted = File.ReadAllBytes(_filePath);
            var raw = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                var plain = Encoding.UTF8.GetString(raw);
                return new AiKeyHandle(plain);
            }
            finally
            {
                Array.Clear(raw, 0, raw.Length);
            }
        }
        catch
        {
            return null;
        }
    }

    public void DeleteKey()
    {
        try
        {
            if (File.Exists(_filePath)) File.Delete(_filePath);
        }
        catch
        {
            // 파일 잠금 등 — 무시 (다음 호출에서 재시도 가능)
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test --filter "FullyQualifiedName~DpapiCredentialStoreTests"`
Expected: 5개 테스트 PASS

- [ ] **Step 5: 커밋**

```bash
git add src/ShinCapture/Services/Ai/DpapiCredentialStore.cs tests/ShinCapture.Tests/Services/Ai/DpapiCredentialStoreTests.cs
git commit -m "feat: add DpapiCredentialStore for encrypted key storage"
```

---

## Task 6: OpenAiTypes (DTO + Exception) + IOpenAiClient

**Files:**
- Create: `src/ShinCapture/Services/Ai/OpenAiTypes.cs`
- Create: `src/ShinCapture/Services/Ai/IOpenAiClient.cs`

- [ ] **Step 1: OpenAiTypes 작성**

`src/ShinCapture/Services/Ai/OpenAiTypes.cs` 신규:

```csharp
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
```

- [ ] **Step 2: IOpenAiClient 작성**

`src/ShinCapture/Services/Ai/IOpenAiClient.cs` 신규:

```csharp
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
```

- [ ] **Step 3: 빌드 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe build src/ShinCapture/ShinCapture.csproj`
Expected: PASS

- [ ] **Step 4: 커밋**

```bash
git add src/ShinCapture/Services/Ai/OpenAiTypes.cs src/ShinCapture/Services/Ai/IOpenAiClient.cs
git commit -m "feat: add OpenAI DTO types and IOpenAiClient interface"
```

---

## Task 7: OpenAiClient 구현 + HttpMessageHandler 모킹 테스트

**Files:**
- Create: `src/ShinCapture/Services/Ai/OpenAiClient.cs`
- Create: `tests/ShinCapture.Tests/Services/Ai/OpenAiClientTests.cs`

- [ ] **Step 1: 테스트용 fake handler + 실패 테스트 작성**

`tests/ShinCapture.Tests/Services/Ai/OpenAiClientTests.cs` 신규:

```csharp
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
        var http = new HttpClient(handler);
        return (new OpenAiClient(http), handler);
    }

    [Fact]
    public async Task PostChat_AttachesAuthorizationHeader()
    {
        var (client, handler) = CreateClient();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"hi\"}}]}")
        };

        using var key = new AiKeyHandle("sk-abc-XYZ");
        var resp = await client.PostChatAsync(new ChatRequest { Model = "gpt-4o-mini" }, key);

        Assert.Equal("hi", resp.Choices![0].Message!.Content);
        var auth = handler.LastRequest!.Headers.Authorization!;
        Assert.Equal("Bearer", auth.Scheme);
        Assert.Equal("sk-abc-XYZ", auth.Parameter);
    }

    [Fact]
    public async Task PostChat_RejectsNonOpenAiHost()
    {
        var handler = new FakeHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://evil.example.com") };
        var client = new OpenAiClient(http);

        using var key = new AiKeyHandle("sk-abc");
        var ex = await Assert.ThrowsAsync<OpenAiException>(
            () => client.PostChatAsync(new ChatRequest(), key));
        Assert.Equal(OpenAiErrorKind.Unknown, ex.Kind);
    }

    [Fact]
    public async Task PostChat_401_ThrowsInvalidKey()
    {
        var (client, handler) = CreateClient();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"bad key\"}")
        };

        using var key = new AiKeyHandle("sk-bad");
        var ex = await Assert.ThrowsAsync<OpenAiException>(
            () => client.PostChatAsync(new ChatRequest(), key));
        Assert.Equal(OpenAiErrorKind.InvalidKey, ex.Kind);
    }

    [Fact]
    public async Task PostChat_429_ThrowsRateLimitedWithRetryAfter()
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
            () => client.PostChatAsync(new ChatRequest(), key));
        Assert.Equal(OpenAiErrorKind.RateLimited, ex.Kind);
        Assert.Equal(30, ex.RetryAfter!.Value.TotalSeconds);
    }

    [Fact]
    public async Task PostChat_404_ThrowsModelNotFound()
    {
        var (client, handler) = CreateClient();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}")
        };

        using var key = new AiKeyHandle("sk-abc");
        var ex = await Assert.ThrowsAsync<OpenAiException>(
            () => client.PostChatAsync(new ChatRequest { Model = "nonexistent" }, key));
        Assert.Equal(OpenAiErrorKind.ModelNotFound, ex.Kind);
    }

    [Fact]
    public async Task PostChat_500_ThrowsServerError()
    {
        var (client, handler) = CreateClient();
        handler.Responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{}")
        };

        using var key = new AiKeyHandle("sk-abc");
        var ex = await Assert.ThrowsAsync<OpenAiException>(
            () => client.PostChatAsync(new ChatRequest(), key));
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
```

- [ ] **Step 2: 테스트 컴파일 실패 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test --filter "FullyQualifiedName~OpenAiClientTests"`
Expected: COMPILATION FAIL — `OpenAiClient` 없음

- [ ] **Step 3: OpenAiClient 구현**

`src/ShinCapture/Services/Ai/OpenAiClient.cs` 신규:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                           System.Security.Authentication.SslProtocols.Tls13
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

    public async Task<ChatResponse> PostChatAsync(ChatRequest request, AiKeyHandle key, CancellationToken ct = default)
    {
        EnsureWhitelist();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
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
                var parsed = await resp.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct).ConfigureAwait(false);
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
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test --filter "FullyQualifiedName~OpenAiClientTests"`
Expected: 7개 테스트 PASS

- [ ] **Step 5: 커밋**

```bash
git add src/ShinCapture/Services/Ai/OpenAiClient.cs tests/ShinCapture.Tests/Services/Ai/OpenAiClientTests.cs
git commit -m "feat: implement OpenAiClient with whitelist and error mapping"
```

---

## Task 8: TranslationService + 모킹 테스트

**Files:**
- Create: `src/ShinCapture/Services/Ai/TranslationService.cs`
- Create: `src/ShinCapture/Services/Ai/TranslationResult.cs`
- Create: `tests/ShinCapture.Tests/Services/Ai/TranslationServiceTests.cs`

- [ ] **Step 1: TranslationResult DTO 작성**

`src/ShinCapture/Services/Ai/TranslationResult.cs` 신규:

```csharp
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
```

- [ ] **Step 2: 실패 테스트 작성**

`tests/ShinCapture.Tests/Services/Ai/TranslationServiceTests.cs` 신규:

```csharp
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
        public Func<ChatRequest, ChatResponse> Responder { get; set; } = req =>
            new ChatResponse { Choices = new() { new ChatChoice { Message = new ChatMessage { Content = "[translated]" } } } };
        public ChatRequest? LastRequest { get; private set; }
        public Task<bool> ValidateKeyAsync(AiKeyHandle key, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ChatResponse> PostChatAsync(ChatRequest request, AiKeyHandle key, CancellationToken ct = default)
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
            Responder = req => new ChatResponse
            {
                Choices = new() { new ChatChoice { Message = new ChatMessage { Content = "안녕" } } }
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
            Responder = req => new ChatResponse
            {
                Choices = new() { new ChatChoice { Message = new ChatMessage { Content = req.Messages[^1].Content } } }
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
        Assert.Contains("ja", ai.LastRequest.Messages[0].Content); // 시스템 프롬프트에 대상 언어 포함
    }
}
```

- [ ] **Step 3: 테스트 컴파일 실패 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test --filter "FullyQualifiedName~TranslationServiceTests"`
Expected: COMPILATION FAIL — `TranslationService` 없음

- [ ] **Step 4: TranslationService 구현**

`src/ShinCapture/Services/Ai/TranslationService.cs` 신규:

```csharp
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
```

- [ ] **Step 5: 테스트 통과 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test --filter "FullyQualifiedName~TranslationServiceTests"`
Expected: 5개 테스트 PASS

- [ ] **Step 6: 커밋**

```bash
git add src/ShinCapture/Services/Ai/TranslationService.cs src/ShinCapture/Services/Ai/TranslationResult.cs tests/ShinCapture.Tests/Services/Ai/TranslationServiceTests.cs
git commit -m "feat: implement TranslationService with same-language detection"
```

---

## Task 9: HotkeySettings에 TranslateCapture 추가

**Files:**
- Modify: `src/ShinCapture/Models/AppSettings.cs`

- [ ] **Step 1: HotkeySettings에 속성 추가**

`src/ShinCapture/Models/AppSettings.cs`의 `HotkeySettings` 클래스에 `TextCapture` 다음 줄 추가:

```csharp
public string TranslateCapture { get; set; } = "Ctrl+Shift+L";
```

- [ ] **Step 2: 빌드 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe build src/ShinCapture/ShinCapture.csproj`
Expected: PASS

- [ ] **Step 3: 커밋**

```bash
git add src/ShinCapture/Models/AppSettings.cs
git commit -m "feat: add TranslateCapture hotkey setting"
```

---

## Task 10: MainWindow에 Ctrl+Shift+L 단축키 + RunOcrAndTranslate

**Files:**
- Modify: `src/ShinCapture/Models/CaptureMode.cs` (`Translate` enum 값 추가)
- Modify: `src/ShinCapture/Views/MainWindow.xaml.cs`

- [ ] **Step 1: CaptureMode에 Translate 추가**

`src/ShinCapture/Models/CaptureMode.cs` 확인 후 enum에 추가:

```csharp
public enum CaptureMode
{
    Region, Freeform, Window, Element, Fullscreen, Scroll, FixedSize, Text,
    Translate   // 신규: 영역 → OCR → 번역 한 큐
}
```

- [ ] **Step 2: MainWindow에 등록 (RegisterHotkeys 메서드)**

`src/ShinCapture/Views/MainWindow.xaml.cs`의 `RegisterHotkeys()` 안에 `TextCapture` 등록 다음 줄 추가:

```csharp
_hotkeyManager.Register(_settings.Hotkeys.TranslateCapture, () => StartCapture(CaptureMode.Translate));
```

- [ ] **Step 3: HandleCaptureResult 안에 Translate 분기 추가**

`HandleCaptureResult` 메서드의 `if (_lastCaptureMode == CaptureMode.Text)` 블록 바로 아래에 추가:

```csharp
if (_lastCaptureMode == CaptureMode.Translate)
{
    RunOcrAndTranslateAndNotify(result.Image);
    return;
}
```

- [ ] **Step 4: RunOcrAndTranslateAndNotify 메서드 추가**

`RunOcrAndNotify` 메서드 바로 아래에 추가 (위치는 같은 클래스 내):

```csharp
private async void RunOcrAndTranslateAndNotify(System.Drawing.Bitmap image)
{
    var langTag = Services.OcrService.ResolveLanguageOrFallback(_settings.Ocr.Language);
    if (langTag == null)
    {
        PromptInstallLanguagePack(_settings.Ocr.Language);
        return;
    }

    string text;
    try
    {
        text = await Services.OcrService.ExtractTextAsync(image, langTag, _settings.Ocr.UpscaleSmallImages);
    }
    catch (Exception ex)
    {
        _trayIcon.ShowBalloonTip(3000, "신캡쳐 OCR 실패", ex.Message, System.Windows.Forms.ToolTipIcon.Error);
        return;
    }

    if (string.IsNullOrWhiteSpace(text))
    {
        _trayIcon.ShowBalloonTip(3000, "신캡쳐", "텍스트를 찾지 못했습니다", System.Windows.Forms.ToolTipIcon.Info);
        return;
    }

    if (!_settings.Ai.Enabled)
    {
        _trayIcon.ShowBalloonTip(4000, "신캡쳐 — AI 기능 비활성", "설정 > AI 탭에서 키를 입력하고 활성화하세요", System.Windows.Forms.ToolTipIcon.Warning);
        OpenSettings();
        return;
    }

    var store = new Services.Ai.DpapiCredentialStore();
    if (!store.HasKey())
    {
        _trayIcon.ShowBalloonTip(4000, "신캡쳐 — AI 키 필요", "설정 > AI 탭에서 OpenAI 키를 입력하세요", System.Windows.Forms.ToolTipIcon.Warning);
        OpenSettings();
        return;
    }

    var openAi = Services.Ai.OpenAiClient.CreateDefault(_settings.Ai.TimeoutSeconds);
    var svc = new Services.Ai.TranslationService(store, openAi);

    Services.Ai.TranslationResult tr;
    try
    {
        tr = await svc.TranslateAsync(text, _settings.Ai.TargetLanguage, _settings.Ai.Model);
    }
    catch (Services.Ai.OpenAiException ex)
    {
        var msg = ex.Kind switch
        {
            Services.Ai.OpenAiErrorKind.InvalidKey => "키가 유효하지 않습니다",
            Services.Ai.OpenAiErrorKind.RateLimited => "OpenAI 사용 한도 초과",
            Services.Ai.OpenAiErrorKind.ModelNotFound => "모델명 오류, 설정 확인",
            Services.Ai.OpenAiErrorKind.Network => "네트워크 연결 확인",
            Services.Ai.OpenAiErrorKind.Timeout => "응답 지연, 다시 시도",
            Services.Ai.OpenAiErrorKind.ServerError => "OpenAI 일시 장애",
            _ => "예상치 못한 응답"
        };
        _trayIcon.ShowBalloonTip(3500, "신캡쳐 — 번역 실패", msg, System.Windows.Forms.ToolTipIcon.Error);
        return;
    }

    switch (tr.Outcome)
    {
        case Services.Ai.TranslationOutcome.SkippedSameLanguage:
            System.Windows.Clipboard.SetText(text);
            _trayIcon.ShowBalloonTip(3000, "신캡쳐", $"이미 {_settings.Ai.TargetLanguage}입니다 (원문 복사됨)", System.Windows.Forms.ToolTipIcon.Info);
            break;
        case Services.Ai.TranslationOutcome.Success:
            System.Windows.Clipboard.SetText(tr.TranslatedText);
            var preview = tr.TranslatedText.Length > 40 ? tr.TranslatedText[..40] + "…" : tr.TranslatedText;
            _trayIcon.ShowBalloonTip(3500, "신캡쳐 — 번역 복사됨", preview, System.Windows.Forms.ToolTipIcon.Info);
            break;
        default:
            // SkippedEmpty/NoKey 등은 위에서 이미 처리했지만 안전망
            break;
    }
}
```

- [ ] **Step 5: 빌드 + 실행 가능 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe build src/ShinCapture/ShinCapture.csproj`
Expected: PASS, 경고 없음

- [ ] **Step 6: 커밋**

```bash
git add src/ShinCapture/Models/CaptureMode.cs src/ShinCapture/Views/MainWindow.xaml.cs
git commit -m "feat: add Ctrl+Shift+L hotkey for OCR-translate flow"
```

---

## Task 11: SettingsWindow AI 탭 신설

**Files:**
- Modify: `src/ShinCapture/Views/SettingsWindow.xaml`
- Modify: `src/ShinCapture/Views/SettingsWindow.xaml.cs`

- [ ] **Step 1: XAML에 AI 탭 추가**

`src/ShinCapture/Views/SettingsWindow.xaml`의 `<TabControl>` 안에 마지막 `<TabItem>` 다음(닫는 `</TabControl>` 직전)에 추가:

```xml
<TabItem Header="AI">
    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="20">
        <StackPanel>
            <CheckBox x:Name="AiEnabledCheckBox" Content="AI 기능 활성화" Margin="0,0,0,16" FontSize="14"/>

            <TextBlock Text="제공자: OpenAI" FontWeight="Bold" Margin="0,0,0,8"/>

            <Grid Margin="0,0,0,8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="80"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBlock Text="API 키:" VerticalAlignment="Center"/>
                <PasswordBox x:Name="AiKeyBox" Grid.Column="1" Margin="4,0"/>
                <Button x:Name="AiKeyShowBtn" Grid.Column="2" Content="👁" Width="32" Margin="2,0" Click="OnAiKeyShowClick"/>
                <Button x:Name="AiKeyValidateBtn" Grid.Column="3" Content="검증" Width="60" Margin="2,0" Click="OnAiKeyValidateClick"/>
                <Button x:Name="AiKeyDeleteBtn" Grid.Column="4" Content="삭제" Width="60" Margin="2,0" Click="OnAiKeyDeleteClick"/>
            </Grid>
            <TextBlock x:Name="AiKeyStatusText" Margin="84,0,0,8" Foreground="#666"/>
            <TextBlock Margin="84,0,0,16">
                <Hyperlink NavigateUri="https://platform.openai.com/api-keys" RequestNavigate="OnHyperlinkRequestNavigate">OpenAI 키 발급받기 →</Hyperlink>
            </TextBlock>

            <Grid Margin="0,0,0,8">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="80"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <TextBlock Text="모델:" VerticalAlignment="Center"/>
                <ComboBox x:Name="AiModelBox" Grid.Column="1" IsEditable="True" Margin="4,0">
                    <ComboBoxItem>gpt-4o-mini</ComboBoxItem>
                    <ComboBoxItem>gpt-4o</ComboBoxItem>
                    <ComboBoxItem>gpt-4.1</ComboBoxItem>
                </ComboBox>
            </Grid>
            <TextBlock Text="예상 비용: 1회 ≈ $0.0001 (1만 회 ≈ $1)" Margin="84,0,0,16" FontSize="11" Foreground="#888"/>

            <TextBlock Text="번역" FontWeight="Bold" Margin="0,8,0,8"/>
            <Grid Margin="0,0,0,16">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="80"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <TextBlock Text="대상 언어:" VerticalAlignment="Center"/>
                <ComboBox x:Name="AiTargetLangBox" Grid.Column="1" Margin="4,0">
                    <ComboBoxItem Tag="ko">한국어</ComboBoxItem>
                    <ComboBoxItem Tag="en">영어</ComboBoxItem>
                    <ComboBoxItem Tag="ja">일본어</ComboBoxItem>
                    <ComboBoxItem Tag="zh">중국어 (간체)</ComboBoxItem>
                </ComboBox>
            </Grid>

            <Border BorderBrush="#DDD" BorderThickness="1" Padding="12" Background="#FAFAFA">
                <StackPanel>
                    <TextBlock Text="보안 안내" FontWeight="Bold" Margin="0,0,0,4"/>
                    <TextBlock TextWrapping="Wrap" FontSize="12" Foreground="#444">
                        키는 이 PC의 Windows 사용자 계정에만 DPAPI로 암호화 저장됩니다.<LineBreak/>
                        파일을 다른 PC로 복사해도 사용할 수 없습니다.<LineBreak/>
                        ⚠ 같은 사용자 권한으로 실행되는 멀웨어가 있다면 어떤 OS 암호화도 우회될 수 있습니다.
                    </TextBlock>
                </StackPanel>
            </Border>
        </StackPanel>
    </ScrollViewer>
</TabItem>
```

- [ ] **Step 2: SettingsWindow.xaml.cs 보강**

기존 구조: 생성자가 `LoadSettings()` 호출, 저장 시 `OnSave` → `ApplyToSettings()` → `_settingsManager.Save(_settings)`.

`src/ShinCapture/Views/SettingsWindow.xaml.cs` 상단 using 영역에 추가:

```csharp
using ShinCapture.Services.Ai;
```

클래스 필드 영역(`_fixedSizes` 다음 줄)에 추가:

```csharp
private readonly DpapiCredentialStore _aiStore = new();
```

`LoadSettings()` 메서드 끝(현재 지정사이즈 블록 다음)에 AI 탭 로드 추가:

```csharp
        // AI
        AiEnabledCheckBox.IsChecked = _settings.Ai.Enabled;
        AiModelBox.Text = _settings.Ai.Model;
        foreach (System.Windows.Controls.ComboBoxItem item in AiTargetLangBox.Items)
        {
            if ((string)item.Tag == _settings.Ai.TargetLanguage)
            {
                AiTargetLangBox.SelectedItem = item;
                break;
            }
        }
        if (AiTargetLangBox.SelectedItem == null && AiTargetLangBox.Items.Count > 0)
            AiTargetLangBox.SelectedIndex = 0;
        UpdateAiKeyStatus();
```

`ApplyToSettings()` 메서드 끝(지정사이즈 블록 앞)에 추가:

```csharp
        // AI
        _settings.Ai.Enabled = AiEnabledCheckBox.IsChecked == true;
        _settings.Ai.Model = string.IsNullOrWhiteSpace(AiModelBox.Text) ? "gpt-4o-mini" : AiModelBox.Text.Trim();
        if (AiTargetLangBox.SelectedItem is System.Windows.Controls.ComboBoxItem aiItem && aiItem.Tag is string aiTag)
            _settings.Ai.TargetLanguage = aiTag;
```

클래스 끝(마지막 메서드 뒤)에 핸들러 추가:

```csharp
    private void UpdateAiKeyStatus()
    {
        if (_aiStore.HasKey())
        {
            AiKeyStatusText.Text = "✓ 저장된 키 있음 (검증 버튼으로 확인)";
            AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Green;
        }
        else
        {
            AiKeyStatusText.Text = "키 미설정";
            AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Gray;
        }
    }

    private void OnAiKeyShowClick(object sender, RoutedEventArgs e)
    {
        // PasswordBox 평문 토글: ToolTip으로 3초 노출
        AiKeyBox.ToolTip = AiKeyBox.Password;
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        t.Tick += (_, _) => { AiKeyBox.ToolTip = null; t.Stop(); };
        t.Start();
    }

    private async void OnAiKeyValidateClick(object sender, RoutedEventArgs e)
    {
        var key = AiKeyBox.Password;
        AiKeyStatusText.Text = "검증 중…";
        AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Gray;

        AiKeyHandle? handle;
        bool persistFromBox;
        if (string.IsNullOrWhiteSpace(key))
        {
            handle = _aiStore.AcquireKey();
            persistFromBox = false;
            if (handle == null)
            {
                AiKeyStatusText.Text = "검증할 키가 없습니다";
                AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }
        }
        else
        {
            handle = new AiKeyHandle(key);
            persistFromBox = true;
        }

        try
        {
            var client = OpenAiClient.CreateDefault(timeoutSeconds: 10);
            var ok = await client.ValidateKeyAsync(handle);
            if (ok)
            {
                if (persistFromBox)
                {
                    _aiStore.SaveKey(AiKeyBox.Password);
                    AiKeyBox.Password = ""; // 평문 흔적 제거
                }
                AiKeyStatusText.Text = $"✓ 키 유효 (검증: {DateTime.Now:HH:mm:ss})";
                AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Green;
                UpdateAiKeyStatus();
            }
            else
            {
                AiKeyStatusText.Text = "✗ 키 검증 실패";
                AiKeyStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
        finally
        {
            handle.Dispose();
        }
    }

    private void OnAiKeyDeleteClick(object sender, RoutedEventArgs e)
    {
        var r = MessageBox.Show("저장된 OpenAI 키를 삭제할까요?", "신캡쳐",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        _aiStore.DeleteKey();
        AiKeyBox.Password = "";
        AiEnabledCheckBox.IsChecked = false;
        UpdateAiKeyStatus();
    }

    private void OnHyperlinkRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }
```

- [ ] **Step 3: 빌드 + 설정창 수동 확인 (다음 task에서 일괄 검증)**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe build src/ShinCapture/ShinCapture.csproj`
Expected: PASS

- [ ] **Step 4: 커밋**

```bash
git add src/ShinCapture/Views/SettingsWindow.xaml src/ShinCapture/Views/SettingsWindow.xaml.cs
git commit -m "feat: add AI settings tab with key validation"
```

---

## Task 12: 단축키 탭에 TranslateCapture 행 추가

**Files:**
- Modify: `src/ShinCapture/Views/SettingsWindow.xaml`

기존 단축키 탭은 12개 RowDefinition(0~11)을 쓴다 — Row 0~7은 단축키, Row 8은 Separator, Row 9는 PrintScreen 토글, Row 10은 OCR 언어, Row 11은 OCR 전처리 체크박스. 새 행을 Row 8 자리에 끼워 넣고 나머지를 +1 시프트한다.

- [ ] **Step 1: RowDefinitions 13개로 확장**

`src/ShinCapture/Views/SettingsWindow.xaml`의 단축키 탭에서 `<Grid.RowDefinitions>` 블록에 `<RowDefinition Height="Auto"/>` 한 줄 추가 (총 13개).

- [ ] **Step 2: TextCapture 행 다음에 TranslateCapture 행 삽입**

기존:

```xml
<TextBlock Grid.Row="7" Grid.Column="0" Text="텍스트 캡쳐" VerticalAlignment="Center" Margin="0,0,0,10"/>
<TextBox x:Name="TxtHkTextCapture" Grid.Row="7" Grid.Column="1" Padding="4,3" Margin="0,0,0,10"/>

<Separator Grid.Row="8" Grid.ColumnSpan="2" Margin="0,4,0,10"/>
```

다음으로 변경:

```xml
<TextBlock Grid.Row="7" Grid.Column="0" Text="텍스트 캡쳐" VerticalAlignment="Center" Margin="0,0,0,10"/>
<TextBox x:Name="TxtHkTextCapture" Grid.Row="7" Grid.Column="1" Padding="4,3" Margin="0,0,0,10"/>

<TextBlock Grid.Row="8" Grid.Column="0" Text="텍스트+번역" VerticalAlignment="Center" Margin="0,0,0,10"/>
<TextBox x:Name="TxtHkTranslateCapture" Grid.Row="8" Grid.Column="1" Padding="4,3" Margin="0,0,0,10"/>

<Separator Grid.Row="9" Grid.ColumnSpan="2" Margin="0,4,0,10"/>
```

이어서 기존 Row 9~11을 Row 10~12로 시프트:
- `ChkOverridePrintScreen`: `Grid.Row="9"` → `Grid.Row="10"`
- OCR 언어 StackPanel: `Grid.Row="10"` → `Grid.Row="11"`
- `ChkOcrUpscale`: `Grid.Row="11"` → `Grid.Row="12"`

- [ ] **Step 3: SettingsWindow.xaml.cs 바인딩 추가**

`LoadSettings()`의 `TxtHkTextCapture.Text = _settings.Hotkeys.TextCapture;` 다음 줄에 추가:

```csharp
TxtHkTranslateCapture.Text = _settings.Hotkeys.TranslateCapture;
```

`ApplyToSettings()`의 `_settings.Hotkeys.TextCapture = TxtHkTextCapture.Text;` 다음 줄에 추가:

```csharp
_settings.Hotkeys.TranslateCapture = TxtHkTranslateCapture.Text;
```

- [ ] **Step 4: 빌드 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe build src/ShinCapture/ShinCapture.csproj`
Expected: PASS

- [ ] **Step 5: 커밋**

```bash
git add src/ShinCapture/Views/SettingsWindow.xaml src/ShinCapture/Views/SettingsWindow.xaml.cs
git commit -m "feat: add TranslateCapture row to hotkeys settings tab"
```

---

## Task 13: 편집기 OCR 패널에 번역 버튼 + 결과 영역

**Files:**
- Modify: `src/ShinCapture/Views/EditorWindow.xaml`
- Modify: `src/ShinCapture/Views/EditorWindow.xaml.cs`

- [ ] **Step 1: XAML 수정 — OCR 패널에 번역 버튼/영역 추가**

기존 OCR 패널 구조(`OcrPanel`, `OcrTextBox`, `OcrPanelTitle`, OCR 닫기/복사 버튼) 가까이에 추가:

- "🌐 번역" 버튼 (`x:Name="OcrTranslateBtn"`, `Click="OnOcrTranslateClick"`)
- 대상 언어 ComboBox (`x:Name="OcrTranslateLangBox"`, 4개 언어, Tag로 ko/en/ja/zh)
- 번역 결과 영역 (`x:Name="OcrTranslatedPanel"`, 기본 `Visibility="Collapsed"`):
  - 헤더 TextBlock "번역" + 길이 표시
  - 멀티라인 TextBox `x:Name="OcrTranslatedBox" IsReadOnly="True"`
  - "번역문 복사" 버튼 (`Click="OnOcrTranslatedCopyClick"`)

기존 XAML 구조에 맞춰 들여쓰기/스타일 일관성 유지.

- [ ] **Step 2: code-behind 핸들러 추가**

`src/ShinCapture/Views/EditorWindow.xaml.cs` 안에 추가:

```csharp
private async void OnOcrTranslateClick(object sender, RoutedEventArgs e)
{
    var text = OcrTextBox.Text;
    if (string.IsNullOrWhiteSpace(text))
    {
        SetStatus("번역: 추출된 텍스트가 없습니다");
        return;
    }

    var settings = _settingsManager?.Load() ?? _settings;
    if (!settings.Ai.Enabled)
    {
        SetStatus("번역: 설정 > AI 탭에서 활성화 필요");
        return;
    }

    var store = new ShinCapture.Services.Ai.DpapiCredentialStore();
    if (!store.HasKey())
    {
        SetStatus("번역: AI 키가 필요합니다 (설정 > AI)");
        return;
    }

    string targetLang = settings.Ai.TargetLanguage;
    if (OcrTranslateLangBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
        targetLang = tag;

    OcrTranslatedPanel.Visibility = Visibility.Visible;
    OcrTranslatedBox.Text = "번역 중…";
    SetStatus("번역 실행 중…");

    var openAi = ShinCapture.Services.Ai.OpenAiClient.CreateDefault(settings.Ai.TimeoutSeconds);
    var svc = new ShinCapture.Services.Ai.TranslationService(store, openAi);

    try
    {
        var r = await svc.TranslateAsync(text, targetLang, settings.Ai.Model);
        switch (r.Outcome)
        {
            case ShinCapture.Services.Ai.TranslationOutcome.Success:
                OcrTranslatedBox.Text = r.TranslatedText;
                SetStatus($"번역 완료 ({r.TranslatedText.Length}자, {targetLang})");
                break;
            case ShinCapture.Services.Ai.TranslationOutcome.SkippedSameLanguage:
                OcrTranslatedBox.Text = r.OriginalText;
                SetStatus($"이미 {targetLang}입니다");
                break;
            default:
                OcrTranslatedBox.Text = "(번역 결과 없음)";
                SetStatus("번역 결과 없음");
                break;
        }
    }
    catch (ShinCapture.Services.Ai.OpenAiException ex)
    {
        OcrTranslatedBox.Text = $"번역 실패: {ex.Message}";
        SetStatus($"번역 실패 — {ex.Kind}");
    }
}

private void OnOcrTranslatedCopyClick(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrEmpty(OcrTranslatedBox.Text)) return;
    System.Windows.Clipboard.SetText(OcrTranslatedBox.Text);
    SetStatus($"번역문 복사됨 ({OcrTranslatedBox.Text.Length}자)");
}
```

또한 새 캡쳐 시 `OcrPanel`을 숨기는 기존 로직(라인 130 근방)과 같은 위치에 다음 추가:

```csharp
if (OcrTranslatedPanel != null)
{
    OcrTranslatedPanel.Visibility = Visibility.Collapsed;
    OcrTranslatedBox.Text = "";
}
```

- [ ] **Step 3: 빌드 확인**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe build src/ShinCapture/ShinCapture.csproj`
Expected: PASS

- [ ] **Step 4: 커밋**

```bash
git add src/ShinCapture/Views/EditorWindow.xaml src/ShinCapture/Views/EditorWindow.xaml.cs
git commit -m "feat: add translate button and result panel to editor OCR"
```

---

## Task 14: 통합 회귀 + 수동 스모크 테스트

**Files:** (없음 — 검증)

- [ ] **Step 1: 모든 단위 테스트 실행**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe test tests/ShinCapture.Tests/ShinCapture.Tests.csproj`
Expected: 전체 통과 (기존 29개 + 신규 ~25개)

- [ ] **Step 2: Release 빌드**

Run: `C:\Users\popol\dotnet-sdk2\dotnet.exe publish src/ShinCapture/ShinCapture.csproj -c Release -r win-x64 --self-contained -o publish_preview/v1.2.0-preview`
Expected: PASS, 단일 EXE 생성

- [ ] **Step 3: 수동 스모크 (최소 시나리오)**

이 단계는 사람이 직접 확인. 체크리스트:
- [ ] 앱 실행 → 트레이 아이콘 표시
- [ ] 설정 > AI 탭 → 키 입력 → 검증 ✓
- [ ] 키 삭제 후 다시 검증 → 실패 메시지
- [ ] 키 다시 저장 → AI 활성화 ON
- [ ] `Ctrl+Shift+T` (기존 OCR) → 텍스트 복사 토스트 (변화 없는지)
- [ ] `Ctrl+Shift+L` (신규) → OCR + 번역 → 클립보드에 번역문 + 토스트
- [ ] 같은 언어 캡쳐(한국어→ko 설정) → "이미 한국어입니다" 토스트, 클립보드는 원문
- [ ] 편집기 OCR 패널 → "🌐 번역" 버튼 → 번역 영역 펼침 + 결과 표시
- [ ] 편집기에서 "번역문 복사" 작동
- [ ] 인터넷 차단(또는 잘못된 모델) → 토스트로 적절한 에러
- [ ] 앱 재시작 후 키 유지 (DPAPI 라운드트립)
- [ ] 다른 Windows 사용자로 로그인 → `apikey.dat` 못 읽음(수동, 선택)

- [ ] **Step 4: csproj 버전 bump**

`src/ShinCapture/ShinCapture.csproj`의 `<Version>1.1.0</Version>` → `<Version>1.2.0</Version>`

- [ ] **Step 5: 최종 커밋 (push 보류)**

```bash
git add src/ShinCapture/ShinCapture.csproj
git commit -m "chore: bump version to 1.2.0"
git log --oneline -20
```

**Push 안 함** — 사용자가 v1.2.0 수동 테스트 통과 확인 후 결정.

---

## 완료 기준

- 신규 단위 테스트 모두 통과 + 기존 29개 회귀 없음
- Release 빌드 단일 EXE 생성 성공
- Task 14 Step 3 스모크 체크리스트 모두 PASS
- 키 평문이 디스크/로그/클립보드 어디에도 잔재 없음 (Task 5 테스트로 검증)
- AI 비활성/키 미설정 사용자는 v1.1.0과 동일하게 동작 (호환성)
