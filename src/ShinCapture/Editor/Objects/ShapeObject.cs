using System;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public enum ShapeType
{
    Rectangle,
    Ellipse,
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
        {
            pen.DashStyle = new DashStyle(new double[] { 4, 2 }, 0);
        }

        pen.Freeze();

        var fill = GetFillBrush();
        var rect = Bounds;

        switch (ShapeType)
        {
            case ShapeType.Rectangle:
                dc.DrawRoundedRectangle(fill, pen, rect, 0, 0);
                break;

            case ShapeType.Ellipse:
                var center = rect.Center();
                dc.DrawEllipse(fill, pen, center, rect.Width / 2.0, rect.Height / 2.0);
                break;

            case ShapeType.Line:
            case ShapeType.DashedLine:
                dc.DrawLine(pen, Start, End);
                break;
        }
    }

    public override bool HitTest(Point point)
    {
        var rect = Bounds;
        double pad = StrokeWidth + 4.0;
        var outer = new Rect(rect.X - pad, rect.Y - pad, rect.Width + pad * 2, rect.Height + pad * 2);

        if (!outer.Contains(point)) return false;

        if (ShapeType == ShapeType.Line || ShapeType == ShapeType.DashedLine)
        {
            return DistanceToSegment(point, Start, End) <= StrokeWidth + 4.0;
        }

        // For filled shapes, interior hit counts
        if (FillMode != FillMode.None && rect.Contains(point)) return true;

        // Check border proximity
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
