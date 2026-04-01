using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ShinCapture.Editor.Objects;

namespace ShinCapture.Editor.Tools;

public class BalloonTool : ToolBase
{
    private readonly List<EditorObject> _objects;
    private readonly EditorCanvas _canvas;
    private TextBox? _textBox;

    private BalloonObject? _creating;
    private BalloonObject? _draggingBody;
    private BalloonObject? _draggingHandle;
    private BalloonObject.TailHandle _activeHandle;
    private Vector _dragOffset;

    public override string Name => "Balloon";
    public override string Icon => "💬";

    public BalloonStyle BalloonStyle { get; set; } = BalloonStyle.Rounded;

    // 활성 텍스트박스에 속성 변경 실시간 반영 (속성 변경 후 포커스 복원)
    public override Color CurrentColor
    {
        get => base.CurrentColor;
        set
        {
            base.CurrentColor = value;
            if (_creating != null) _creating.BorderColor = TextBorderColor ?? value;
            if (_textBox != null) { _textBox.BorderBrush = new SolidColorBrush(TextBorderColor ?? value); _textBox.Focus(); }
        }
    }
    public override double CurrentFontSize
    {
        get => base.CurrentFontSize;
        set
        {
            base.CurrentFontSize = value;
            if (_creating != null) _creating.FontSize = value;
            if (_textBox != null) { _textBox.FontSize = value * (_canvas?.Zoom ?? 1.0); _textBox.Focus(); }
        }
    }
    public override string CurrentFontName
    {
        get => base.CurrentFontName;
        set
        {
            base.CurrentFontName = value;
            if (_creating != null) _creating.FontName = value;
            if (_textBox != null) { _textBox.FontFamily = new FontFamily(value); _textBox.Focus(); }
        }
    }
    public override bool Bold
    {
        get => base.Bold;
        set { base.Bold = value; if (_textBox != null) { _textBox.FontWeight = value ? FontWeights.Bold : FontWeights.Normal; _textBox.Focus(); } }
    }
    public override Color? TextFillColor
    {
        get => base.TextFillColor;
        set { base.TextFillColor = value; SyncBalloonStyle(); _textBox?.Focus(); }
    }
    public override Color? TextBorderColor
    {
        get => base.TextBorderColor;
        set { base.TextBorderColor = value; SyncBalloonStyle(); _textBox?.Focus(); }
    }
    public override bool GlassBackground
    {
        get => base.GlassBackground;
        set { base.GlassBackground = value; SyncBalloonStyle(); _textBox?.Focus(); }
    }

