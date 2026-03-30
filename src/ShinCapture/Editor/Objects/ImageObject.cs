using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor.Objects;

public class ImageObject : EditorObject
{
    public BitmapSource? Source { get; set; }
    public Point Position { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public override Rect Bounds =>
        new Rect(Position.X, Position.Y, Width, Height);

    public override void Render(DrawingContext dc)
    {
        if (Source == null) return;
        dc.DrawImage(Source, Bounds);
    }

    public override bool HitTest(Point point)
    {
        return Bounds.Contains(point);
    }

    public override EditorObject Clone()
    {
        return new ImageObject
        {
            Source = Source,
            Position = Position,
            Width = Width,
            Height = Height,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
