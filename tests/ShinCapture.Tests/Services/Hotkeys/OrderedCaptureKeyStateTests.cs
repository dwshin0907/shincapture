using ShinCapture.Models;
using ShinCapture.Services.Hotkeys;
using System.Text.Json;

namespace ShinCapture.Tests.Services.Hotkeys;

public class OrderedCaptureKeyStateTests
{
    [Fact]
    public void ExistingSettingsAndFreshInstallKeepOrderIndependentCapture()
    {
        Assert.False(new HotkeySettings().RegionCaptureShiftFirst);
        Assert.False(JsonSerializer.Deserialize<HotkeySettings>("{}")!.RegionCaptureShiftFirst);
        var saved = JsonSerializer.Serialize(new HotkeySettings { RegionCaptureShiftFirst = true });
        Assert.True(JsonSerializer.Deserialize<HotkeySettings>(saved)!.RegionCaptureShiftFirst);
    }

    [Theory]
    [InlineData(0xA0, 0xA2)]
    [InlineData(0xA0, 0xA3)]
    [InlineData(0xA1, 0xA2)]
    [InlineData(0xA1, 0xA3)]
    public void ShiftFirstCapturesAndCtrlFirstPassesThroughForBothSides(int shift, int ctrl)
    {
        var state = new OrderedCaptureKeyState();
        Assert.False(state.Process(ctrl, true, true, out _));
        state.Process(shift, true, true, out _);
        Assert.False(state.Process(0x43, true, true, out bool trigger));
        Assert.False(trigger);
        Assert.False(state.Process(0x43, false, true, out _));
        state.Process(ctrl, false, true, out _);
        state.Process(ctrl, true, true, out _);
        Assert.True(state.Process(0x43, true, true, out trigger));
        Assert.True(trigger);
        Assert.True(state.Process(0x43, true, true, out trigger));
        Assert.False(trigger); // no repeat
        state.Process(ctrl, false, true, out _);
        state.Process(shift, false, true, out _);
        Assert.True(state.Process(0x43, false, true, out _));
        Assert.False(state.Process(0x43, true, true, out _));
    }

    [Theory]
    [InlineData(0xA4)]
    [InlineData(0xA5)]
    [InlineData(0x5B)]
    [InlineData(0x5C)]
    public void ExtraModifiersPassThrough(int extra)
    {
        var state = Ready();
        state.Process(extra, true, true, out _);
        Assert.False(state.Process(0x43, true, true, out _));
    }

    [Fact]
    public void SuspensionDoesNotCaptureButFinishesPreviouslySwallowedPress()
    {
        var state = Ready();
        Assert.False(state.Process(0x43, true, false, out _));
        Assert.False(state.Process(0x43, true, true, out _));
        state.Process(0x43, false, true, out _);
        Assert.True(state.Process(0x43, true, true, out _));
        Assert.True(state.Process(0x43, false, false, out _));
    }

    [Fact]
    public void UnknownInitialOrderAndMissingModifiersDoNotCapture()
    {
        var state = new OrderedCaptureKeyState();
        state.SeedHeldModifier(0xA0);
        state.Process(0xA2, true, true, out _);
        Assert.False(state.Process(0x43, true, true, out _));
        state.Process(0x43, false, true, out _);
        state.ReleaseModifier(0xA0);
        Assert.False(state.Process(0x43, true, true, out _));
    }

    [Fact]
    public void ModifierAutoRepeatDoesNotChangeOrder()
    {
        var state = Ready();
        state.Process(0xA0, true, true, out _);
        Assert.True(state.Process(0x43, true, true, out bool trigger));
        Assert.True(trigger);
    }

    private static OrderedCaptureKeyState Ready()
    {
        var state = new OrderedCaptureKeyState();
        state.Process(0xA0, true, true, out _);
        state.Process(0xA2, true, true, out _);
        return state;
    }
}
