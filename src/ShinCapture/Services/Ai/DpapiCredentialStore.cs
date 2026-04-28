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
