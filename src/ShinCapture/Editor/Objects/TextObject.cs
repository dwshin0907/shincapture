using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ShinCapture.Editor.Objects;

public class TextObject : EditorObject
{
    public string Text { get; set; } = string.Empty;
    public Point Position { get; set; }
    public Color TextColor { get; set; } = Colors.Black;
    public FontFamily FontFamily { get; set; } = new FontFamily("Pretendard, Malgun Gothic, Segoe UI");
    public double FontSize { get; set; } = 16.0;
    public bool Bold { get; set; } = false;
    public bool Italic { get; set; } = false;
    public bool Underline { get; set; } = false;
    public bool GlassBackground { get; set; } = false;
    public Color? FillColor { get; set; }
    public Color? BorderColor { get; set; }
    public double BorderWidth { get; set; } = 2.0;

    private const double GlassPadH = 12.0;
    private const double GlassPadV = 8.0;
    private const double GlassRadius = 8.0;
    private const double BoxPadH = 10.0;
    private const double BoxPadV = 6.0;
    private const double BoxRadius = 6.0;

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

    private bool HasBox => GlassBackground || FillColor.HasValue || BorderColor.HasValue;

    public override Rect Bounds
    {
        get
        {
            if (string.IsNullOrEmpty(Text)) return new Rect(Position, new Size(0, 0));
            var ft = BuildFormattedText();
            if (HasBox)
                return new Rect(
                    Position.X - GlassPadH, Position.Y - GlassPadV,
                    ft.Width + GlassPadH * 2, ft.Height + GlassPadV * 2);
            return new Rect(Position, new Size(ft.Width, ft.Height));
        }
    }

    public override void Render(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;

        var ft = BuildFormattedText();

        if (HasBox)
        {
            var boxRect = new Rect(
                Position.X - GlassPadH, Position.Y - GlassPadV,
                ft.Width + GlassPadH * 2, ft.Height + GlassPadV * 2);

            // 1) 배경색
            if (FillColor.HasValue)
            {
                byte alpha = GlassBackground ? (byte)160 : (byte)255;
                var c = FillColor.Value;
                var fill = new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
                fill.Freeze();
                dc.DrawRoundedRectangle(fill, null, boxRect, GlassRadius, GlassRadius);
            }
            else if (GlassBackground)
            {
                // 배경색 없이 반투명만: 흰색 반투명
                var fill = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
                fill.Freeze();
                dc.DrawRoundedRectangle(fill, null, boxRect, GlassRadius, GlassRadius);
            }

            // 3) 테두리 (가장 위)
            if (BorderColor.HasValue)
            {
                var bb = new SolidColorBrush(BorderColor.Value);
                bb.Freeze();
                var borderPen = new Pen(bb, BorderWidth);
                borderPen.Freeze();
                dc.DrawRoundedRectangle(null, borderPen, boxRect, GlassRadius, GlassRadius);
            }
        }

        dc.DrawText(ft, Position);
    }

    public override bool HitTest(Point point)
    {
        return Bounds.Contains(point);
    }

    public override void Scale(double factor, Point anchor)
    {
        Position = ScalePoint(Position, factor, anchor);
        FontSize = Math.Max(6, FontSize * factor);
    }

    private static Point ScalePoint(Point p, double f, Point a) =>
        new(a.X + (p.X - a.X) * f, a.Y + (p.Y - a.Y) * f);

    public override void Move(Vector delta)
    {
        Position = new Point(Position.X + delta.X, Position.Y + delta.Y);
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
            GlassBackground = GlassBackground,
            FillColor = FillColor,
            BorderColor = BorderColor,
            BorderWidth = BorderWidth,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
