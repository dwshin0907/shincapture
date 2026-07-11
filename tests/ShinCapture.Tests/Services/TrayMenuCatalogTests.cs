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
}
