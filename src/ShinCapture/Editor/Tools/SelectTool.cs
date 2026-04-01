using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class SelectTool : ToolBase
{
    private readonly List<EditorObject> _objects;

    private EditorObject? _selected;
    private DragMode _dragMode = DragMode.None;
    private Point _dragStart;
    private Rect _originalBounds;
    private double _rotateStartAngle;

    private const double HandleSize = 8;
    private const double HandleHit = 16;
    private const double RotateHandleOffset = 28;

    private enum DragMode { None, Move, ResizeTL, ResizeTR, ResizeBL, ResizeBR, Rotate, BalloonTail }

    public override string Name => "Select";
    public override string Icon => "↖";

    private Cursor? _cursor;
    public override Cursor? RequestedCursor => _cursor;

    public SelectTool(List<EditorObject> objects)
    {
        _objects = objects;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _dragMode = DragMode.None;

        // 1. 선택된 개체의 핸들 체크 (회전 고려: 클릭 좌표를 로컬 좌표로 변환)
        if (_selected != null)
        {
            var localPos = TransformToLocal(_selected, position);

            // 회전 핸들
            if (IsNearRotateHandle(_selected, localPos))
            {
                _dragMode = DragMode.Rotate;
                _dragStart = position;
                var center = _selected.Bounds.Center();
                _rotateStartAngle = Math.Atan2(position.Y - center.Y, position.X - center.X) * 180.0 / Math.PI;
                return;
            }

            // 리사이즈 핸들
            var handle = HitTestHandle(_selected, localPos);
            if (handle != DragMode.None)
            {
                _dragMode = handle;
                _dragStart = position;
                _originalBounds = _selected.Bounds;
                return;
            }

            // 말풍선 꼬리 체크
            if (_selected is BalloonObject balloon && balloon.HitTestTail(localPos))
            {
                _dragMode = DragMode.BalloonTail;
                _dragStart = position;
                return;
            }
        }

        // 2. 개체 히트 테스트
        EditorObject? hit = null;
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i].IsVisible && _objects[i].HitTestWithTransform(position))
            {
                hit = _objects[i];
                break;
            }
        }

        // 선택 업데이트
        foreach (var obj in _objects) obj.IsSelected = false;
        _selected = hit;
        if (_selected != null)
        {
            _selected.IsSelected = true;
            _dragMode = DragMode.Move;
            _dragStart = position;
        }
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        if (_dragMode == DragMode.None)
        {
            UpdateHoverCursor(position);
            return;
        }

        // 드래그 중 커서 유지
        _cursor = _dragMode switch
        {
            DragMode.Move => Cursors.SizeAll,
            DragMode.Rotate => Cursors.Hand,
            DragMode.ResizeTL or DragMode.ResizeBR => Cursors.SizeNWSE,
            DragMode.ResizeTR or DragMode.ResizeBL => Cursors.SizeNESW,
            DragMode.BalloonTail => Cursors.Hand,
            _ => null
        };

        switch (_dragMode)
        {
            case DragMode.Move:
                var delta = position - _dragStart;
                if (delta.Length > 0.5)
                {
                    _selected!.Move(delta);
                    _dragStart = position;
                }
                break;

            case DragMode.Rotate:
                var center = _selected!.Bounds.Center();
                double currentAngle = Math.Atan2(position.Y - center.Y, position.X - center.X) * 180.0 / Math.PI;
                double angleDelta = currentAngle - _rotateStartAngle;
                _selected.Rotation += angleDelta;
                _rotateStartAngle = currentAngle;
                break;

            case DragMode.BalloonTail:
                if (_selected is BalloonObject balloon)
                    balloon.TailTarget = position;
                break;

            case DragMode.ResizeTL:
            case DragMode.ResizeTR:
            case DragMode.ResizeBL:
            case DragMode.ResizeBR:
                ApplyResize(position);
                break;
        }
    }

    private void UpdateHoverCursor(Point position)
    {
        // 선택된 개체의 핸들 위 호버
        if (_selected != null)
        {
            var localPos = TransformToLocal(_selected, position);

            if (IsNearRotateHandle(_selected, localPos))
            {
                _cursor = Cursors.Hand;
                return;
            }

            var handle = HitTestHandle(_selected, localPos);
            if (handle != DragMode.None)
            {
                _cursor = handle switch
                {
                    DragMode.ResizeTL or DragMode.ResizeBR => Cursors.SizeNWSE,
                    DragMode.ResizeTR or DragMode.ResizeBL => Cursors.SizeNESW,
                    _ => Cursors.SizeAll
                };
                return;
            }

            if (_selected is BalloonObject bal && bal.HitTestTail(localPos))
            {
                _cursor = Cursors.Hand;
                return;
            }
        }

        // 개체 본체 위 호버
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i].IsVisible && _objects[i].HitTestWithTransform(position))
            {
                _cursor = _objects[i] == _selected ? Cursors.SizeAll : Cursors.Hand;
                return;
            }
        }

        _cursor = null;
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        _dragMode = DragMode.None;
    }

    private void ApplyResize(Point currentPos)
    {
        if (_selected == null) return;

        var bounds = _originalBounds;
        var anchor = _dragMode switch
        {
            DragMode.ResizeTL => new Point(bounds.Right, bounds.Bottom),
            DragMode.ResizeTR => new Point(bounds.Left, bounds.Bottom),
            DragMode.ResizeBL => new Point(bounds.Right, bounds.Top),
            DragMode.ResizeBR => new Point(bounds.Left, bounds.Top),
            _ => bounds.Center()
        };

        // 회전된 개체의 경우 드래그 좌표를 로컬 좌표로 변환하여 거리 계산
        var localStart = TransformToLocal(_selected, _dragStart);
        var localCurrent = TransformToLocal(_selected, currentPos);

        double distOrig = Math.Max(10, Distance(localStart, anchor));
        double distCurr = Math.Max(10, Distance(localCurrent, anchor));
        double factor = distCurr / distOrig;
        factor = Math.Clamp(factor, 0.2, 5.0);

        _selected.Scale(factor, anchor);
        _dragStart = currentPos;
        _originalBounds = _selected.Bounds;
    }

    /// <summary>회전된 개체의 로컬(비회전) 좌표계로 포인트 변환</summary>
    private static Point TransformToLocal(EditorObject obj, Point point)
    {
        if (Math.Abs(obj.Rotation) < 0.01) return point;
        var center = obj.Bounds.Center();
        var rad = -obj.Rotation * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return new Point(center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos);
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private DragMode HitTestHandle(EditorObject obj, Point pos)
    {
        var b = obj.Bounds;
        if (IsNear(pos, new Point(b.Left, b.Top))) return DragMode.ResizeTL;
        if (IsNear(pos, new Point(b.Right, b.Top))) return DragMode.ResizeTR;
        if (IsNear(pos, new Point(b.Left, b.Bottom))) return DragMode.ResizeBL;
        if (IsNear(pos, new Point(b.Right, b.Bottom))) return DragMode.ResizeBR;
        return DragMode.None;
    }

    private bool IsNearRotateHandle(EditorObject obj, Point pos)
    {
        var b = obj.Bounds;
        var handlePos = new Point(b.X + b.Width / 2, b.Top - RotateHandleOffset);
        return IsNear(pos, handlePos);
    }

    private static bool IsNear(Point a, Point b) =>
        Math.Abs(a.X - b.X) <= HandleHit && Math.Abs(a.Y - b.Y) <= HandleHit;

    public override IEditorCommand? GetCommand() => null;

    public override void RenderPreview(DrawingContext dc)
    {
        if (_selected == null || !_selected.IsVisible) return;

        var bounds = _selected.Bounds;
        var center = bounds.Center();

        // 회전 적용
        if (Math.Abs(_selected.Rotation) > 0.01)
            dc.PushTransform(new RotateTransform(_selected.Rotation, center.X, center.Y));

        // 선택 테두리
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 0, 120, 212)), 1.2)
        {
            DashStyle = DashStyles.Dash
        };
        borderPen.Freeze();
        dc.DrawRectangle(null, borderPen, new Rect(
            bounds.X - 2, bounds.Y - 2,
            bounds.Width + 4, bounds.Height + 4));

        // 리사이즈 핸들 (4코너)
        var handleBrush = new SolidColorBrush(Colors.White);
        handleBrush.Freeze();
        var handlePen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 120, 212)), 1.5);
        handlePen.Freeze();

        DrawHandle(dc, handleBrush, handlePen, bounds.Left, bounds.Top);
        DrawHandle(dc, handleBrush, handlePen, bounds.Right, bounds.Top);
        DrawHandle(dc, handleBrush, handlePen, bounds.Left, bounds.Bottom);
        DrawHandle(dc, handleBrush, handlePen, bounds.Right, bounds.Bottom);

        // 회전 핸들 (상단 중앙 위)
        var rotateCenter = new Point(bounds.X + bounds.Width / 2, bounds.Top - RotateHandleOffset);
        var linePen = new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 120, 212)), 1);
        linePen.Freeze();
        dc.DrawLine(linePen, new Point(bounds.X + bounds.Width / 2, bounds.Top), rotateCenter);

        var rotateBrush = new SolidColorBrush(Color.FromArgb(200, 0, 180, 100));
        rotateBrush.Freeze();
        dc.DrawEllipse(rotateBrush, handlePen, rotateCenter, 7, 7);

        // 말풍선 꼬리 핸들
        if (_selected is BalloonObject balloon)
        {
            var (tip, left, right) = balloon.GetTailHandles();
            var tailBrush = new SolidColorBrush(Color.FromArgb(200, 255, 140, 0));
            tailBrush.Freeze();
            dc.DrawEllipse(tailBrush, handlePen, tip, 6, 6);
        }

        if (Math.Abs(_selected.Rotation) > 0.01)
            dc.Pop();
    }

    private static void DrawHandle(DrawingContext dc, Brush fill, Pen pen, double x, double y)
    {
        dc.DrawRectangle(fill, pen, new Rect(
            x - HandleSize / 2, y - HandleSize / 2, HandleSize, HandleSize));
    }

    public override void Reset()
    {
        _selected = null;
        _dragMode = DragMode.None;
        foreach (var obj in _objects) obj.IsSelected = false;
    }
}
