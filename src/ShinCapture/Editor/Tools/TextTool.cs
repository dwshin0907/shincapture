using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class TextTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private readonly Canvas _canvas;
    private TextBox? _textBox;
    private Point _position;
    private IEditorCommand? _pendingCommand;

    public override string Name => "Text";
    public override string Icon => "T";

    public TextTool(List<EditorObject> objects, Canvas canvas)
    {
        _objects = objects;
        _canvas = canvas;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        // Finalize any existing text box first
        FinalizeTextBox();

        _position = position;

        _textBox = new TextBox
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Foreground = new SolidColorBrush(CurrentColor),
            BorderBrush = new SolidColorBrush(Colors.DodgerBlue),
            BorderThickness = new Thickness(1),
            FontSize = 16,
            MinWidth = 80,
            MinHeight = 24,
            AcceptsReturn = true,
            AcceptsTab = false
        };

        Canvas.SetLeft(_textBox, position.X);
        Canvas.SetTop(_textBox, position.Y);
        _canvas.Children.Add(_textBox);
        _textBox.Focus();

        _textBox.LostFocus += (_, _) => FinalizeTextBox();
        _textBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Escape) CancelTextBox();
        };
    }

    public override void OnMouseMove(Point position, MouseEventArgs e) { }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e) { }

    public override IEditorCommand? GetCommand()
    {
        // Command is set via TakePendingCommand after finalize
        return null;
    }

    public IEditorCommand? TakePendingCommand()
    {
        var cmd = _pendingCommand;
        _pendingCommand = null;
        return cmd;
    }

    private void FinalizeTextBox()
    {
        if (_textBox == null) return;

        var text = _textBox.Text?.Trim();
        _canvas.Children.Remove(_textBox);
        _textBox = null;

        if (!string.IsNullOrEmpty(text))
        {
            var obj = new TextObject
            {
                Text = text,
                Position = _position,
                TextColor = CurrentColor,
                FontSize = 16
            };
            _pendingCommand = new AddObjectCommand(_objects, obj);
        }
    }

    private void CancelTextBox()
    {
        if (_textBox == null) return;
        _canvas.Children.Remove(_textBox);
        _textBox = null;
    }

    public override void RenderPreview(DrawingContext dc) { }

    public override void Reset()
    {
        CancelTextBox();
        _pendingCommand = null;
    }
}
