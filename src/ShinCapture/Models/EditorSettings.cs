namespace ShinCapture.Models;

public sealed class EditorSettings
{
    public EditorWindowSizeMode WindowSizeMode { get; set; } = EditorWindowSizeMode.RememberLast;
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 750;
}
