using System.Collections.Generic;

namespace ShinCapture.Models;

public sealed class EditorSettings
{
    public WatermarkSettings Watermark { get; set; } = new();
    public EditorWindowSizeMode WindowSizeMode { get; set; } = EditorWindowSizeMode.RememberLast;
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 750;
    public Dictionary<string, EditorToolStyle> ToolStyles { get; set; } = new();
}

public sealed class EditorToolStyle
{
    public string Color { get; set; } = "#FF000000";
    public double Width { get; set; } = 3;
}
