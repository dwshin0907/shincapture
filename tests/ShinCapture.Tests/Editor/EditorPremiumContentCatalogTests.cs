using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class EditorPremiumContentCatalogTests
{
    [Fact]
    public void DefinesStableNaverPremiumContentMetadata()
    {
        Assert.Equal("AI 실전 활용법", EditorPremiumContentCatalog.Title);
        Assert.Equal("네이버 프리미엄콘텐츠에서 보기", EditorPremiumContentCatalog.Description);
        Assert.Equal("https", EditorPremiumContentCatalog.ChannelUri.Scheme);
        Assert.Equal("contents.premium.naver.com", EditorPremiumContentCatalog.ChannelUri.Host);
    }
}
