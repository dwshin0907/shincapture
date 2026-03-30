using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public class TextObject : EditorObject
{
    public string Text { get; set; } = string.Empty;
    public Point Position { get; set; }
    public Color TextColor { get; set; } = Colors.Black;
    public FontFamily FontFamily { get; set; } = new FontFamily("Segoe UI");
    public double FontSize { get; set; } = 16.0;
    public bool Bold { get; set; } = false;
    public bool Italic { get; set; } = false;
    public bool Underline { get; set; } = false;

    private FormattedText BuildFormattedText()
    {
        var weight = Bold ? FontWeights.Bold : FontWeights.Normal;
        var style = Italic ? FontStyles.Italic : FontStyles.Normal;
        var typeface = new Typeface(FontFamily, style, weight, FontStretches.Normal);

        var decorations = new TextDecorationCollection();
        if (Underline)
            decorations.Add(TextDecorations.Underline);

        var ft = new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            new SolidColorBrush(TextColor),
            VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);

        if (Underline)
            ft.SetTextDecorations(decorations);

        return ft;
    }

    public override Rect Bounds
    {
        get
        {
            if (string.IsNullOrEmpty(Text)) return new Rect(Position, new Size(0, 0));
            var ft = BuildFormattedText();
            return new Rect(Position, new Size(ft.Width, ft.Height));
        }
    }

    public override void Render(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;
        var ft = BuildFormattedText();
        dc.DrawText(ft, Position);
    }

    public override bool HitTest(Point point)
    {
        return Bounds.Contains(point);
    }

    public override EditorObject Clone()
    {
        return new TextObject
        {
            Text = Text,
            Position = Position,
            TextColor = TextColor,
            FontFamily = FontFamily,
            FontSize = FontSize,
            Bold = Bold,
            Italic = Italic,
            Underline = Underline,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
