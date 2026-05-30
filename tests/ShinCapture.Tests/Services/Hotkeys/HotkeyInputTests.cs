using System.Windows.Input;
using ShinCapture.Services.Hotkeys;

namespace ShinCapture.Tests.Services.Hotkeys;

public class HotkeyInputTests
{
    [Fact]
    public void Format_OrdersModifiersCtrlAltShift_ThenKey()
    {
        var s = HotkeyInput.Format(ModifierKeys.Shift | ModifierKeys.Control, Key.G);
        Assert.Equal("Ctrl+Shift+G", s);
    }

    [Fact]
    public void Format_DigitKey_UsesBareDigit()
    {
        Assert.Equal("Ctrl+1", HotkeyInput.Format(ModifierKeys.Control, Key.D1));
    }

    [Fact]
    public void Format_PrintScreen_NoModifier()
    {
        Assert.Equal("PrintScreen", HotkeyInput.Format(ModifierKeys.None, Key.PrintScreen));
    }

    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.System)]
    public void IsModifierKey_True(Key k) => Assert.True(HotkeyInput.IsModifierKey(k));

    [Fact]
    public void IsValid_BareLetter_Rejected()
    {
        Assert.False(HotkeyInput.IsValidGlobalHotkey(ModifierKeys.None, Key.G, out _));
    }

    [Fact]
    public void IsValid_PrintScreenAlone_Allowed()
    {
        Assert.True(HotkeyInput.IsValidGlobalHotkey(ModifierKeys.None, Key.PrintScreen, out _));
    }

    [Fact]
    public void IsValid_CtrlLetter_Allowed()
    {
        Assert.True(HotkeyInput.IsValidGlobalHotkey(ModifierKeys.Control, Key.G, out _));
    }
}
