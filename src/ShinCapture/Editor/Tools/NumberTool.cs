using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class NumberTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private NumberObject? _pending;

    public override string Name => "Number";
    public override string Icon => "①";

    public int Counter { get; set; } = 1;

    public NumberTool(List<EditorObject> objects)
    {
        _objects = objects;
        CurrentColor = Colors.Black;
        CurrentWidth = 22.4;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _pending = new NumberObject
        {
            Center = position,
            Number = Counter,
            CircleColor = CurrentColor,
            Radius = 8 + CurrentWidth * 1.2
        };
    }

    public override void OnMouseMove(Point position, MouseEventArgs e) { }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e) { }

    public override IEditorCommand? GetCommand()
    {
        if (_pending == null) return null;
        var cmd = new AddObjectCommand(_objects, _pending);
        Counter++;
        _pending = null;
        return cmd;
    }

    public override void RenderPreview(DrawingContext dc)
    {
        _pending?.Render(dc);
    }

    public override void Reset()
    {
        _pending = null;
    }
}
