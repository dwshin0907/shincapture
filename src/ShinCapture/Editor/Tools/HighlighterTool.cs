using System;
using System.Collections.Generic;
using System.Linq;
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

    public string GradientMode { get; set; } = "없음"; // 없음, 무지개, 모노, 가을, 여름, 바다, 파스텔, 네온

    private static readonly Dictionary<string, Color[]> GradientPalettes = new()
    {
        ["무지개"] = new[] { Colors.Red, Colors.Orange, Colors.Yellow, Colors.LimeGreen, Colors.DodgerBlue, Colors.BlueViolet, Colors.DeepPink },
        ["모노"] = new[] { Color.FromRgb(30, 30, 30), Color.FromRgb(80, 80, 80), Color.FromRgb(140, 140, 140), Color.FromRgb(200, 200, 200), Color.FromRgb(100, 100, 100) },
        ["가을"] = new[] { Color.FromRgb(139, 69, 19), Color.FromRgb(205, 92, 0), Color.FromRgb(218, 165, 32), Color.FromRgb(160, 82, 45), Color.FromRgb(178, 34, 34) },
        ["여름"] = new[] { Color.FromRgb(34, 139, 34), Color.FromRgb(0, 128, 0), Color.FromRgb(107, 142, 35), Color.FromRgb(85, 170, 85), Color.FromRgb(60, 179, 113) },
        ["바다"] = new[] { Color.FromRgb(0, 105, 148), Color.FromRgb(0, 150, 199), Color.FromRgb(72, 202, 228), Color.FromRgb(0, 180, 216), Color.FromRgb(144, 224, 239) },
        ["파스텔"] = new[] { Color.FromRgb(255, 179, 186), Color.FromRgb(255, 223, 186), Color.FromRgb(255, 255, 186), Color.FromRgb(186, 255, 201), Color.FromRgb(186, 225, 255) },
        ["네온"] = new[] { Color.FromRgb(255, 0, 102), Color.FromRgb(255, 230, 0), Color.FromRgb(0, 255, 102), Color.FromRgb(0, 200, 255), Color.FromRgb(180, 0, 255) },
    };

    public override string Name => "Highlighter";
    public override string Icon => "🖊️";

    public HighlighterTool(List<EditorObject> objects)
    {
        _objects = objects;
        CurrentColor = Colors.Black;
        CurrentWidth = 25;
        CurrentOpacity = 0.5;
    }

    private List<Color>? MakeGradient()
    {
        if (GradientMode == "없음" || !GradientPalettes.TryGetValue(GradientMode, out var palette))
            return null;
        var rng = new Random();
        var picked = palette.OrderBy(_ => rng.Next()).Take(Math.Min(4, palette.Length)).ToList();
        picked.Add(picked[0]); // 루프
        return picked;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _current = new StrokeObject
        {
            StrokeColor = CurrentColor,
            StrokeWidth = CurrentWidth,
            Opacity = CurrentOpacity,
            IsHighlighter = true,
            GradientColors = MakeGradient()
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
