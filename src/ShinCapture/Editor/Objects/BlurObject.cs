using System;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public enum BlurStrength
{
    Light = 5,
    Medium = 15,
    Strong = 30
}

public class BlurObject : EditorObject
{
    public Rect Region { get; set; }
    public BlurStrength BlurStrength { get; set; } = BlurStrength.Medium;

    public override Rect Bounds => Region;

    public override void Render(DrawingContext dc)
    {
        // Frosted glass effect: layered semi-transparent overlays
        byte alpha = BlurStrength switch
        {
            BlurStrength.Light => 80,
            BlurStrength.Medium => 140,
            BlurStrength.Strong => 200,
            _ => 140
        };

        // Base white frosted layer
        var frost = new SolidColorBrush(Color.FromArgb(alpha, 220, 220, 230));
        frost.Freeze();
        dc.DrawRectangle(frost, null, Region);

        // Noise/depth layer to simulate blur
        int layers = (int)BlurStrength / 5;
        double layerAlpha = 15.0 / layers;
        for (int i = 0; i < Math.Min(layers, 6); i++)
        {
            double offset = i * 2.0;
            var layerColor = Color.FromArgb(
                (byte)(layerAlpha * 255 / 100),
                180, 180, 200);
            var layerBrush = new SolidColorBrush(layerColor);
            layerBrush.Freeze();
            var layerRect = new Rect(
                Region.X + offset,
                Region.Y + offset,
                Math.Max(0, Region.Width - offset * 2),
                Math.Max(0, Region.Height - offset * 2));
            dc.DrawRectangle(layerBrush, null, layerRect);
        }
    }

    public override bool HitTest(Point point)
    {
        return Region.Contains(point);
    }

    public override EditorObject Clone()
    {
        return new BlurObject
        {
            Region = Region,
            BlurStrength = BlurStrength,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
