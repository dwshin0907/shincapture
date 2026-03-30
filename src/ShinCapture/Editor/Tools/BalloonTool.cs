using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class BalloonTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private BalloonObject? _current;
    private bool _isDrawing;

    public override string Name => "Balloon";
    public override string Icon => "💬";

    public BalloonStyle BalloonStyle { get; set; } = BalloonStyle.Rounded;
    public string DefaultText { get; set; } = "텍스트";

    public BalloonTool(List<EditorObject> objects)
    {
        _objects = objects;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _current = new BalloonObject
        {
            Position = position,
            TailTarget = position,
            FillColor = System.Windows.Media.Colors.White,
            BorderColor = CurrentColor,
            BalloonStyle = BalloonStyle,
            Text = DefaultText
        };
        _isDrawing = true;
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        if (!_isDrawing || _current == null) return;
        _current.TailTarget = position;
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        if (!_isDrawing || _current == null) return;
        _current.TailTarget = position;
        _isDrawing = false;
    }

    public override IEditorCommand? GetCommand()
    {
        if (_current == null) return null;
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
