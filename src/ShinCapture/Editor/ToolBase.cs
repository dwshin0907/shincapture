using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ShinCapture.Editor;

public abstract class ToolBase : ITool
{
    public abstract string Name { get; }
    public abstract string Icon { get; }
    public Color CurrentColor { get; set; } = Colors.Red;
    public double CurrentWidth { get; set; } = 3;
    public double CurrentOpacity { get; set; } = 1.0;

    public abstract void OnMouseDown(Point position, MouseButtonEventArgs e);
    public abstract void OnMouseMove(Point position, MouseEventArgs e);
    public abstract void OnMouseUp(Point position, MouseButtonEventArgs e);
    public abstract IEditorCommand? GetCommand();
    public abstract void RenderPreview(DrawingContext dc);
    public abstract void Reset();
}
