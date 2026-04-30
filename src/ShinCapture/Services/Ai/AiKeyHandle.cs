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