    private void SyncBalloonStyle()
    {
        if (_creating != null)
        {
            _creating.FillColor = TextFillColor ?? Colors.White;
            _creating.BorderColor = TextBorderColor ?? CurrentColor;
        }
        if (_textBox != null)
        {
            if (GlassBackground)
            {
                var c = TextFillColor ?? Colors.White;
                _textBox.Background = new SolidColorBrush(Color.FromArgb(180, c.R, c.G, c.B));
            }
            else if (TextFillColor.HasValue)
            {
                _textBox.Background = new SolidColorBrush(TextFillColor.Value);
            }
            else
            {
                _textBox.Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255));
            }
            _textBox.BorderBrush = new SolidColorBrush(TextBorderColor ?? CurrentColor);
        }
    }

    public BalloonTool(List<EditorObject> objects, Canvas canvas)
    {
        _objects = objects;
        _canvas = (EditorCanvas)canvas;
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        if (_textBox != null)
        {
            FinalizeTextBox();
            return;
        }

        // 1. 꼬리 핸들 3개 클릭 체크 (우선)
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i] is BalloonObject balloon && balloon.IsVisible)
            {
                var handle = balloon.HitTestHandles(position);
                if (handle != BalloonObject.TailHandle.None)
                {
                    _draggingHandle = balloon;
                    _activeHandle = handle;
                    foreach (var o in _objects) o.IsSelected = false;
                    balloon.IsSelected = true;
                    return;
                }
            }
        }

        // 2. 본체 클릭 체크 → 드래그 이동
        for (int i = _objects.Count - 1; i >= 0; i--)
        {
            if (_objects[i] is BalloonObject balloon && balloon.IsVisible && balloon.HitTest(position))
            {
                _draggingBody = balloon;
                _dragOffset = position - balloon.Position;
                foreach (var o in _objects) o.IsSelected = false;
                balloon.IsSelected = true;
                return;
            }
        }

        // 3. 빈 곳 → 새 말풍선 생성
        foreach (var o in _objects) o.IsSelected = false;
        _creating = new BalloonObject
        {
            Position = position,
            TailTarget = new Point(position.X + 40, position.Y + 80),
            FillColor = TextFillColor ?? Colors.White,
            BorderColor = TextBorderColor ?? CurrentColor,
            BalloonStyle = BalloonStyle,
            FontName = CurrentFontName,
            Text = ""
        };
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        if (_draggingHandle != null)
        {
            switch (_activeHandle)
            {
                case BalloonObject.TailHandle.Tip:
                    _draggingHandle.TailTarget = position;
                    break;
                case BalloonObject.TailHandle.Left:
                case BalloonObject.TailHandle.Right:
                    // 베이스 핸들 드래그 → TailWidth 조절
                    var (_, baseL, baseR) = _draggingHandle.GetTailHandles();
                    var center = new Point((baseL.X + baseR.X) / 2, (baseL.Y + baseR.Y) / 2);
                    double dist = Math.Sqrt(DistSq(position, center));
                    _draggingHandle.TailWidth = Math.Max(4, dist * 2);
                    break;
            }
        }
        else if (_draggingBody != null)
        {
            var newPos = position - _dragOffset;
            var delta = newPos - _draggingBody.Position;
            _draggingBody.Position = newPos;
            _draggingBody.TailTarget = new Point(
                _draggingBody.TailTarget.X + delta.X,
                _draggingBody.TailTarget.Y + delta.Y);
        }
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e)
    {
        if (_draggingHandle != null)
        {
            _draggingHandle = null;
            _activeHandle = BalloonObject.TailHandle.None;
            return;
        }

        if (_draggingBody != null)
        {
            _draggingBody = null;
            return;
        }

        if (_creating != null)
            ShowTextBox(_creating.Position);
    }

    private void ShowTextBox(Point imagePos)
    {
        var screenPos = _canvas.ImageToScreen(imagePos);
        double zoom = _canvas.Zoom;

        _textBox = new TextBox
        {
            Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
            Foreground = Brushes.Black,
            BorderBrush = new SolidColorBrush(CurrentColor),
            BorderThickness = new Thickness(1.5),
            FontFamily = new FontFamily(CurrentFontName),
            FontSize = CurrentFontSize * zoom,
            MinWidth = 80,
            MinHeight = 24,
            AcceptsReturn = true,
            AcceptsTab = false,
            Padding = new Thickness(8, 6, 8, 6)
        };

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

    private void FinalizeTextBox()
    {
        if (_textBox == null) return;

        var tb = _textBox;
        _textBox = null;  // LostFocus 재진입 방지
        var text = tb.Text?.Trim();
        _canvas.Children.Remove(tb);

        if (!string.IsNullOrEmpty(text) && _creating != null)
        {
            _creating.Text = text;
            _creating.FontName = CurrentFontName;
            _creating.FontSize = CurrentFontSize;

            // 본체 크기 기준으로 꼬다리 설정: 폭 15%, 높이 30% 바깥으로
            var body = _creating.GetBodyRect();
            _creating.TailWidth = body.Width * 0.15;
            _creating.TailTarget = new Point(
                body.X + body.Width * 0.35,
                body.Bottom + body.Height * 0.3);

            _canvas.SubmitExternalCommand(new AddObjectCommand(_objects, _creating));
        }
        _creating = null;
    }

    private void CancelTextBox()
    {
        if (_textBox == null) return;
        var tb = _textBox;
        _textBox = null;
        _canvas.Children.Remove(tb);
        _creating = null;
    }

    public override IEditorCommand? GetCommand() => null;

    public override void RenderPreview(DrawingContext dc)
    {
        // 텍스트 입력 전에는 미리보기 안 보여줌
        if (_creating != null && !string.IsNullOrEmpty(_creating.Text))
            _creating.Render(dc);
    }

    public override void Reset()
    {
        CancelTextBox();
        _creating = null;
        _draggingBody = null;
        _draggingHandle = null;
    }

    private static double DistSq(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
