using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor;

public class CropCommand : IEditorCommand
{
    private readonly Action<BitmapSource> _setImage;
    private readonly BitmapSource _originalImage;
    private readonly BitmapSource _croppedImage;

    public CropCommand(BitmapSource original, Int32Rect cropRect, Action<BitmapSource> setImage)
    {
        _setImage = setImage;
        _originalImage = original;
        _croppedImage = new CroppedBitmap(original, cropRect);
        _croppedImage.Freeze();
    }

    public void Execute() => _setImage(_croppedImage);
    public void Undo() => _setImage(_originalImage);
}
