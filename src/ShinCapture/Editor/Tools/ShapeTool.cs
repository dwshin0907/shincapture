using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class ShapeTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private ShapeObject? _current;
    private bool _isDrawing;

    public override string Name => "Shape";
    public override string Icon => "⬜";

    public ShapeType SelectedShape { get; set; } = ShapeType.Rectangle;
    public FillMode SelectedFill { get; set; } = FillMode.None;

    public ShapeTool(List<EditorObject> objects)
    {
        _objects = objects;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _current = new ShapeObject
        {
            Start = position,
            End = position,
            ShapeType = SelectedShape,
            StrokeColor = CurrentColor,
            StrokeWidth = CurrentWidth,
            FillMode = SelectedFill
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
        var bounds = _current.Bounds;
        if (bounds.Width < 2 && bounds.Height < 2) return null;
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
