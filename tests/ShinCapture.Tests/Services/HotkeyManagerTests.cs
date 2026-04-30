using System.Windows.Input;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public class HotkeyManagerTests
{
    [Fact]
    public void Parse_CtrlShift1_MapsToD1()
    {
        HotkeyManager.ParseHotkeyString("Ctrl+Shift+1", out var mods, out var vk);
        Assert.NotEqual(0u, vk);
        Assert.Equal((uint)KeyInterop.VirtualKeyFromKey(Key.D1), vk);
    }

    [Fact]
    public void Parse_AllDigitsAreNonZero()
    {
        for (int i = 0; i <= 9; i++)
        {
            HotkeyManager.ParseHotkeyString($"Ctrl+{i}", out _, out var vk);
            Assert.True(vk != 0u, $"digit {i} should map to a non-zero vk");
        }
    }

    [Fact]
    public void Parse_AlphabetStillWorks()
    {
        HotkeyManager.ParseHotkeyString("Ctrl+Shift+A", out var mods, out var vk);
        Assert.Equal((uint)KeyInterop.VirtualKeyFromKey(Key.A), vk);
    }

    [Fact]
    public void Parse_PrintScreenStillWorks()
    {
        HotkeyManager.ParseHotkeyString("PrintScreen", out _, out var vk);
        Assert.Equal((uint)KeyInterop.VirtualKeyFromKey(Key.PrintScreen), vk);
    }

    [Fact]
    public void Parse_ModifiersAreCorrect()
    {
        HotkeyManager.ParseHotkeyString("Ctrl+Shift+Alt+1", out var mods, out _);
        // MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_ALT = 0x0001
        Assert.Equal(0x0007u, mods);
    }
}
