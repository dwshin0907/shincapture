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
    public List<Color>? GradientColors { get; set; }

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

        // 그라데이션 모드: 세그먼트별로 색상 보간
        if (GradientColors != null && GradientColors.Count >= 2)
        {
            RenderGradient(dc);
            return;
        }

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

    private void RenderGradient(DrawingContext dc)
    {
        var colors = GradientColors!;
        byte a = IsHighlighter ? (byte)(128 * Opacity) : (byte)(255 * Opacity);

        // 전체 경로를 하나의 지오메트리로 생성
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(Points[0], false, false);
            ctx.PolyLineTo(Points.Skip(1).ToList(), true, true);
        }
        geometry.Freeze();

        // 시작점→끝점 방향으로 그라데이션 브러시 적용
        var gradBrush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = Points[0],
            EndPoint = Points[Points.Count - 1]
        };
        for (int i = 0; i < colors.Count; i++)
        {
            var c = colors[i];
            gradBrush.GradientStops.Add(new GradientStop(
                Color.FromArgb(a, c.R, c.G, c.B),
                (double)i / (colors.Count - 1)));
        }
        gradBrush.Freeze();

        var pen = new Pen(gradBrush, StrokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();

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

    public override void Scale(double factor, Point anchor)
    {
        for (int i = 0; i < Points.Count; i++)
        {
            var p = Points[i];
            Points[i] = new Point(
                anchor.X + (p.X - anchor.X) * factor,
                anchor.Y + (p.Y - anchor.Y) * factor);
        }
        StrokeWidth = Math.Max(0.5, StrokeWidth * factor);
    }

    public override void Move(Vector delta)
    {
        for (int i = 0; i < Points.Count; i++)
            Points[i] = new Point(Points[i].X + delta.X, Points[i].Y + delta.Y);
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
            GradientColors = GradientColors != null ? new List<Color>(GradientColors) : null,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
