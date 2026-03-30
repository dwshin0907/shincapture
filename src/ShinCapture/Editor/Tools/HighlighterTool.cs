using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class HighlighterTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private StrokeObject? _current;
    private bool _isDrawing;

    public override string Name => "Highlighter";
    public override string Icon => "🖊️";

    public HighlighterTool(List<EditorObject> objects)
    {
        _objects = objects;
        CurrentColor = Colors.Yellow;
        CurrentWidth = 20;
        CurrentOpacity = 0.5;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _current = new StrokeObject
        {
            StrokeColor = CurrentColor,
            StrokeWidth = CurrentWidth,
            Opacity = CurrentOpacity,
            IsHighlighter = true
        };
        _current.Points.Add(position);
        _isDrawing = true;
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        if (!_isDrawing || _current == null) return;
        _current.Points.Add(position);
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        if (!_isDrawing || _current == null) return;
        _current.Points.Add(position);
        _isDrawing = false;
    }

    public override IEditorCommand? GetCommand()
    {
        if (_current == null || _current.Points.Count < 2) return null;
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
