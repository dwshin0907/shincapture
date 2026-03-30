using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor.Tools;

public class EyedropperTool : ToolBase
{
    private BitmapSource? _sourceImage;

    public override string Name => "Eyedropper";
    public override string Icon => "💉";

    public event Action<Color>? ColorPicked;

    public EyedropperTool(BitmapSource? sourceImage = null)
    {
        _sourceImage = sourceImage;
    }

    public void SetSourceImage(BitmapSource? source) => _sourceImage = source;

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        if (_sourceImage == null) return;

        int px = (int)Math.Clamp(position.X, 0, _sourceImage.PixelWidth - 1);
        int py = (int)Math.Clamp(position.Y, 0, _sourceImage.PixelHeight - 1);

        int stride = _sourceImage.PixelWidth * 4;
        byte[] pixels = new byte[_sourceImage.PixelHeight * stride];
        _sourceImage.CopyPixels(pixels, stride, 0);

        int idx = py * stride + px * 4;
        byte b = pixels[idx];
        byte g = pixels[idx + 1];
        byte r = pixels[idx + 2];
        byte a = pixels[idx + 3];

        var color = Color.FromArgb(a, r, g, b);
        CurrentColor = color;
        ColorPicked?.Invoke(color);
    }

    public override void OnMouseMove(Point position, MouseEventArgs e) { }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e) { }

    public override IEditorCommand? GetCommand() => null;

    public override void RenderPreview(DrawingContext dc) { }

    public override void Reset() { }
}
