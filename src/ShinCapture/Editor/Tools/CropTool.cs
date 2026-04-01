using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor.Tools;

public class CropTool : ToolBase
{
    private BitmapSource? _sourceImage;
    private readonly Action<BitmapSource> _setImage;
    private readonly List<EditorObject> _objects;
    private Point _start;
    private Point _end;
    private bool _isDrawing;

    public override string Name => "Crop";
    public override string Icon => "✂️";

    public CropTool(BitmapSource? sourceImage, Action<BitmapSource> setImage, List<EditorObject> objects)
    {
        _sourceImage = sourceImage;
        _setImage = setImage;
        _objects = objects;
    }

    public void SetSourceImage(BitmapSource? source) => _sourceImage = source;

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        _start = position;
        _end = position;
        _isDrawing = true;
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        if (!_isDrawing) return;
        _end = position;
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        if (!_isDrawing) return;
        _end = position;
        _isDrawing = false;
    }

    private Rect GetRegion()
    {
        double x = Math.Min(_start.X, _end.X);
        double y = Math.Min(_start.Y, _end.Y);
        double w = Math.Abs(_end.X - _start.X);
        double h = Math.Abs(_end.Y - _start.Y);
        return new Rect(x, y, w, h);
    }

    public override IEditorCommand? GetCommand()
    {
        if (_sourceImage == null) return null;
        var region = GetRegion();
        if (region.Width < 2 || region.Height < 2) return null;

        int imgW = _sourceImage.PixelWidth;
        int imgH = _sourceImage.PixelHeight;

        int x = (int)Math.Max(0, region.X);
        int y = (int)Math.Max(0, region.Y);
        int w = (int)Math.Min(region.Width, imgW - x);
        int h = (int)Math.Min(region.Height, imgH - y);

        if (w <= 0 || h <= 0) return null;

        var cropRect = new Int32Rect(x, y, w, h);
        return new CropCommand(_sourceImage, cropRect, _setImage, _objects);
    }

    public override void RenderPreview(DrawingContext dc)
    {
        var region = GetRegion();
        if (region.Width < 2 || region.Height < 2) return;

        // Dim overlay outside selection
        var dimBrush = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
        dimBrush.Freeze();

        var selPen = new Pen(new SolidColorBrush(Colors.White), 1.5) { DashStyle = DashStyles.Dash };
        selPen.Freeze();

        dc.DrawRectangle(null, selPen, region);
    }

    public override void Reset()
    {
        _isDrawing = false;
    }
}
