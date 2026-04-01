using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor;

public class CropCommand : IEditorCommand
{
    private readonly Action<BitmapSource> _setImage;
    private readonly BitmapSource _originalImage;
    private readonly BitmapSource _croppedImage;
    private readonly List<EditorObject> _objects;
    private readonly Vector _offset;

    public CropCommand(BitmapSource original, Int32Rect cropRect, Action<BitmapSource> setImage,
        List<EditorObject> objects)
    {
        _setImage = setImage;
        _originalImage = original;
        _croppedImage = new CroppedBitmap(original, cropRect);
        _croppedImage.Freeze();
        _objects = objects;
        _offset = new Vector(cropRect.X, cropRect.Y);
    }

    public void Execute()
    {
        _setImage(_croppedImage);
        foreach (var obj in _objects)
            obj.Move(-_offset);
    }

    public void Undo()
    {
        _setImage(_originalImage);
        foreach (var obj in _objects)
            obj.Move(_offset);
    }
}
