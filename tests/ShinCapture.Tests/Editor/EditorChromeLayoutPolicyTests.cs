using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class EditorChromeLayoutPolicyTests
{
    [Theory]
    [InlineData(1600, EditorChromeMode.Comfortable, true, true, 180)]
    [InlineData(1320, EditorChromeMode.Comfortable, true, true, 180)]
    [InlineData(1319, EditorChromeMode.Compact, false, true, 180)]
    [InlineData(1100, EditorChromeMode.Compact, false, true, 180)]
    [InlineData(850, EditorChromeMode.Compact, false, true, 180)]
    [InlineData(849, EditorChromeMode.Narrow, false, false, 0)]
    [InlineData(760, EditorChromeMode.Narrow, false, false, 0)]
    public void ResolvesStableEditorChrome(
        double width,
        EditorChromeMode mode,
        bool showLabels,
        bool showHistory,
        double historyWidth)
    {
        EditorChromeLayout layout = EditorChromeLayoutPolicy.Resolve(width);

        Assert.Equal(mode, layout.Mode);
        Assert.Equal(showLabels, layout.ShowToolLabels);
        Assert.Equal(showHistory, layout.ShowHistoryByDefault);
        Assert.Equal(historyWidth, layout.HistoryWidth);
    }

    [Fact]
    public void KeepsDocumentedBreakpointContract()
    {
        Assert.Equal(1320, EditorChromeLayoutPolicy.ComfortableWidth);
        Assert.Equal(850, EditorChromeLayoutPolicy.CompactWidth);
    }
}
