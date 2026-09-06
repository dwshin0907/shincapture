using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor;

public enum ImageTransformKind
{
    RotateClockwise,
    RotateCounterclockwise,
    Rotate180,
    FlipHorizontal,
    FlipVertical
}

/// <summary>Transforms the visible composition; undo restores the original editable objects.</summary>
public sealed class ImageTransformCommand : IEditorCommand
{
    private readonly BitmapSource _original;
    private readonly BitmapSource _transformed;
    private readonly List<EditorObject> _objects;
    private readonly EditorObject[] _originalObjects;
    private readonly Action<BitmapSource> _setImage;

    public ImageTransformCommand(BitmapSource source, List<EditorObject> objects,
        ImageTransformKind kind, Action<BitmapSource> setImage)
    {
        _original = source;
        _objects = objects;
        _originalObjects = objects.ToArray();
        _setImage = setImage;
        Transform transform = kind switch
        {
            ImageTransformKind.RotateClockwise => new RotateTransform(90),
            ImageTransformKind.RotateCounterclockwise => new RotateTransform(-90),
            ImageTransformKind.Rotate180 => new RotateTransform(180),
            ImageTransformKind.FlipHorizontal => new ScaleTransform(-1, 1),
            ImageTransformKind.FlipVertical => new ScaleTransform(1, -1),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var composite = objects.Count == 0 ? source : EditorCompositeRenderer.RenderBitmapSource(source, objects);
        _transformed = new TransformedBitmap(composite, transform);
        _transformed.Freeze();
    }

    public void Execute()
    {
        _objects.Clear();
        _setImage(_transformed);
    }

    public void Undo()
    {
        _objects.Clear();
        _objects.AddRange(_originalObjects);
        _setImage(_original);
    }
}
