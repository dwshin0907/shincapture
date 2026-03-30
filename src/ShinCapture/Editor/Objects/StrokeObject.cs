using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor.Objects;

public class StrokeObject : EditorObject
{
    public List<Point> Points { get; set; } = new();
    public Color StrokeColor { get; set; } = Colors.Black;
    public double StrokeWidth { get; set; } = 3.0;
    public double Opacity { get; set; } = 1.0;
    public bool IsHighlighter { get; set; } = false;

    public override Rect Bounds
    {
        get
        {
            if (Points.Count == 0) return Rect.Empty;
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in Points)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            double pad = StrokeWidth / 2.0;
            return new Rect(minX - pad, minY - pad, maxX - minX + StrokeWidth, maxY - minY + StrokeWidth);
        }
    }

    public override void Render(DrawingContext dc)
    {
        if (Points.Count < 2) return;

        var color = IsHighlighter
            ? Color.FromArgb((byte)(128 * Opacity), StrokeColor.R, StrokeColor.G, StrokeColor.B)
            : Color.FromArgb((byte)(255 * Opacity), StrokeColor.R, StrokeColor.G, StrokeColor.B);

        var pen = new Pen(new SolidColorBrush(color), StrokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(Points[0], false, false);
            ctx.PolyLineTo(Points.Skip(1).ToList(), true, true);
        }
        geometry.Freeze();

        dc.DrawGeometry(null, pen, geometry);
    }

    public override bool HitTest(Point point)
    {
        double threshold = StrokeWidth + 5.0;
        foreach (var p in Points)
        {
            double dx = point.X - p.X;
            double dy = point.Y - p.Y;
            if (dx * dx + dy * dy <= threshold * threshold)
                return true;
        }
        return false;
    }

    public override EditorObject Clone()
    {
        return new StrokeObject
        {
            Points = new List<Point>(Points),
            StrokeColor = StrokeColor,
            StrokeWidth = StrokeWidth,
            Opacity = Opacity,
            IsHighlighter = IsHighlighter,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
