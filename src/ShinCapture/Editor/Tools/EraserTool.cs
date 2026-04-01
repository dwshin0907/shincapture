using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ShinCapture.Editor.Tools;

public enum EraserMode
{
    Object,  // 클릭한 개체 삭제
    Area     // 드래그 영역 안의 개체 모두 삭제
}

public class EraserTool : ToolBase
{
    private readonly List<EditorObject> _objects;

    // 개체 모드
    private EditorObject? _hitObject;
    private readonly List<EditorObject> _swipeHits = new();
    private bool _isSwiping;

    // 영역 모드
    private Point _areaStart;
    private Point _areaEnd;
    private bool _isDrawingArea;

    private Point _lastPosition;

    public EraserMode Mode { get; set; } = EraserMode.Object;

    public override string Name => "Eraser";
    public override string Icon => "🧹";

    private Cursor? _cursor;
    public override Cursor? RequestedCursor => _cursor;

    public EraserTool(List<EditorObject> objects)
    {
        _objects = objects;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _lastPosition = position;

        if (Mode == EraserMode.Area)
        {
            _areaStart = position;
            _areaEnd = position;
            _isDrawingArea = true;
            return;
        }

        // 개체 모드: 클릭 + 스와이프 지우기
        _hitObject = null;
        _swipeHits.Clear();
        _isSwiping = true;

        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i].IsVisible && _objects[i].HitTestWithTransform(position))
            {
                _hitObject = _objects[i];
                _swipeHits.Add(_objects[i]);
                break;
            }
        }
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        _lastPosition = position;

        if (Mode == EraserMode.Area && _isDrawingArea)
        {
            _areaEnd = position;
            return;
        }

        // 개체 모드: 드래그 중 스치는 개체도 수집
        if (_isSwiping)
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i].IsVisible && !_swipeHits.Contains(_objects[i])
                    && _objects[i].HitTestWithTransform(position))
                {
                    _swipeHits.Add(_objects[i]);
                }
            }
        }

        // 커서 업데이트
        UpdateCursor(position);
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        _lastPosition = position;
        _isSwiping = false;

        if (Mode == EraserMode.Area)
        {
            _isDrawingArea = false;
            _areaEnd = position;
        }
    }

    public override IEditorCommand? GetCommand()
    {
        if (Mode == EraserMode.Area)
        {
            var rect = GetAreaRect();
            if (rect.Width < 4 && rect.Height < 4) return null;

            var targets = _objects.Where(o => o.IsVisible && rect.IntersectsWith(o.Bounds)).ToList();
            if (targets.Count == 0) return null;
            return new RemoveMultipleCommand(_objects, targets);
        }

        // 개체 모드
        if (_swipeHits.Count > 1)
        {
            var cmd = new RemoveMultipleCommand(_objects, _swipeHits);
            _swipeHits.Clear();
            _hitObject = null;
            return cmd;
        }
        if (_hitObject == null) return null;
        var singleCmd = new RemoveObjectCommand(_objects, _hitObject);
        _hitObject = null;
        _swipeHits.Clear();
        return singleCmd;
    }

    public override void RenderPreview(DrawingContext dc)
    {
        if (Mode == EraserMode.Area && _isDrawingArea)
        {
            var rect = GetAreaRect();
            // 빨간 점선 영역
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(180, 220, 50, 50)), 1.5)
            {
                DashStyle = DashStyles.Dash
            };
            pen.Freeze();
            var fill = new SolidColorBrush(Color.FromArgb(30, 220, 50, 50));
            fill.Freeze();
            dc.DrawRectangle(fill, pen, rect);

            // 영역 안 개체 하이라이트
            var hitPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 60, 60)), 2);
            hitPen.Freeze();
            foreach (var obj in _objects.Where(o => o.IsVisible && rect.IntersectsWith(o.Bounds)))
            {
                dc.DrawRectangle(null, hitPen, obj.Bounds);
            }
            return;
        }

        // 개체 모드: 지우개 커서 원
        var cursorPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 120, 120, 120)), 1.2);
        cursorPen.Freeze();
        dc.DrawEllipse(null, cursorPen, _lastPosition, 10, 10);

        // 스와이프 중 수집된 개체 하이라이트
        if (_isSwiping && _swipeHits.Count > 0)
        {
            var highlightPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 60, 60)), 2)
            {
                DashStyle = DashStyles.Dash
            };
            highlightPen.Freeze();
            foreach (var obj in _swipeHits)
                dc.DrawRectangle(null, highlightPen, obj.Bounds);
        }
    }

    private Rect GetAreaRect()
    {
        double x = Math.Min(_areaStart.X, _areaEnd.X);
        double y = Math.Min(_areaStart.Y, _areaEnd.Y);
        double w = Math.Abs(_areaEnd.X - _areaStart.X);
        double h = Math.Abs(_areaEnd.Y - _areaStart.Y);
        return new Rect(x, y, w, h);
    }

    private void UpdateCursor(Point position)
    {
        if (Mode == EraserMode.Area)
        {
            _cursor = _isDrawingArea ? Cursors.Cross : Cursors.Cross;
            return;
        }
        // 개체 위에 있으면 손가락 커서
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i].IsVisible && _objects[i].HitTestWithTransform(position))
            {
                _cursor = Cursors.Hand;
                return;
            }
        }
        _cursor = null;
    }

    public override void Reset()
    {
        _hitObject = null;
        _swipeHits.Clear();
        _isSwiping = false;
        _isDrawingArea = false;
    }
}
