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
    public string FontName { get; set; } = "Pretendard";
    public double FontSize { get; set; } = 14.0;
    public double TailWidth { get; set; } = 14.0;

    private const double PaddingH = 16.0;
    private const double PaddingV = 12.0;
    private const double MinWidth = 80.0;
    private const double MinHeight = 36.0;
    private const double ShadowOffset = 3.0;
    private const double HandleRadius = 6.0;
    private const double HandleHitRadius = 12.0;

    private FormattedText BuildFormattedText(Color color)
    {
        var typeface = new Typeface(new FontFamily(FontName),
            FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        return new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            new SolidColorBrush(color),
            VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);
    }

    public Rect GetBodyRect()
    {
        double w = MinWidth;
        double h = MinHeight;

        if (!string.IsNullOrEmpty(Text))
        {
            var ft = BuildFormattedText(Colors.Black);
            w = Math.Max(MinWidth, ft.Width + PaddingH * 2);
            h = Math.Max(MinHeight, ft.Height + PaddingV * 2);
        }

        return new Rect(Position.X, Position.Y, w, h);
    }

    // ── 핸들 위치 계산 ──

    public enum TailHandle { None, Tip, Left, Right }

    /// <summary>꼬리의 3개 핸들 위치 반환 (tip, baseLeft, baseRight)</summary>
    public (Point tip, Point baseLeft, Point baseRight) GetTailHandles()
    {
        var body = GetBodyRect();
        var (attachPoint, _, tailDir) = ComputeTailAttachment(body);

        Vector perp = tailDir == TailDirection.Vertical
            ? new Vector(1, 0) : new Vector(0, 1);
        var halfW = TailWidth / 2;

        return (TailTarget, attachPoint + perp * halfW, attachPoint - perp * halfW);
    }

    /// <summary>포인트가 어떤 핸들에 히트하는지 반환</summary>
    public TailHandle HitTestHandles(Point point)
    {
        var (tip, left, right) = GetTailHandles();

        if (DistSq(point, tip) <= HandleHitRadius * HandleHitRadius)
            return TailHandle.Tip;
        if (DistSq(point, left) <= HandleHitRadius * HandleHitRadius)
            return TailHandle.Left;
        if (DistSq(point, right) <= HandleHitRadius * HandleHitRadius)
            return TailHandle.Right;

        return TailHandle.None;
    }

    /// <summary>기존 호환: 꼬리 끝점 근처 클릭 감지</summary>
    public bool HitTestTail(Point point) => HitTestHandles(point) != TailHandle.None;

    private static double DistSq(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    public override Rect Bounds
    {
        get
        {
            var body = GetBodyRect();
            var (tip, left, right) = GetTailHandles();
            var minX = Math.Min(Math.Min(body.X, tip.X), Math.Min(left.X, right.X)) - ShadowOffset - 2;
            var minY = Math.Min(Math.Min(body.Y, tip.Y), Math.Min(left.Y, right.Y)) - 2;
            var maxX = Math.Max(Math.Max(body.Right, tip.X), Math.Max(left.X, right.X)) + ShadowOffset + 2;
            var maxY = Math.Max(Math.Max(body.Bottom, tip.Y), Math.Max(left.Y, right.Y)) + ShadowOffset + 2;
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }

    public override void Render(DrawingContext dc)
    {
        var body = GetBodyRect();
        double radius = BalloonStyle == BalloonStyle.Rounded ? 12.0 : 2.0;

        // 그림자 (본체 + 꼬다리)
        var shadowBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        shadowBrush.Freeze();
        var shadowBody = new Rect(body.X + ShadowOffset, body.Y + ShadowOffset, body.Width, body.Height);
        dc.DrawRoundedRectangle(shadowBrush, null, shadowBody, radius, radius);

        var shadowTailBody = new Rect(body.X + ShadowOffset, body.Y + ShadowOffset, body.Width, body.Height);
        var (shadowTailGeo, _) = BuildTailGeometries(shadowTailBody);
        dc.DrawGeometry(shadowBrush, null, shadowTailGeo);

        var fill = new SolidColorBrush(FillColor);
        fill.Freeze();

        // 꼬리와 본체를 채우기
        var (tailGeo, _2) = BuildTailGeometries(body);
        dc.DrawGeometry(fill, null, tailGeo);
        dc.DrawRoundedRectangle(fill, null, body, radius, radius);

        // 텍스트
        if (!string.IsNullOrEmpty(Text))
        {
            var ft = BuildFormattedText(Colors.Black);
            var textPos = new Point(
                body.X + PaddingH,
                body.Y + (body.Height - ft.Height) / 2.0);
            dc.DrawText(ft, textPos);
        }

        // 꼬리 핸들 (선택 시 3개)
        if (IsSelected)
        {
            var (tip, left, right) = GetTailHandles();

            var tipBrush = new SolidColorBrush(Color.FromArgb(200, 0, 120, 212));
            tipBrush.Freeze();
            var baseBrush = new SolidColorBrush(Color.FromArgb(200, 212, 80, 0));
            baseBrush.Freeze();
            var handlePen = new Pen(Brushes.White, 1.5);
            handlePen.Freeze();

            // 연결선 (핸들 간)
            var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(80, 0, 120, 212)), 1) { DashStyle = DashStyles.Dash };
            guidePen.Freeze();
            dc.DrawLine(guidePen, left, tip);
            dc.DrawLine(guidePen, right, tip);
            dc.DrawLine(guidePen, left, right);

            // 핸들 점
            dc.DrawEllipse(tipBrush, handlePen, tip, HandleRadius, HandleRadius);
            dc.DrawEllipse(baseBrush, handlePen, left, HandleRadius * 0.8, HandleRadius * 0.8);
            dc.DrawEllipse(baseBrush, handlePen, right, HandleRadius * 0.8, HandleRadius * 0.8);
        }
    }

    private (StreamGeometry fill, StreamGeometry outline) BuildTailGeometries(Rect body)
    {
        var (attachPoint, tailTip, tailDir) = ComputeTailAttachment(body);

        Vector perpendicular = tailDir == TailDirection.Vertical
            ? new Vector(1, 0) : new Vector(0, 1);
        var halfW = TailWidth / 2;
        var tailBase1 = attachPoint + perpendicular * halfW;
        var tailBase2 = attachPoint - perpendicular * halfW;

        var fillGeo = new StreamGeometry();
        using (var ctx = fillGeo.Open())
        {
            ctx.BeginFigure(tailBase1, true, true);
            ctx.LineTo(tailTip, true, true);
            ctx.LineTo(tailBase2, true, true);
        }
        fillGeo.Freeze();

        var outlineGeo = new StreamGeometry();
        using (var ctx = outlineGeo.Open())
        {
            ctx.BeginFigure(tailBase1, false, false);
            ctx.LineTo(tailTip, true, true);
            ctx.LineTo(tailBase2, true, true);
        }
        outlineGeo.Freeze();

        return (fillGeo, outlineGeo);
    }

    private enum TailDirection { Vertical, Horizontal }

    private (Point attach, Point tip, TailDirection dir) ComputeTailAttachment(Rect body)
    {
        var tx = TailTarget.X;
        var ty = TailTarget.Y;

        double distBottom = ty - body.Bottom;
        double distTop = body.Top - ty;
        double distRight = tx - body.Right;
        double distLeft = body.Left - tx;

        double maxDist = Math.Max(Math.Max(distBottom, distTop), Math.Max(distRight, distLeft));
        if (maxDist <= 0) maxDist = distBottom;

        // TailWidth가 본체보다 커지면 중앙 고정
        double xMin = body.X + TailWidth;
        double xMax = body.Right - TailWidth;
        if (xMin > xMax) xMin = xMax = (body.X + body.Right) / 2;
        double yMin = body.Y + TailWidth;
        double yMax = body.Bottom - TailWidth;
        if (yMin > yMax) yMin = yMax = (body.Y + body.Bottom) / 2;

        double clampedX = Math.Clamp(tx, xMin, xMax);
        double clampedY = Math.Clamp(ty, yMin, yMax);

        if (maxDist == distBottom)
            return (new Point(clampedX, body.Bottom), TailTarget, TailDirection.Vertical);
        if (maxDist == distTop)
            return (new Point(clampedX, body.Top), TailTarget, TailDirection.Vertical);
        if (maxDist == distRight)
            return (new Point(body.Right, clampedY), TailTarget, TailDirection.Horizontal);
        return (new Point(body.Left, clampedY), TailTarget, TailDirection.Horizontal);
    }

    public override bool HitTest(Point point)
    {
        return GetBodyRect().Contains(point);
    }

    public override void Scale(double factor, Point anchor)
    {
        Position = ScalePoint(Position, factor, anchor);
        TailTarget = ScalePoint(TailTarget, factor, anchor);
        FontSize = Math.Max(6, FontSize * factor);
        TailWidth = Math.Max(4, TailWidth * factor);
    }

    private static Point ScalePoint(Point p, double f, Point a) =>
        new(a.X + (p.X - a.X) * f, a.Y + (p.Y - a.Y) * f);

    public override void Move(Vector delta)
    {
        Position = new Point(Position.X + delta.X, Position.Y + delta.Y);
        TailTarget = new Point(TailTarget.X + delta.X, TailTarget.Y + delta.Y);
    }

    public override EditorObject Clone()
    {
        return new BalloonObject
        {
            Text = Text,
            Position = Position,
            TailTarget = TailTarget,
            TailWidth = TailWidth,
            FillColor = FillColor,
            BorderColor = BorderColor,
            BalloonStyle = BalloonStyle,
            FontName = FontName,
            FontSize = FontSize,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
