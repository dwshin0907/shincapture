using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ShinCapture.Editor.Tools;

public class EraserTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private EditorObject? _hitObject;
    private Point _lastPosition;

    public override string Name => "Eraser";
    public override string Icon => "🧹";

    public EraserTool(List<EditorObject> objects)
    {
        _objects = objects;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _lastPosition = position;
        // Find topmost hit object (search in reverse order)
        _hitObject = null;
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i].HitTest(position))
            {
                _hitObject = _objects[i];
                break;
            }
        }
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        _lastPosition = position;
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        _lastPosition = position;
    }

    public override IEditorCommand? GetCommand()
    {
        if (_hitObject == null) return null;
        var cmd = new RemoveObjectCommand(_objects, _hitObject);
        _hitObject = null;
        return cmd;
    }

    public override void RenderPreview(DrawingContext dc)
    {
        // Draw eraser cursor circle
        var pen = new Pen(new SolidColorBrush(Colors.Gray), 1);
        pen.Freeze();
        dc.DrawEllipse(null, pen, _lastPosition, 8, 8);
    }

    public override void Reset()
    {
        _hitObject = null;
    }
}
