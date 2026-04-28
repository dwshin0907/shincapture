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
