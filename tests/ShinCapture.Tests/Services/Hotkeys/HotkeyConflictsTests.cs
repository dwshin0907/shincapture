using System.Collections.Generic;
using ShinCapture.Services.Hotkeys;

namespace ShinCapture.Tests.Services.Hotkeys;

public class HotkeyConflictsTests
{
    [Fact]
    public void Normalize_IgnoresModifierOrder()
    {
        Assert.Equal(HotkeyConflicts.Normalize("Ctrl+Shift+G"),
                     HotkeyConflicts.Normalize("Shift+Ctrl+G"));
    }

    [Fact]
    public void Normalize_EmptyOrInvalid_Null()
    {
        Assert.Null(HotkeyConflicts.Normalize(""));
        Assert.Null(HotkeyConflicts.Normalize("   "));
    }

    [Fact]
    public void FindInternalConflict_DetectsCollision_ExcludingSelf()
    {
        var bindings = new Dictionary<string, string>
        {
            ["영역지정"] = "Ctrl+Shift+C",
            ["전체화면"] = "Ctrl+Shift+A",
        };
        Assert.Equal("전체화면",
            HotkeyConflicts.FindInternalConflict("Shift+Ctrl+A", bindings, "영역지정"));
        Assert.Null(
            HotkeyConflicts.FindInternalConflict("Ctrl+Shift+A", bindings, "전체화면")); // 자기 자신
        Assert.Null(
            HotkeyConflicts.FindInternalConflict("Ctrl+Shift+X", bindings, "영역지정"));
    }

    [Fact]
    public void SuggestAlternative_ReturnsFirstAvailableVariant()
    {
        // Ctrl+Shift+W는 점유. Ctrl+Alt+W는 빈 것으로 가정.
        bool IsAvailable(string combo) => combo != "Ctrl+Shift+W";
        var alt = HotkeyConflicts.SuggestAlternative("Ctrl+Shift+W", IsAvailable);
        Assert.Equal("Ctrl+Alt+W", alt);
    }

    [Fact]
    public void SuggestAlternative_NoneAvailable_Null()
    {
        var alt = HotkeyConflicts.SuggestAlternative("Ctrl+Shift+W", _ => false);
        Assert.Null(alt);
    }
}
