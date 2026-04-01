using System;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor;

public abstract class EditorObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public bool IsSelected { get; set; }
    public bool IsVisible { get; set; } = true;

    /// <summary>회전 각도 (도, 중심 기준)</summary>
    public double Rotation { get; set; } = 0;

    public abstract Rect Bounds { get; }

    /// <summary>회전 적용된 렌더링</summary>
    public void RenderWithTransform(DrawingContext dc)
    {
        if (Math.Abs(Rotation) > 0.01)
        {
            var center = Bounds.Center();
            dc.PushTransform(new RotateTransform(Rotation, center.X, center.Y));
            Render(dc);
            dc.Pop();
        }
        else
        {
            Render(dc);
        }
    }

    public abstract void Render(DrawingContext dc);

    /// <summary>회전 고려한 히트 테스트</summary>
    public bool HitTestWithTransform(Point point)
    {
        if (Math.Abs(Rotation) > 0.01)
        {
            // 포인트를 역회전하여 로컬 좌표로 변환
            var center = Bounds.Center();
            var rad = -Rotation * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            var dx = point.X - center.X;
            var dy = point.Y - center.Y;
            var local = new Point(center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos);
            return HitTest(local);
        }
        return HitTest(point);
    }

    public abstract bool HitTest(Point point);
    public abstract EditorObject Clone();
    public abstract void Move(Vector delta);

    /// <summary>anchor 기준으로 비례 확대/축소</summary>
    public abstract void Scale(double factor, Point anchor);
}

public static class RectExtensions
{
    public static Point Center(this Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
}
