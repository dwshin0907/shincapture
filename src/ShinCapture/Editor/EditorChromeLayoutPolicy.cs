namespace ShinCapture.Editor;

public enum EditorChromeMode
{
    Narrow,
    Compact,
    Comfortable
}

public enum EditorToolVisibility
{
    Essential,
    Common,
    All
}

public readonly record struct EditorChromeLayout(
    EditorChromeMode Mode,
    EditorToolVisibility DirectToolVisibility,
    bool ShowHistoryByDefault,
    double HistoryWidth);

public static class EditorChromeLayoutPolicy
{
    public const double ComfortableWidth = 1400;
    public const double CompactWidth = 850;

    public static EditorChromeLayout Resolve(double width) => width switch
    {
        >= ComfortableWidth => new(EditorChromeMode.Comfortable, EditorToolVisibility.All, true, 180),
        >= CompactWidth => new(EditorChromeMode.Compact, EditorToolVisibility.Common, true, 180),
        _ => new(EditorChromeMode.Narrow, EditorToolVisibility.Essential, false, 0)
    };
}
