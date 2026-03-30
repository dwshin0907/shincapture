using System;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public class ArrowObject : EditorObject
{
    public Point Start { get; set; }
    public Point End { get; set; }
    public Color Color { get; set; } = Colors.Red;
    public double StrokeWidth { get; set; } = 2.0;
    public double HeadSize { get; set; } = 12.0;

    public override Rect Bounds
    {
        get
        {
            double x = Math.Min(Start.X, End.X) - HeadSize;
            double y = Math.Min(Start.Y, End.Y) - HeadSize;
            double w = Math.Abs(End.X - Start.X) + HeadSize * 2;
            double h = Math.Abs(End.Y - Start.Y) + HeadSize * 2;
            return new Rect(x, y, w, h);
        }
    }

    public override void Render(DrawingContext dc)
    {
        var brush = new SolidColorBrush(Color);
        brush.Freeze();

        var pen = new Pen(brush, StrokeWidth);
        pen.Freeze();

        dc.DrawLine(pen, Start, End);

        // Arrowhead
        double angle = Math.Atan2(End.Y - Start.Y, End.X - Start.X);
        double headAngle = Math.PI / 6.0; // 30 degrees

        var tip = End;
        var left = new Point(
            tip.X - HeadSize * Math.Cos(angle - headAngle),
            tip.Y - HeadSize * Math.Sin(angle - headAngle));
        var right = new Point(
            tip.X - HeadSize * Math.Cos(angle + headAngle),
            tip.Y - HeadSize * Math.Sin(angle + headAngle));

        var headGeometry = new StreamGeometry();
        using (var ctx = headGeometry.Open())
        {
            ctx.BeginFigure(tip, true, true);
            ctx.LineTo(left, true, false);
            ctx.LineTo(right, true, false);
        }
        headGeometry.Freeze();

        dc.DrawGeometry(brush, pen, headGeometry);
    }

    public override bool HitTest(Point point)
    {
        double dx = End.X - Start.X;
        double dy = End.Y - Start.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq == 0) return false;
        double t = Math.Max(0, Math.Min(1, ((point.X - Start.X) * dx + (point.Y - Start.Y) * dy) / lenSq));
        double px = Start.X + t * dx;
        double py = Start.Y + t * dy;
        double dist = Math.Sqrt((point.X - px) * (point.X - px) + (point.Y - py) * (point.Y - py));
        return dist <= StrokeWidth + 5.0;
    }

    public override EditorObject Clone()
    {
        return new ArrowObject
        {
            Start = Start,
            End = End,
            Color = Color,
            StrokeWidth = StrokeWidth,
            HeadSize = HeadSize,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
