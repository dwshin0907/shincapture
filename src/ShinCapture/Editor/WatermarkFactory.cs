using System;
using System.Windows;
using System.Windows.Media;
using ShinCapture.Editor.Objects;
using ShinCapture.Models;

namespace ShinCapture.Editor;

public static class WatermarkFactory
{
    public static TextObject Create(int imageWidth, int imageHeight, WatermarkSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (imageWidth <= 0 || imageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(imageWidth));
        if (string.IsNullOrWhiteSpace(settings.Text)) throw new ArgumentException("워터마크 문구를 입력하세요.");
        double size = double.IsFinite(settings.FontSize) ? Math.Clamp(settings.FontSize, 8, 200) : 32;
        double opacity = double.IsFinite(settings.Opacity) ? Math.Clamp(settings.Opacity, 0.05, 1) : 0.4;
        byte channel = settings.White ? (byte)255 : (byte)0;
        var text = new TextObject
        {
            Text = settings.Text.Trim(), FontSize = size, Bold = true,
            TextColor = Color.FromArgb((byte)Math.Round(opacity * 255), channel, channel, channel)
        };
        double margin = Math.Min(20, Math.Min(imageWidth, imageHeight) * 0.05);
        var bounds = text.Bounds;
        double fit = Math.Min(1, Math.Min((imageWidth - margin * 2) / Math.Max(1, bounds.Width),
            (imageHeight - margin * 2) / Math.Max(1, bounds.Height)));
        text.FontSize *= fit;
        bounds = text.Bounds;
        int position = Math.Clamp(settings.Position, 0, 8);
        text.Position = new Point(margin + (imageWidth - margin * 2 - bounds.Width) * (position % 3) / 2,
            margin + (imageHeight - margin * 2 - bounds.Height) * (position / 3) / 2);
        return text;
    }
}
