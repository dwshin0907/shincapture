using System;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public enum ShapeType
{
    Rectangle,
    RoundedRect,
    Ellipse,
    Triangle,
    Diamond,
    Star,
    Line,
    DashedLine
}

public enum FillMode
{
    None,
    Solid,
    SemiTransparent
}

public static class RectExtensions
{
    public static Point Center(this Rect rect) =>
        new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0);
}

public class ShapeObject : EditorObject
{
    public Point Start { get; set; }
    public Point End { get; set; }
    public ShapeType ShapeType { get; set; } = ShapeType.Rectangle;
    public Color StrokeColor { get; set; } = Colors.Red;
    public double StrokeWidth { get; set; } = 2.0;
    public FillMode FillMode { get; set; } = FillMode.None;

    public override Rect Bounds
    {
        get
        {
            double x = Math.Min(Start.X, End.X);
            double y = Math.Min(Start.Y, End.Y);
            double w = Math.Abs(End.X - Start.X);
            double h = Math.Abs(End.Y - Start.Y);
            return new Rect(x, y, w, h);
        }
    }

    private Brush? GetFillBrush()
    {
        return FillMode switch
        {
            FillMode.None => null,
            FillMode.Solid => new SolidColorBrush(StrokeColor),
            FillMode.SemiTransparent => new SolidColorBrush(
                Color.FromArgb(64, StrokeColor.R, StrokeColor.G, StrokeColor.B)),
            _ => null
        };
    }

    public override void Render(DrawingContext dc)
    {
        var pen = new Pen(new SolidColorBrush(StrokeColor), StrokeWidth);

        if (ShapeType == ShapeType.DashedLine)
            pen.DashStyle = new DashStyle(new double[] { 4, 2 }, 0);

        pen.Freeze();

        var fill = GetFillBrush();
        var rect = Bounds;
        var center = rect.Center();

        switch (ShapeType)
        {
            case ShapeType.Rectangle:
                dc.DrawRectangle(fill, pen, rect);
                break;

            case ShapeType.RoundedRect:
                // 내부 꼭지점에도 곡률 유지: innerRadius = radius - strokeWidth/2 >= 8
                double radius = Math.Max(10, StrokeWidth / 2 + 8);
                dc.DrawRoundedRectangle(fill, pen, rect, radius, radius);
                break;

            case ShapeType.Ellipse:
                dc.DrawEllipse(fill, pen, center, rect.Width / 2.0, rect.Height / 2.0);
                break;

            case ShapeType.Triangle:
                var tri = new StreamGeometry();
                using (var ctx = tri.Open())
                {
                    ctx.BeginFigure(new Point(center.X, rect.Top), true, true);
                    ctx.LineTo(new Point(rect.Right, rect.Bottom), true, false);
                    ctx.LineTo(new Point(rect.Left, rect.Bottom), true, false);
                }
                tri.Freeze();
                dc.DrawGeometry(fill, pen, tri);
                break;

            case ShapeType.Diamond:
                var dia = new StreamGeometry();
                using (var ctx = dia.Open())
                {
                    ctx.BeginFigure(new Point(center.X, rect.Top), true, true);
                    ctx.LineTo(new Point(rect.Right, center.Y), true, false);
                    ctx.LineTo(new Point(center.X, rect.Bottom), true, false);
                    ctx.LineTo(new Point(rect.Left, center.Y), true, false);
                }
                dia.Freeze();
                dc.DrawGeometry(fill, pen, dia);
                break;

            case ShapeType.Star:
                var star = BuildStarGeometry(center, rect.Width / 2.0, rect.Height / 2.0, 5);
                dc.DrawGeometry(fill, pen, star);
                break;

            case ShapeType.Line:
            case ShapeType.DashedLine:
                dc.DrawLine(pen, Start, End);
                break;
        }
    }

    private static StreamGeometry BuildStarGeometry(Point center, double outerRx, double outerRy, int points)
    {
        double innerRx = outerRx * 0.4;
        double innerRy = outerRy * 0.4;
        var geo = new StreamGeometry();
        using var ctx = geo.Open();

        double startAngle = -Math.PI / 2;
        var first = new Point(
            center.X + outerRx * Math.Cos(startAngle),
            center.Y + outerRy * Math.Sin(startAngle));
        ctx.BeginFigure(first, true, true);

        for (int i = 0; i < points; i++)
        {
            double outerAngle = startAngle + i * 2 * Math.PI / points;
            double innerAngle = outerAngle + Math.PI / points;
            double nextOuterAngle = outerAngle + 2 * Math.PI / points;

            ctx.LineTo(new Point(
                center.X + innerRx * Math.Cos(innerAngle),
                center.Y + innerRy * Math.Sin(innerAngle)), true, false);
            ctx.LineTo(new Point(
                center.X + outerRx * Math.Cos(nextOuterAngle),
                center.Y + outerRy * Math.Sin(nextOuterAngle)), true, false);
        }

        geo.Freeze();
        return geo;
    }

    public override bool HitTest(Point point)
    {
        var rect = Bounds;
        double pad = StrokeWidth + 4.0;
        var outer = new Rect(rect.X - pad, rect.Y - pad, rect.Width + pad * 2, rect.Height + pad * 2);

        if (!outer.Contains(point)) return false;

        if (ShapeType == ShapeType.Line || ShapeType == ShapeType.DashedLine)
            return DistanceToSegment(point, Start, End) <= StrokeWidth + 4.0;

        if (FillMode != FillMode.None && rect.Contains(point)) return true;

        var inner = new Rect(rect.X + pad, rect.Y + pad,
            Math.Max(0, rect.Width - pad * 2), Math.Max(0, rect.Height - pad * 2));
        return !inner.Contains(point);
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq == 0) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq));
        double px = a.X + t * dx;
        double py = a.Y + t * dy;
        return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
    }

    public override void Scale(double factor, Point anchor)
    {
        Start = ScalePoint(Start, factor, anchor);
        End = ScalePoint(End, factor, anchor);
        StrokeWidth = Math.Max(0.5, StrokeWidth * factor);
    }

    private static Point ScalePoint(Point p, double f, Point a) =>
        new(a.X + (p.X - a.X) * f, a.Y + (p.Y - a.Y) * f);

    public override void Move(Vector delta)
    {
        Start = new Point(Start.X + delta.X, Start.Y + delta.Y);
        End = new Point(End.X + delta.X, End.Y + delta.Y);
    }

    public override EditorObject Clone()
    {
        return new ShapeObject
        {
            Start = Start,
            End = End,
            ShapeType = ShapeType,
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth,
            FillMode = FillMode,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
