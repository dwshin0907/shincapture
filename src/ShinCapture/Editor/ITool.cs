using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ShinCapture.Editor;

public interface ITool
{
    string Name { get; }
    string Icon { get; }
    Color CurrentColor { get; set; }
    double CurrentWidth { get; set; }
    double CurrentOpacity { get; set; }
    string CurrentFontName { get; set; }
    double CurrentFontSize { get; set; }
    bool GlassBackground { get; set; }
    bool Bold { get; set; }
    Color? TextFillColor { get; set; }
    Color? TextBorderColor { get; set; }
    Cursor? RequestedCursor { get; }
    void OnMouseDown(Point position, MouseButtonEventArgs e);
    void OnMouseMove(Point position, MouseEventArgs e);
    void OnMouseUp(Point position, MouseButtonEventArgs e);
    IEditorCommand? GetCommand();
    void RenderPreview(DrawingContext dc);
    void Reset();
}
