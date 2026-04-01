using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class TextTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private readonly Canvas _canvas;
    private TextBox? _textBox;
    private Point _position;

    public override string Name => "Text";
    public override string Icon => "T";

    // 활성 텍스트박스에 속성 변경 실시간 반영 (속성 변경 후 포커스 복원)
    public override Color CurrentColor
    {
        get => base.CurrentColor;
        set { base.CurrentColor = value; if (_textBox != null) { _textBox.Foreground = new SolidColorBrush(value); _textBox.Focus(); } }
    }
    public override bool Bold
    {
        get => base.Bold;
        set { base.Bold = value; if (_textBox != null) { _textBox.FontWeight = value ? FontWeights.Bold : FontWeights.Normal; _textBox.Focus(); } }
    }
    public override double CurrentFontSize
    {
        get => base.CurrentFontSize;
        set
        {
            base.CurrentFontSize = value;
            if (_textBox != null)
            {
                var ec = _canvas as EditorCanvas;
                _textBox.FontSize = value * (ec?.Zoom ?? 1.0);
                _textBox.Focus();
            }
        }
    }
    public override string CurrentFontName
    {
        get => base.CurrentFontName;
        set { base.CurrentFontName = value; if (_textBox != null) { _textBox.FontFamily = new FontFamily(value); _textBox.Focus(); } }
    }
    public override bool GlassBackground
    {
        get => base.GlassBackground;
        set { base.GlassBackground = value; UpdateTextBoxStyle(); _textBox?.Focus(); }
    }
    public override Color? TextFillColor
    {
        get => base.TextFillColor;
        set { base.TextFillColor = value; UpdateTextBoxStyle(); _textBox?.Focus(); }
    }
    public override Color? TextBorderColor
    {
        get => base.TextBorderColor;
        set { base.TextBorderColor = value; UpdateTextBoxStyle(); _textBox?.Focus(); }
    }

    private void UpdateTextBoxStyle()
    {
        if (_textBox == null) return;
        if (GlassBackground)
        {
            _textBox.Background = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
            _textBox.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            _textBox.BorderThickness = new Thickness(1);
        }
        else if (TextFillColor.HasValue || TextBorderColor.HasValue)
        {
            _textBox.Background = TextFillColor.HasValue
                ? new SolidColorBrush(TextFillColor.Value)
                : new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            _textBox.BorderBrush = TextBorderColor.HasValue
                ? new SolidColorBrush(TextBorderColor.Value)
                : new SolidColorBrush(Colors.DodgerBlue);
            _textBox.BorderThickness = new Thickness(TextBorderColor.HasValue ? 2 : 1);
        }
        else
        {
            _textBox.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            _textBox.BorderBrush = new SolidColorBrush(Colors.DodgerBlue);
            _textBox.BorderThickness = new Thickness(1);
        }
    }

    public TextTool(List<EditorObject> objects, Canvas canvas)
    {
        _objects = objects;
        _canvas = canvas;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        FinalizeTextBox();

        _position = position;

        var ec = _canvas as EditorCanvas;
        double zoom = ec?.Zoom ?? 1.0;

        _textBox = new TextBox
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Foreground = new SolidColorBrush(CurrentColor),
            BorderBrush = new SolidColorBrush(Colors.DodgerBlue),
            BorderThickness = new Thickness(1),
            FontFamily = new FontFamily(CurrentFontName),
            FontSize = CurrentFontSize * zoom,
            FontWeight = Bold ? FontWeights.Bold : FontWeights.Normal,
            MinWidth = 80,
            MinHeight = 24,
            AcceptsReturn = true,
            AcceptsTab = false
        };

        var screenPos = ec != null ? ec.ImageToScreen(position) : position;
        Canvas.SetLeft(_textBox, screenPos.X);
        Canvas.SetTop(_textBox, screenPos.Y);
        _canvas.Children.Add(_textBox);
        _textBox.Focus();

        _textBox.LostFocus += (_, _) =>
        {
            var tb = _textBox;
            tb?.Dispatcher.BeginInvoke(() =>
            {
                if (_textBox == tb && !tb.IsFocused)
                    FinalizeTextBox();
            }, DispatcherPriority.Background);
        };
        _textBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Escape) CancelTextBox();
        };
    }

    public override void OnMouseMove(Point position, MouseEventArgs e) { }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e) { }

    public override IEditorCommand? GetCommand() => null;

    private void FinalizeTextBox()
    {
        if (_textBox == null) return;

        var tb = _textBox;
        _textBox = null;  // LostFocus 재진입 방지: Remove 전에 null 설정
        var text = tb.Text?.Trim();
        _canvas.Children.Remove(tb);

        if (!string.IsNullOrEmpty(text))
        {
            var obj = new TextObject
            {
                Text = text,
                Position = _position,
                TextColor = CurrentColor,
                FontFamily = new FontFamily(CurrentFontName),
                FontSize = CurrentFontSize,
                Bold = Bold,
                GlassBackground = GlassBackground,
                FillColor = TextFillColor,
                BorderColor = TextBorderColor
            };
            var cmd = new AddObjectCommand(_objects, obj);
            if (_canvas is EditorCanvas ec)
                ec.SubmitExternalCommand(cmd);
        }
    }

    private void CancelTextBox()
    {
        if (_textBox == null) return;
        var tb = _textBox;
        _textBox = null;
        _canvas.Children.Remove(tb);
    }

    public override void RenderPreview(DrawingContext dc) { }

    public override void Reset()
    {
        CancelTextBox();
    }
}
