namespace ShinCapture.Editor;

public enum EditorChromeMode
{
    Narrow,
    Compact,
    Comfortable
}

public readonly record struct EditorChromeLayout(
    EditorChromeMode Mode,
    bool ShowToolLabels,
    bool ShowHistoryByDefault,
    double HistoryWidth);

public static class EditorChromeLayoutPolicy
{
    public const double ComfortableWidth = 1500;
    public const double CompactWidth = 850;

    public static EditorChromeLayout Resolve(double width) => width switch
    {
        >= ComfortableWidth => new(EditorChromeMode.Comfortable, true, true, 180),
        >= CompactWidth => new(EditorChromeMode.Compact, false, true, 180),
        _ => new(EditorChromeMode.Narrow, false, false, 0)
    };
}
