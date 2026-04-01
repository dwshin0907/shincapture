using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ShinCapture.Editor;

public abstract class ToolBase : ITool
{
    public abstract string Name { get; }
    public abstract string Icon { get; }
    public virtual Cursor? RequestedCursor => null;
    public virtual Color CurrentColor { get; set; } = Colors.Black;
    public virtual double CurrentWidth { get; set; } = 3;
    public double CurrentOpacity { get; set; } = 1.0;
    public virtual string CurrentFontName { get; set; } = "Paperlogy 5";
    public virtual double CurrentFontSize { get; set; } = 40;
    public virtual bool GlassBackground { get; set; } = false;
    public virtual bool Bold { get; set; } = false;
    public virtual Color? TextFillColor { get; set; }
    public virtual Color? TextBorderColor { get; set; }

    public abstract void OnMouseDown(Point position, MouseButtonEventArgs e);
    public abstract void OnMouseMove(Point position, MouseEventArgs e);
    public abstract void OnMouseUp(Point position, MouseButtonEventArgs e);
    public abstract IEditorCommand? GetCommand();
    public abstract void RenderPreview(DrawingContext dc);
    public abstract void Reset();
}
