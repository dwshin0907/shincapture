using ShinCapture.Models;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class TrayMenuCatalogTests
{
    [Fact]
    public void CreatesCaptureActionsInTrayOrderWithExpectedWidths()
    {
        IReadOnlyList<TrayCaptureAction> actions =
            TrayMenuCatalog.CreateCaptureActions(new HotkeySettings());
        CaptureMode[] expectedModes =
        [
            CaptureMode.Region,
            CaptureMode.Window,
            CaptureMode.Fullscreen,
            CaptureMode.Scroll,
            CaptureMode.SmartCut,
            CaptureMode.FixedSize,
            CaptureMode.Freeform,
            CaptureMode.Element,
            CaptureMode.Text,
            CaptureMode.Translate
        ];

        Assert.Equal(expectedModes, actions.Select(action => action.Mode));
        Assert.Equal(expectedModes.Length, actions.Count);
        Assert.All(expectedModes, mode =>
            Assert.Single(actions, action => action.Mode == mode));
        Assert.All(actions, action =>
            Assert.False(string.IsNullOrWhiteSpace(action.IconKey)));
        Assert.True(actions.Single(action => action.Mode == CaptureMode.Region).IsWide);
        Assert.True(actions.Single(action => action.Mode == CaptureMode.Translate).IsWide);
        Assert.All(
            actions.Where(action => action.Mode is not CaptureMode.Region and not CaptureMode.Translate),
            action => Assert.False(action.IsWide));
    }

    [Fact]
    public void ReflectsCustomPrimaryAlternateAndTranslateShortcuts()
    {
        HotkeySettings settings = new()
        {
            RegionCapture = "Ctrl+Alt+1",
            RegionCaptureAlt = "Ctrl+Alt+R",
            TranslateCapture = "Ctrl+Alt+2"
        };

        IReadOnlyList<TrayCaptureAction> actions = TrayMenuCatalog.CreateCaptureActions(settings);

        Assert.Equal(
            "Ctrl+Alt+1  ·  Ctrl+Alt+R",
            actions.Single(action => action.Mode == CaptureMode.Region).Shortcut);
        Assert.Equal(
            "Ctrl+Alt+2",
            actions.Single(action => action.Mode == CaptureMode.Translate).Shortcut);
    }

    [Fact]
    public void UsesOnlyPrimaryRegionShortcutWhenAlternateIsEmpty()
    {
        HotkeySettings settings = new()
        {
            RegionCapture = "Ctrl+Alt+1",
            RegionCaptureAlt = string.Empty
        };

        IReadOnlyList<TrayCaptureAction> actions = TrayMenuCatalog.CreateCaptureActions(settings);

        Assert.Equal(
            "Ctrl+Alt+1",
            actions.Single(action => action.Mode == CaptureMode.Region).Shortcut);
    }

    [Fact]
    public void CreatesExactDefaultActionTable()
    {
        (CaptureMode Mode, string Label, string Shortcut, string IconKey, bool IsWide)[] expected =
        [
            (CaptureMode.Region, "영역 캡처", "PrintScreen  ·  Ctrl+Shift+C", "region", true),
            (CaptureMode.Window, "창 캡처", "Ctrl+Shift+W", "window", false),
            (CaptureMode.Fullscreen, "전체 화면", "Ctrl+Shift+A", "fullscreen", false),
            (CaptureMode.Scroll, "스크롤", "Ctrl+Shift+S", "scroll", false),
            (CaptureMode.SmartCut, "스마트 컷", "Ctrl+Shift+G", "spark", false),
            (CaptureMode.FixedSize, "지정 크기", "Ctrl+Shift+Z", "fixed", false),
            (CaptureMode.Freeform, "자유형", "Ctrl+Shift+F", "freeform", false),
            (CaptureMode.Element, "단위 영역", "Ctrl+Shift+D", "element", false),
            (CaptureMode.Text, "텍스트", "Ctrl+Shift+T", "text", false),
            (CaptureMode.Translate, "텍스트 + 번역", "Ctrl+Shift+L", "translate", true)
        ];

        IReadOnlyList<TrayCaptureAction> actions =
            TrayMenuCatalog.CreateCaptureActions(new HotkeySettings());

        Assert.Equal(
            expected,
            actions.Select(action =>
                (action.Mode, action.Label, action.Shortcut, action.IconKey, action.IsWide)));
    }

    [Fact]
    public void NormalizesEveryConfiguredShortcut()
    {
        HotkeySettings padded = new()
        {
            RegionCapture = "  Ctrl+1  ",
            RegionCaptureAlt = "  Alt+R  ",
            WindowCapture = "  Ctrl+W  ",
            FullscreenCapture = "  Ctrl+A  ",
            ScrollCapture = "  Ctrl+S  ",
            SmartCutCapture = "  Ctrl+G  ",
            FixedSizeCapture = "  Ctrl+Z  ",
            FreeformCapture = "  Ctrl+F  ",
            ElementCapture = "  Ctrl+D  ",
            TextCapture = "  Ctrl+T  ",
            TranslateCapture = "  Ctrl+L  "
        };
        string[] expected =
        [
            "Ctrl+1  ·  Alt+R",
            "Ctrl+W",
            "Ctrl+A",
            "Ctrl+S",
            "Ctrl+G",
            "Ctrl+Z",
            "Ctrl+F",
            "Ctrl+D",
            "Ctrl+T",
            "Ctrl+L"
        ];

        Assert.Equal(
            expected,
            TrayMenuCatalog.CreateCaptureActions(padded).Select(action => action.Shortcut));

        HotkeySettings blank = new()
        {
            RegionCapture = " \t ",
            RegionCaptureAlt = " \t ",
            WindowCapture = " \t ",
            FullscreenCapture = " \t ",
            ScrollCapture = " \t ",
            SmartCutCapture = " \t ",
            FixedSizeCapture = " \t ",
            FreeformCapture = " \t ",
            ElementCapture = " \t ",
            TextCapture = " \t ",
            TranslateCapture = " \t "
        };

        Assert.All(
            TrayMenuCatalog.CreateCaptureActions(blank),
            action => Assert.Equal(string.Empty, action.Shortcut));
    }

    [Theory]
    [InlineData("  ", " \t ", "")]
    [InlineData("  Ctrl+1  ", " ", "Ctrl+1")]
    [InlineData(" ", " Alt+R ", "Alt+R")]
    [InlineData(" Ctrl+1 ", " Alt+R ", "Ctrl+1  ·  Alt+R")]
    public void NormalizesRegionShortcutCombinations(
        string primary,
        string alternate,
        string expected)
    {
        HotkeySettings settings = new()
        {
            RegionCapture = primary,
            RegionCaptureAlt = alternate
        };

        string shortcut = TrayMenuCatalog.CreateCaptureActions(settings)
            .Single(action => action.Mode == CaptureMode.Region)
            .Shortcut;

        Assert.Equal(expected, shortcut);
    }

    [Fact]
    public void RejectsNullHotkeySettings()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TrayMenuCatalog.CreateCaptureActions(null!));
    }
}
