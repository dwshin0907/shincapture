using System;
using System.Windows;
using System.Windows.Media;

namespace ShinCapture.Editor;

public abstract class EditorObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public bool IsSelected { get; set; }
    public bool IsVisible { get; set; } = true;

    public abstract Rect Bounds { get; }
    public abstract void Render(DrawingContext dc);
    public abstract bool HitTest(Point point);
    public abstract EditorObject Clone();
}
