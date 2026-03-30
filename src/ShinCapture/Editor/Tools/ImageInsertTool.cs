using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class ImageInsertTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private readonly Canvas _canvas;
    private ImageObject? _pending;

    public override string Name => "ImageInsert";
    public override string Icon => "🖼️";

    public ImageInsertTool(List<EditorObject> objects, Canvas canvas)
    {
        _objects = objects;
        _canvas = canvas;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "이미지 파일 선택",
            Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.tif;*.webp|모든 파일|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new System.Uri(dialog.FileName);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            _pending = new ImageObject
            {
                Source = bitmap,
                Position = position,
                Width = bitmap.PixelWidth,
                Height = bitmap.PixelHeight
            };
        }
        catch
        {
            _pending = null;
        }
    }

    public override void OnMouseMove(Point position, MouseEventArgs e) { }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e) { }

    public override IEditorCommand? GetCommand()
    {
        if (_pending == null) return null;
        var cmd = new AddObjectCommand(_objects, _pending);
        _pending = null;
        return cmd;
    }

    public override void RenderPreview(DrawingContext dc)
    {
        _pending?.Render(dc);
    }

    public override void Reset()
    {
        _pending = null;
    }
}
