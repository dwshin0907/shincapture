using ShinCapture.Editor;

namespace ShinCapture.Tests.Editor;

public class EditorToolbarCatalogTests
{
    [Fact]
    public void DefinesFourteenUniqueToolsWithAccessibleMetadata()
    {
        IReadOnlyList<EditorToolDescriptor> tools = EditorToolbarCatalog.Tools;

        Assert.Equal(14, tools.Count);
        Assert.Equal(tools.Count, tools.Select(tool => tool.Name).Distinct().Count());
        Assert.Equal(tools.Count, tools.Select(tool => tool.IconKey).Distinct().Count());
        Assert.All(tools, tool =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Name));
            Assert.False(string.IsNullOrWhiteSpace(tool.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(tool.IconKey));
            Assert.False(string.IsNullOrWhiteSpace(tool.Group));
            Assert.False(string.IsNullOrWhiteSpace(tool.ToolTip));
            Assert.All(tool.IconKey, character => Assert.True(character <= 127));
        });
    }

    [Fact]
    public void KeepsExistingToolOrderAndShortcutContract()
    {
        Assert.Equal(
            ["선택", "펜", "형광펜", "도형", "화살표", "텍스트", "말풍선", "모자이크", "블러", "번호", "이미지", "색상추출", "크롭", "지우개"],
            EditorToolbarCatalog.Tools.Select(tool => tool.Name));

        Assert.Equal("V", EditorToolbarCatalog.Tools.Single(tool => tool.Name == "선택").Shortcut);
        Assert.Equal("T", EditorToolbarCatalog.Tools.Single(tool => tool.Name == "텍스트").Shortcut);
        Assert.Equal("", EditorToolbarCatalog.Tools.Single(tool => tool.Name == "블러").Shortcut);
    }

    [Fact]
    public void KeepsCommonToolsDirectlyDiscoverableAtCompactWidths()
    {
        string[] expected = ["선택", "펜", "형광펜", "도형", "화살표", "텍스트", "모자이크"];

        Assert.Equal(
            expected,
            EditorToolbarCatalog.Tools
                .Where(tool => tool.Visibility <= EditorToolVisibility.Common)
                .Select(tool => tool.Name));
        Assert.Equal("자르기", EditorToolbarCatalog.Tools.Single(tool => tool.Name == "크롭").DisplayName);
        Assert.Equal("색상 추출", EditorToolbarCatalog.Tools.Single(tool => tool.Name == "색상추출").DisplayName);
    }
}
