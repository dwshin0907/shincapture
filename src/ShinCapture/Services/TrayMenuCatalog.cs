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
            new(CaptureMode.Window, "창 캡처", NormalizeShortcut(hotkeys.WindowCapture), "window"),
            new(
                CaptureMode.Fullscreen,
                "전체 화면",
                NormalizeShortcut(hotkeys.FullscreenCapture),
                "fullscreen"),
            new(CaptureMode.Scroll, "스크롤", NormalizeShortcut(hotkeys.ScrollCapture), "scroll"),
            new(
                CaptureMode.SmartCut,
                "스마트 컷",
                NormalizeShortcut(hotkeys.SmartCutCapture),
                "spark"),
            new(
                CaptureMode.FixedSize,
                "지정 크기",
                NormalizeShortcut(hotkeys.FixedSizeCapture),
                "fixed"),
            new(
                CaptureMode.Freeform,
                "자유형",
                NormalizeShortcut(hotkeys.FreeformCapture),
                "freeform"),
            new(
                CaptureMode.Element,
                "단위 영역",
                NormalizeShortcut(hotkeys.ElementCapture),
                "element"),
            new(CaptureMode.Text, "텍스트", NormalizeShortcut(hotkeys.TextCapture), "text"),
            new(
                CaptureMode.Translate,
                "텍스트 + 번역",
                NormalizeShortcut(hotkeys.TranslateCapture),
                "translate",
                IsWide: true)
        ];
    }

    private static string JoinShortcuts(string? primary, string? alternate)
    {
        primary = NormalizeShortcut(primary);
        alternate = NormalizeShortcut(alternate);

        if (alternate.Length == 0)
            return primary;
        if (primary.Length == 0)
            return alternate;

        return $"{primary}  ·  {alternate}";
    }

    private static string NormalizeShortcut(string? shortcut) =>
        string.IsNullOrWhiteSpace(shortcut) ? string.Empty : shortcut.Trim();
}
