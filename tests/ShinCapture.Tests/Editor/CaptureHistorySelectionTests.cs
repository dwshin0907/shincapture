using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public sealed class CaptureHistorySelectionTests
{
    private readonly string[] _items = ["newest", "middle", "older", "oldest"];

    [Fact]
    public void CheckingItemsKeepsOtherSelectionsAndReturnsDisplayOrder()
    {
        var selection = new CaptureHistorySelection<string>();
        selection.Toggle(_items[3]);
        selection.Toggle(_items[0]);
        selection.Toggle(_items[2]);
        Assert.Equal(new[] { "newest", "older", "oldest" }, selection.InDisplayOrder(_items));

        selection.Toggle(_items[2]);
        Assert.Equal(new[] { "newest", "oldest" }, selection.InDisplayOrder(_items));
    }

    [Fact]
    public void PlainSelectionReplacesThePreviousBatch()
    {
        var selection = new CaptureHistorySelection<string>();
        selection.SelectAll(_items);
        selection.SelectOnly(_items[1]);
        Assert.Equal(new[] { "middle" }, selection.InDisplayOrder(_items));
    }

    [Fact]
    public void ShiftSelectionCanGrowAndShrinkFromTheSameAnchor()
    {
        var selection = new CaptureHistorySelection<string>();
        selection.SelectOnly(_items[3]);
        selection.SelectRange(_items, _items[0]);
        Assert.Equal(_items, selection.InDisplayOrder(_items));
        selection.SelectRange(_items, _items[2]);
        Assert.Equal(new[] { "older", "oldest" }, selection.InDisplayOrder(_items));
    }

    [Fact]
    public void ControlShiftPreservesSelectionsOutsideTheRange()
    {
        var selection = new CaptureHistorySelection<string>();
        selection.Toggle(_items[0]);
        selection.Toggle(_items[2]);
        selection.SelectRange(_items, _items[3], additive: true);
        Assert.Equal(new[] { "newest", "older", "oldest" }, selection.InDisplayOrder(_items));
    }

    [Fact]
    public void TrimmingHistoryRemovesStaleSelectionsAndResetsTheRemovedAnchor()
    {
        var selection = new CaptureHistorySelection<string>();
        selection.Toggle(_items[0]);
        selection.Toggle(_items[3]);
        string[] retained = _items[..3];
        selection.Retain(retained);
        Assert.Equal(new[] { "newest" }, selection.InDisplayOrder(retained));
        selection.SelectRange(retained, _items[1]);
        Assert.Equal(new[] { "middle" }, selection.InDisplayOrder(retained));
    }

    [Fact]
    public void TransformingAnImagePreservesItsSelectionAndRangeAnchor()
    {
        var selection = new CaptureHistorySelection<string>();
        selection.SelectOnly(_items[1]);
        selection.Replace(_items[1], "rotated");
        selection.Remove(_items[1]);
        string[] transformed = ["newest", "rotated", "older", "oldest"];
        selection.SelectRange(transformed, "oldest");
        Assert.Equal(new[] { "rotated", "older", "oldest" }, selection.InDisplayOrder(transformed));
    }

    [Fact]
    public void ClearingOrRemovingAllItemsLeavesNoExportTargets()
    {
        var selection = new CaptureHistorySelection<string>();
        selection.SelectAll(_items);
        selection.Clear();
        Assert.Empty(selection.InDisplayOrder(_items));
        selection.SelectOnly(_items[1]);
        selection.Remove(_items[1]);
        Assert.Equal(0, selection.Count);
        Assert.Empty(selection.InDisplayOrder(_items));
    }
}
