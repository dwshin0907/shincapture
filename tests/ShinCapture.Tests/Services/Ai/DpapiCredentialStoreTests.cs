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
