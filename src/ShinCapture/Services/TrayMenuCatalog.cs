using System;
using System.Collections.Generic;
using ShinCapture.Models;

namespace ShinCapture.Services;

public static class TrayMenuCatalog
{
    public static IReadOnlyList<TrayCaptureAction> CreateCaptureActions(HotkeySettings hotkeys)
    {
        ArgumentNullException.ThrowIfNull(hotkeys);

        return
        [
            new(
                CaptureMode.Region,
                "영역 캡처",
                JoinShortcuts(hotkeys.RegionCapture, hotkeys.RegionCaptureAlt),
                "region",
                IsWide: true),
            new(CaptureMode.Window, "창 캡처", hotkeys.WindowCapture, "window"),
            new(CaptureMode.Fullscreen, "전체 화면", hotkeys.FullscreenCapture, "fullscreen"),
            new(CaptureMode.Scroll, "스크롤", hotkeys.ScrollCapture, "scroll"),
            new(CaptureMode.SmartCut, "스마트 컷", hotkeys.SmartCutCapture, "spark"),
            new(CaptureMode.FixedSize, "지정 크기", hotkeys.FixedSizeCapture, "fixed"),
            new(CaptureMode.Freeform, "자유형", hotkeys.FreeformCapture, "freeform"),
            new(CaptureMode.Element, "단위 영역", hotkeys.ElementCapture, "element"),
            new(CaptureMode.Text, "텍스트", hotkeys.TextCapture, "text"),
            new(
                CaptureMode.Translate,
                "텍스트 + 번역",
                hotkeys.TranslateCapture,
                "translate",
                IsWide: true)
        ];
    }

    private static string JoinShortcuts(string primary, string alternate)
    {
        if (string.IsNullOrWhiteSpace(alternate))
            return primary;
        if (string.IsNullOrWhiteSpace(primary))
            return alternate;

        return $"{primary}  ·  {alternate}";
    }
}
