using System;

namespace ShinCapture.Editor;

public static class EditorPremiumContentCatalog
{
    public const string Title = "AI 실전 활용법";
    public const string Description = "네이버 프리미엄콘텐츠에서 보기";

    public static Uri ChannelUri { get; } =
        new("https://contents.premium.naver.com/market/ai");
}
