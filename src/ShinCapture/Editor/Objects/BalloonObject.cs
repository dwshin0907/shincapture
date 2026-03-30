using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public enum BalloonStyle
{
    Rounded,
    Square
}

public class BalloonObject : EditorObject
{
    public string Text { get; set; } = string.Empty;
    public Point Position { get; set; }
    public Point TailTarget { get; set; }
    public Color FillColor { get; set; } = Colors.White;
    public Color BorderColor { get; set; } = Colors.Black;
    public BalloonStyle BalloonStyle { get; set; } = BalloonStyle.Rounded;

    private const double Padding = 10.0;
    private const double MinWidth = 60.0;
    private const double MinHeight = 30.0;

    private FormattedText BuildFormattedText()
    {
        var typeface = new Typeface("Segoe UI");
        return new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            14.0,
            new SolidColorBrush(Colors.Black),
            VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);
    }

    private Rect GetBodyRect()
    {
        double w = MinWidth;
        double h = MinHeight;

        if (!string.IsNullOrEmpty(Text))
        {
            var ft = BuildFormattedText();
            w = Math.Max(MinWidth, ft.Width + Padding * 2);
            h = Math.Max(MinHeight, ft.Height + Padding * 2);
        }

        return new Rect(Position.X, Position.Y, w, h);
    }

    public override Rect Bounds
    {
        get
        {
            var body = GetBodyRect();
            return new Rect(
                Math.Min(body.X, TailTarget.X) - 4,
                Math.Min(body.Y, TailTarget.Y) - 4,
                Math.Abs(body.X - TailTarget.X) + body.Width + 8,
                Math.Abs(body.Y - TailTarget.Y) + body.Height + 8);
        }
    }

    public override void Render(DrawingContext dc)
    {
        var fill = new SolidColorBrush(FillColor);
        fill.Freeze();
        var stroke = new SolidColorBrush(BorderColor);
        stroke.Freeze();
        var pen = new Pen(stroke, 1.5);
        pen.Freeze();

        var body = GetBodyRect();
        double radius = BalloonStyle == BalloonStyle.Rounded ? 8.0 : 0.0;

        // Tail triangle
        var bodyCenter = body.Center();
        var tailBase1 = new Point(bodyCenter.X - 8, body.Y + body.Height - 1);
        var tailBase2 = new Point(bodyCenter.X + 8, body.Y + body.Height - 1);

        // Check if tail target is below, above, left, or right of body
        // and adjust tail attachment point accordingly
        Point tb1, tb2;
        if (TailTarget.Y >= body.Y + body.Height)
        {
            tb1 = new Point(bodyCenter.X - 8, body.Y + body.Height);
            tb2 = new Point(bodyCenter.X + 8, body.Y + body.Height);
        }
        else if (TailTarget.Y <= body.Y)
        {
            tb1 = new Point(bodyCenter.X - 8, body.Y);
            tb2 = new Point(bodyCenter.X + 8, body.Y);
        }
        else if (TailTarget.X >= body.X + body.Width)
        {
            tb1 = new Point(body.X + body.Width, bodyCenter.Y - 8);
            tb2 = new Point(body.X + body.Width, bodyCenter.Y + 8);
        }
        else
        {
            tb1 = new Point(body.X, bodyCenter.Y - 8);
            tb2 = new Point(body.X, bodyCenter.Y + 8);
        }

        var tailGeometry = new StreamGeometry();
        using (var ctx = tailGeometry.Open())
        {
            ctx.BeginFigure(tb1, true, true);
            ctx.LineTo(TailTarget, true, false);
            ctx.LineTo(tb2, true, false);
        }
        tailGeometry.Freeze();

        dc.DrawGeometry(fill, pen, tailGeometry);
        dc.DrawRoundedRectangle(fill, pen, body, radius, radius);

        if (!string.IsNullOrEmpty(Text))
        {
            var ft = BuildFormattedText();
            var textPos = new Point(
                body.X + Padding,
                body.Y + (body.Height - ft.Height) / 2.0);
            dc.DrawText(ft, textPos);
        }
    }

    public override bool HitTest(Point point)
    {
        return GetBodyRect().Contains(point);
    }

    public override EditorObject Clone()
    {
        return new BalloonObject
        {
            Text = Text,
            Position = Position,
            TailTarget = TailTarget,
            FillColor = FillColor,
            BorderColor = BorderColor,
            BalloonStyle = BalloonStyle,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
