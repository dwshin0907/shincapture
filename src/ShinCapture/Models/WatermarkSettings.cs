namespace ShinCapture.Models;

public sealed class WatermarkSettings
{
    public string Text { get; set; } = "© 내 워터마크";
    public double FontSize { get; set; } = 32;
    public double Opacity { get; set; } = 0.4;
    public bool White { get; set; } = true;
    // 0..8: top-left to bottom-right in a 3 × 3 grid.
    public int Position { get; set; } = 8;
}
