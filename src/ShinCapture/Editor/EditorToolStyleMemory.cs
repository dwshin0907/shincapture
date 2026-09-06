using System;
using System.Windows.Media;
using ShinCapture.Models;

namespace ShinCapture.Editor;

public static class EditorToolStyleMemory
{
    public static bool Supports(ITool tool) =>
        tool.Name is "Pen" or "Highlighter" or "Shape" or "Arrow" or "Text" or "Number" or "Balloon";

    public static void Remember(EditorSettings settings, ITool tool)
    {
        if (!Supports(tool)) return;
        settings.ToolStyles ??= new();
        settings.ToolStyles[tool.Name] = new EditorToolStyle
        {
            Color = tool.CurrentColor.ToString(),
            Width = tool.CurrentWidth
        };
    }

    public static void Restore(EditorSettings settings, ITool tool)
    {
        if (!Supports(tool) || settings.ToolStyles == null ||
            !settings.ToolStyles.TryGetValue(tool.Name, out var style) || style == null)
            return;

        try
        {
            if (!string.IsNullOrWhiteSpace(style.Color))
                tool.CurrentColor = (Color)ColorConverter.ConvertFromString(style.Color);
        }
        catch (FormatException) { }
        catch (NotSupportedException) { }

        double maxWidth = tool.Name is "Highlighter" or "Number" ? 40 : 20;
        if (double.IsFinite(style.Width) && style.Width >= 1 && style.Width <= maxWidth)
            tool.CurrentWidth = style.Width;
    }
}
