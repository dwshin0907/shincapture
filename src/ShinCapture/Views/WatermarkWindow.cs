using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Editor;
using ShinCapture.Models;

namespace ShinCapture.Views;

public sealed class WatermarkWindow : Window
{
    private readonly TextBox _text;
    private readonly Slider _size;
    private readonly Slider _opacity;
    private readonly ComboBox _position;
    private readonly ComboBox _color;
    private readonly Image _preview;
    private readonly Button _apply;
    private readonly BitmapSource _source;
    public WatermarkSettings Result => new()
    {
        Text = _text.Text, FontSize = _size.Value, Opacity = _opacity.Value / 100,
        Position = _position.SelectedIndex, White = _color.SelectedIndex == 0
    };

    public WatermarkWindow(BitmapSource source, WatermarkSettings settings)
    {
        _source = source;
        Title = "워터마크 넣기";
        Width = 580; Height = 650; MinWidth = 420; MinHeight = 530;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "BackgroundPrimaryBrush");
        SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
        SetResourceReference(FontFamilyProperty, "AppFont");
        var root = new DockPanel { Margin = new Thickness(20) };
        Content = root;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var cancel = new Button { Content = "취소", IsCancel = true, Padding = new Thickness(18, 7, 18, 7), Margin = new Thickness(0, 12, 8, 0) };
        _apply = new Button { Content = "넣기", IsDefault = true, Padding = new Thickness(18, 7, 18, 7), Margin = new Thickness(0, 12, 0, 0) };
        _apply.SetResourceReference(StyleProperty, "AccentButton");
        _apply.Click += (_, _) => DialogResult = true;
        footer.Children.Add(cancel); footer.Children.Add(_apply);
        var controls = new StackPanel();
        DockPanel.SetDock(controls, Dock.Top); root.Children.Add(controls);
        void Label(string caption) => controls.Children.Add(new TextBlock { Text = caption, Margin = new Thickness(0, 8, 0, 5) });
        Label("워터마크 문구");
        _text = new TextBox { Text = settings.Text, MaxLength = 200, Padding = new Thickness(8) };
        controls.Children.Add(_text);
        Label("글자 크기 (px) · 이미지보다 크면 자동으로 맞춥니다");
        _size = new Slider { Minimum = 8, Maximum = 200, Value = double.IsFinite(settings.FontSize) ? settings.FontSize : 32,
            TickFrequency = 1, IsSnapToTickEnabled = true, AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft };
        controls.Children.Add(_size);
        Label("불투명도 · 낮을수록 연하게 표시됩니다");
        _opacity = new Slider { Minimum = 5, Maximum = 100, Value = double.IsFinite(settings.Opacity) ? settings.Opacity * 100 : 40,
            TickFrequency = 5, IsSnapToTickEnabled = true, AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft };
        controls.Children.Add(_opacity);
        Label("위치");
        _position = new ComboBox { ItemsSource = new[] { "왼쪽 위", "가운데 위", "오른쪽 위", "왼쪽 가운데", "정중앙", "오른쪽 가운데", "왼쪽 아래", "가운데 아래", "오른쪽 아래" }, SelectedIndex = Math.Clamp(settings.Position, 0, 8) };
        controls.Children.Add(_position);
        Label("색상");
        _color = new ComboBox { ItemsSource = new[] { "흰색", "검정" }, SelectedIndex = settings.White ? 0 : 1 };
        controls.Children.Add(_color);
        Label("미리보기 · 넣은 뒤 선택 도구로 이동하거나 Ctrl+Z로 취소할 수 있습니다.");
        _preview = new Image { Stretch = Stretch.Uniform };
        root.Children.Add(new Border { Background = Brushes.DimGray, Padding = new Thickness(8), Child = _preview });
        _text.TextChanged += (_, _) => RefreshPreview();
        _size.ValueChanged += (_, _) => RefreshPreview();
        _opacity.ValueChanged += (_, _) => RefreshPreview();
        _position.SelectionChanged += (_, _) => RefreshPreview();
        _color.SelectionChanged += (_, _) => RefreshPreview();
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        _apply.IsEnabled = !string.IsNullOrWhiteSpace(_text.Text);
        if (!_apply.IsEnabled) { _preview.Source = _source; return; }
        // A vector preview avoids allocating a full-resolution bitmap on every slider tick.
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            dc.DrawImage(_source, new Rect(0, 0, _source.PixelWidth, _source.PixelHeight));
            WatermarkFactory.Create(_source.PixelWidth, _source.PixelHeight, Result).Render(dc);
        }
        _preview.Source = new DrawingImage(drawing);
    }
}
