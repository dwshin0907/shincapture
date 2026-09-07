using System.Collections.Generic;
using System.Linq;

namespace ShinCapture.Services.Hotkeys;

/// <summary>Tracks only shortcut keys. A captured C press owns its repeats and release.</summary>
internal sealed class OrderedCaptureKeyState
{
    internal static readonly int[] ModifierKeys = { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C };
    private readonly Dictionary<int, long> _down = new();
    private long _sequence;
    private bool _cDown;
    private bool _captured;

    public void SeedHeldModifier(int key) => _down[key] = 0; // Order before observation is unknown.
    public void ReleaseModifier(int key) => _down.Remove(key);

    public bool Process(int key, bool down, bool enabled, out bool trigger)
    {
        trigger = false;
        if (System.Array.IndexOf(ModifierKeys, key) >= 0)
        {
            if (!down) _down.Remove(key);
            else if (!_down.ContainsKey(key)) _down[key] = ++_sequence;
            return false;
        }
        if (key != 0x43) return false;
        if (!down)
        {
            bool swallow = _captured;
            _cDown = _captured = false;
            return swallow;
        }
        if (_cDown) return _captured;
        _cDown = true;
        long shift = First(0xA0, 0xA1);
        long ctrl = First(0xA2, 0xA3);
        _captured = enabled && shift > 0 && ctrl > shift
            && !_down.Keys.Any(k => k is 0xA4 or 0xA5 or 0x5B or 0x5C);
        trigger = _captured;
        return _captured;
    }

    private long First(int left, int right)
    {
        long a = _down.TryGetValue(left, out var l) ? l : long.MaxValue;
        long b = _down.TryGetValue(right, out var r) ? r : long.MaxValue;
        long first = System.Math.Min(a, b);
        return first == long.MaxValue ? -1 : first;
    }
}
