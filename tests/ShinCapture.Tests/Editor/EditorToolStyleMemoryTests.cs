using System.Windows.Media;
using ShinCapture.Editor;
using ShinCapture.Editor.Tools;
using ShinCapture.Models;
using ShinCapture.Services;

namespace ShinCapture.Tests.Editor;

public class EditorToolStyleMemoryTests
{
    [Fact]
    public void RestoresIndependentToolStylesAfterSettingsReload()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ShinCapture_Styles_{Guid.NewGuid():N}");
        try
        {
            var manager = new SettingsManager(directory);
            var settings = manager.Load();
            var shape = new ShapeTool([]) { CurrentColor = Colors.Red, CurrentWidth = 1.5 };
            var pen = new PenTool([]) { CurrentColor = Colors.Blue, CurrentWidth = 7 };
            var highlighter = new HighlighterTool([]) { CurrentWidth = 4 };
            EditorToolStyleMemory.Remember(settings.Editor, shape);
            EditorToolStyleMemory.Remember(settings.Editor, pen);
            EditorToolStyleMemory.Remember(settings.Editor, highlighter);
            manager.Save(settings);

            var reloaded = new SettingsManager(directory).Load();
            var newShape = new ShapeTool([]);
            var newPen = new PenTool([]);
            var newHighlighter = new HighlighterTool([]);
            EditorToolStyleMemory.Restore(reloaded.Editor, newShape);
            EditorToolStyleMemory.Restore(reloaded.Editor, newPen);
            EditorToolStyleMemory.Restore(reloaded.Editor, newHighlighter);

            Assert.Equal(Colors.Red, newShape.CurrentColor);
            Assert.Equal(1.5, newShape.CurrentWidth);
            Assert.Equal(Colors.Blue, newPen.CurrentColor);
            Assert.Equal(7, newPen.CurrentWidth);
            Assert.Equal(4, newHighlighter.CurrentWidth);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1000000)]
    [InlineData(double.NaN)]
    public void InvalidSavedStyleKeepsToolDefaults(double width)
    {
        var tool = new ShapeTool([]);
        var color = tool.CurrentColor;
        double defaultWidth = tool.CurrentWidth;
        var settings = new EditorSettings
        {
            ToolStyles = new() { [tool.Name] = new() { Color = "invalid", Width = width } }
        };

        EditorToolStyleMemory.Restore(settings, tool);

        Assert.Equal(color, tool.CurrentColor);
        Assert.Equal(defaultWidth, tool.CurrentWidth);
    }

    [Fact]
    public void LegacyOrNullStylesKeepDefaultsAndAllowRemembering()
    {
        var settings = new EditorSettings { ToolStyles = null! };
        var tool = new ShapeTool([]) { CurrentColor = Colors.Red, CurrentWidth = 2 };
        EditorToolStyleMemory.Restore(settings, tool);
        EditorToolStyleMemory.Remember(settings, tool);
        Assert.Equal(2, settings.ToolStyles[tool.Name].Width);
    }
}
