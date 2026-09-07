using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;

namespace ShinCapture.Capture;

public static class SmartCutGeometry
{
    public static bool TryCloseAndValidate(IReadOnlyList<System.Windows.Point> input, double width, double height, out List<System.Windows.Point> closed)
    {
        closed = new List<System.Windows.Point>();
        if (input.Count < 3 || width <= 0 || height <= 0) return false;
        foreach (System.Windows.Point point in input)
        {
            if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) return false;
            if (closed.Count == 0 || (point - closed[^1]).LengthSquared > 1) closed.Add(point);
        }
        if (closed.Count < 3) return false;
        double minX = closed.Min(point => point.X), maxX = closed.Max(point => point.X);
        double minY = closed.Min(point => point.Y), maxY = closed.Max(point => point.Y);
        double diagonal = Math.Sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY));
        if ((closed[0] - closed[^1]).Length > Math.Clamp(diagonal * 0.12, 10, 64)) return false;
        double area = Math.Abs(SignedArea(closed));
        if (area < 24) return false;
        closed.Add(closed[0]);
        return true;
    }

    public static Rectangle ComputeClampedRegion(IReadOnlyList<System.Windows.Point> points, double scaleX, double scaleY, int bitmapWidth, int bitmapHeight)
    {
        if (points.Count == 0 || bitmapWidth <= 0 || bitmapHeight <= 0 || !double.IsFinite(scaleX) || !double.IsFinite(scaleY) || scaleX <= 0 || scaleY <= 0) return Rectangle.Empty;
        double minX = points.Min(p => p.X) * scaleX, minY = points.Min(p => p.Y) * scaleY;
        double maxX = points.Max(p => p.X) * scaleX, maxY = points.Max(p => p.Y) * scaleY;
        int paddingX = Math.Max(2, (int)Math.Ceiling((maxX - minX) * 0.02));
        int paddingY = Math.Max(2, (int)Math.Ceiling((maxY - minY) * 0.02));
        int left = Math.Clamp((int)Math.Floor(minX) - paddingX, 0, bitmapWidth - 1);
        int top = Math.Clamp((int)Math.Floor(minY) - paddingY, 0, bitmapHeight - 1);
        int right = Math.Clamp((int)Math.Ceiling(maxX) + paddingX, left + 1, bitmapWidth);
        int bottom = Math.Clamp((int)Math.Ceiling(maxY) + paddingY, top + 1, bitmapHeight);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    public static PointF[] ToLocalPolygon(IReadOnlyList<System.Windows.Point> points, double scaleX, double scaleY, Rectangle region) =>
        points.Select(point => new PointF((float)(point.X * scaleX - region.X), (float)(point.Y * scaleY - region.Y))).ToArray();

    private static double SignedArea(IReadOnlyList<System.Windows.Point> points)
    {
        double area = 0;
        for (int i = 0; i < points.Count; i++)
        {
            System.Windows.Point a = points[i], b = points[(i + 1) % points.Count];
            area += a.X * b.Y - b.X * a.Y;
        }
        return area / 2;
    }
}
