using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class EditorChromeLayoutPolicyTests
{
    [Theory]
    [InlineData(1600, EditorChromeMode.Comfortable, EditorToolVisibility.All, true, 180)]
    [InlineData(1400, EditorChromeMode.Comfortable, EditorToolVisibility.All, true, 180)]
    [InlineData(1399, EditorChromeMode.Compact, EditorToolVisibility.Common, true, 180)]
    [InlineData(1100, EditorChromeMode.Compact, EditorToolVisibility.Common, true, 180)]
    [InlineData(850, EditorChromeMode.Compact, EditorToolVisibility.Common, true, 180)]
    [InlineData(849, EditorChromeMode.Narrow, EditorToolVisibility.Essential, false, 0)]
    [InlineData(700, EditorChromeMode.Narrow, EditorToolVisibility.Essential, false, 0)]
    public void ResolvesStableEditorChrome(
        double width,
        EditorChromeMode mode,
        EditorToolVisibility directToolVisibility,
        bool showHistory,
        double historyWidth)
    {
        EditorChromeLayout layout = EditorChromeLayoutPolicy.Resolve(width);

        Assert.Equal(mode, layout.Mode);
        Assert.Equal(directToolVisibility, layout.DirectToolVisibility);
        Assert.Equal(showHistory, layout.ShowHistoryByDefault);
        Assert.Equal(historyWidth, layout.HistoryWidth);
    }

    [Fact]
    public void KeepsDocumentedBreakpointContract()
    {
        Assert.Equal(1400, EditorChromeLayoutPolicy.ComfortableWidth);
        Assert.Equal(850, EditorChromeLayoutPolicy.CompactWidth);
    }
}
