using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public class NumberObject : EditorObject
{
    public int Number { get; set; } = 1;
    public Point Center { get; set; }
    public Color CircleColor { get; set; } = Colors.Red;
    public double Radius { get; set; } = 14.0;

    public override Rect Bounds =>
        new Rect(Center.X - Radius, Center.Y - Radius, Radius * 2, Radius * 2);

    public override void Render(DrawingContext dc)
    {
        var fill = new SolidColorBrush(CircleColor);
        fill.Freeze();

        dc.DrawEllipse(fill, null, Center, Radius, Radius);

        var typeface = new Typeface(
            new FontFamily("Segoe UI"),
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal);

        double fontSize = Radius * 1.1;

        var ft = new FormattedText(
            Number.ToString(),
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.White,
            VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);

        var textOrigin = new Point(
            Center.X - ft.Width / 2.0,
            Center.Y - ft.Height / 2.0);

        dc.DrawText(ft, textOrigin);
    }

    public override bool HitTest(Point point)
    {
        double dx = point.X - Center.X;
        double dy = point.Y - Center.Y;
        return dx * dx + dy * dy <= (Radius + 4.0) * (Radius + 4.0);
    }

    public override EditorObject Clone()
    {
        return new NumberObject
        {
            Number = Number,
            Center = Center,
            CircleColor = CircleColor,
            Radius = Radius,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
