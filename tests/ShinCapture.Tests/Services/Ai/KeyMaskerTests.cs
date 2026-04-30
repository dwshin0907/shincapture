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
