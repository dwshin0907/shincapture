using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class ArrowTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private ArrowObject? _current;
    private bool _isDrawing;

    public override string Name => "Arrow";
    public override string Icon => "➡️";

    public ArrowHeadStyle SelectedHeadStyle { get; set; } = ArrowHeadStyle.Arrow;
    public ArrowLineStyle SelectedLineStyle { get; set; } = ArrowLineStyle.Solid;

    public ArrowTool(List<EditorObject> objects)
    {
        _objects = objects;
        CurrentColor = Colors.Black;
        CurrentWidth = 9.9;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _current = new ArrowObject
        {
            Start = position,
            End = position,
            Color = CurrentColor,
            StrokeWidth = CurrentWidth,
            HeadSize = Math.Max(6, CurrentWidth * 2.8),
            HeadStyle = SelectedHeadStyle,
            LineStyle = SelectedLineStyle
        };
        _isDrawing = true;
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        if (!_isDrawing || _current == null) return;
        _current.End = position;
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        if (!_isDrawing || _current == null) return;
        _current.End = position;
        _isDrawing = false;
    }

    public override IEditorCommand? GetCommand()
    {
        if (_current == null) return null;
        var dx = _current.End.X - _current.Start.X;
        var dy = _current.End.Y - _current.Start.Y;
        if (dx * dx + dy * dy < 4) return null;
        var cmd = new AddObjectCommand(_objects, _current);
        _current = null;
        return cmd;
    }

    public override void RenderPreview(DrawingContext dc)
    {
        _current?.Render(dc);
    }

    public override void Reset()
    {
        _current = null;
        _isDrawing = false;
    }
}
