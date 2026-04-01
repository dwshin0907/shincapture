using System;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public enum ArrowHeadStyle
{
    Arrow,
    Circle,
    Diamond,
    None
}

public enum ArrowLineStyle
{
    Solid,
    Dashed,
    Dotted,
    DashDot,
    DashDotDot
}

public class ArrowObject : EditorObject
{
    public Point Start { get; set; }
    public Point End { get; set; }
    public Color Color { get; set; } = Colors.Red;
    public double StrokeWidth { get; set; } = 2.0;
    public double HeadSize { get; set; } = 12.0;
    public ArrowHeadStyle HeadStyle { get; set; } = ArrowHeadStyle.Arrow;
    public ArrowLineStyle LineStyle { get; set; } = ArrowLineStyle.Solid;

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
        var pen = new Pen(brush, StrokeWidth) { LineJoin = PenLineJoin.Miter, MiterLimit = 10 };
        pen.DashStyle = LineStyle switch
        {
            ArrowLineStyle.Dashed => new DashStyle(new double[] { 4, 3 }, 0),
            ArrowLineStyle.Dotted => new DashStyle(new double[] { 1, 2 }, 0),
            ArrowLineStyle.DashDot => new DashStyle(new double[] { 4, 2, 1, 2 }, 0),
            ArrowLineStyle.DashDotDot => new DashStyle(new double[] { 4, 2, 1, 2, 1, 2 }, 0),
            _ => DashStyles.Solid
        };
        pen.Freeze();

        dc.DrawLine(pen, Start, End);

        double angle = Math.Atan2(End.Y - Start.Y, End.X - Start.X);

        // 헤드는 항상 솔리드 펜 사용 (점선/파선이 헤드에 적용되면 안 됨)
        var headPen = new Pen(brush, StrokeWidth) { LineJoin = PenLineJoin.Miter, MiterLimit = 10 };
        headPen.Freeze();

        switch (HeadStyle)
        {
            case ArrowHeadStyle.Arrow:
                RenderArrowHead(dc, brush, headPen, angle);
                break;
            case ArrowHeadStyle.Circle:
                dc.DrawEllipse(brush, null, End, HeadSize * 0.4, HeadSize * 0.4);
                break;
            case ArrowHeadStyle.Diamond:
                RenderDiamondHead(dc, brush, headPen, angle);
                break;
            case ArrowHeadStyle.None:
                break;
        }
    }

    private void RenderArrowHead(DrawingContext dc, Brush brush, Pen pen, double angle)
    {
        double headAngle = Math.PI / 6.0;
        var tip = End;
        var left = new Point(
            tip.X - HeadSize * Math.Cos(angle - headAngle),
            tip.Y - HeadSize * Math.Sin(angle - headAngle));
        var right = new Point(
            tip.X - HeadSize * Math.Cos(angle + headAngle),
            tip.Y - HeadSize * Math.Sin(angle + headAngle));

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(tip, true, true);
            ctx.LineTo(left, true, false);
            ctx.LineTo(right, true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, pen, geo);
    }

    private void RenderDiamondHead(DrawingContext dc, Brush brush, Pen pen, double angle)
    {
        double s = HeadSize * 0.5;
        var tip = End;
        var top = new Point(tip.X + s * Math.Cos(angle), tip.Y + s * Math.Sin(angle));
        var bottom = new Point(tip.X - s * Math.Cos(angle), tip.Y - s * Math.Sin(angle));
        var left = new Point(tip.X - s * Math.Cos(angle + Math.PI / 2), tip.Y - s * Math.Sin(angle + Math.PI / 2));
        var right = new Point(tip.X + s * Math.Cos(angle + Math.PI / 2), tip.Y + s * Math.Sin(angle + Math.PI / 2));

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(top, true, true);
            ctx.LineTo(right, true, false);
            ctx.LineTo(bottom, true, false);
            ctx.LineTo(left, true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(brush, pen, geo);
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

    public override void Scale(double factor, Point anchor)
    {
        Start = ScalePoint(Start, factor, anchor);
        End = ScalePoint(End, factor, anchor);
        StrokeWidth = Math.Max(0.5, StrokeWidth * factor);
        HeadSize = Math.Max(4, HeadSize * factor);
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
        return new ArrowObject
        {
            Start = Start,
            End = End,
            Color = Color,
            StrokeWidth = StrokeWidth,
            HeadSize = HeadSize,
            HeadStyle = HeadStyle,
            LineStyle = LineStyle,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
