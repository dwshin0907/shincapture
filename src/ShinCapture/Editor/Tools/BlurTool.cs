using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class BlurTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private Point _start;
    private Point _end;
    private bool _isDrawing;
    private BlurObject? _preview;

    public override string Name => "Blur";
    public override string Icon => "🔲";

    public BlurStrength BlurStrength { get; set; } = BlurStrength.Medium;

    public BlurTool(List<EditorObject> objects)
    {
        _objects = objects;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _start = position;
        _end = position;
        _isDrawing = true;
        UpdatePreview();
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        if (!_isDrawing) return;
        _end = position;
        UpdatePreview();
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        if (!_isDrawing) return;
        _end = position;
        _isDrawing = false;
        UpdatePreview();
    }

    private Rect GetRegion()
    {
        double x = Math.Min(_start.X, _end.X);
        double y = Math.Min(_start.Y, _end.Y);
        double w = Math.Abs(_end.X - _start.X);
        double h = Math.Abs(_end.Y - _start.Y);
        return new Rect(x, y, w, h);
    }

    private void UpdatePreview()
    {
        var region = GetRegion();
        if (region.Width < 2 || region.Height < 2)
        {
            _preview = null;
            return;
        }
        _preview = new BlurObject
        {
            Region = region,
            BlurStrength = BlurStrength
        };
    }

    public override IEditorCommand? GetCommand()
    {
        if (_preview == null) return null;
        var cmd = new AddObjectCommand(_objects, _preview);
        _preview = null;
        return cmd;
    }

    public override void RenderPreview(DrawingContext dc)
    {
        if (_preview != null)
        {
            _preview.Render(dc);
        }
        else if (_isDrawing)
        {
            var region = GetRegion();
            if (region.Width > 0 && region.Height > 0)
            {
                var pen = new Pen(new SolidColorBrush(Colors.Gray), 1) { DashStyle = DashStyles.Dash };
                pen.Freeze();
                dc.DrawRectangle(null, pen, region);
            }
        }
    }

    public override void Reset()
    {
        _isDrawing = false;
        _preview = null;
    }
}
