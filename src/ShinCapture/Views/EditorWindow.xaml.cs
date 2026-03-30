using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinCapture.Editor;
using ShinCapture.Editor.Tools;
using ShinCapture.Editor.Objects;
using ShinCapture.Helpers;
using ShinCapture.Models;
using ShinCapture.Services;

namespace ShinCapture.Views;

public partial class EditorWindow : Window
{
    private readonly CommandStack _commandStack = new();
    private readonly List<EditorObject> _objects = new();
    private readonly SaveManager _saveManager;
    private readonly AppSettings _settings;
    private BitmapSource _sourceImage;
    private Bitmap _sourceBitmap;
    private ITool? _activeTool;
    private readonly Dictionary<string, Button> _toolButtons = new();

    public EditorWindow(Bitmap capturedImage, SaveManager saveManager, AppSettings settings)
    {
        InitializeComponent();
        _saveManager = saveManager;
        _settings = settings;
        _sourceBitmap = capturedImage;
        _sourceImage = BitmapHelper.ToBitmapSource(capturedImage);

        Canvas.BackgroundImage = _sourceImage;
        Canvas.CommandRequested += OnCommandRequested;
        _commandStack.Changed += (_, _) => Canvas.InvalidateVisual();

        BuildToolbar();
        BuildPropertyPanel();
        UpdateStatus();

        KeyDown += OnEditorKeyDown;
        Loaded += (_, _) => Canvas.FitToView();
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _commandStack.Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _commandStack.Redo();
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnSaveClick(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            OnSaveAsClick(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnCopyClick(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            DeleteSelectedObjects();
            e.Handled = true;
        }
    }

    private void BuildToolbar()
    {
        var tools = new (string name, string icon, string group)[]
        {
            ("펜",    "✏",  "draw"),
            ("형광펜", "🖍", "draw"),
            ("도형",  "□",  "draw"),
            ("화살표", "↗", "draw"),
            ("텍스트", "T", "draw"),
            ("모자이크", "▦", "effect"),
            ("블러",  "◎",  "effect"),
            ("번호",  "①",  "effect"),
            ("말풍선", "💬", "effect"),
            ("크롭",  "✂",  "edit"),
            ("지우개", "🧹", "edit"),
            ("스포이드", "🔍", "edit"),
            ("이미지", "🖼", "edit"),
        };

        string? lastGroup = null;
        foreach (var (name, icon, group) in tools)
        {
            if (lastGroup != null && lastGroup != group)
                ToolbarPanel.Children.Add(CreateSeparator());

            var btn = CreateToolButton(icon, name);
            btn.Click += (_, _) => SelectTool(name);
            ToolbarPanel.Children.Add(btn);
            _toolButtons[name] = btn;
            lastGroup = group;
        }

        ToolbarPanel.Children.Add(CreateSeparator());

        var undoBtn = CreateToolButton("↩", "실행취소");
        undoBtn.Click += (_, _) => _commandStack.Undo();
        ToolbarPanel.Children.Add(undoBtn);

        var redoBtn = CreateToolButton("↪", "다시실행");
        redoBtn.Click += (_, _) => _commandStack.Redo();
        ToolbarPanel.Children.Add(redoBtn);
    }

    private void SelectTool(string name)
    {
        _activeTool?.Reset();
        _activeTool = name switch
        {
            "펜"    => new PenTool(_objects),
            "형광펜" => new HighlighterTool(_objects),
            "도형"  => new ShapeTool(_objects),
            "화살표" => new ArrowTool(_objects),
            "텍스트" => new TextTool(_objects, Canvas),
            "모자이크" => new MosaicTool(_objects, _sourceImage),
            "블러"  => new BlurTool(_objects),
            "번호"  => new NumberTool(_objects),
            "말풍선" => new BalloonTool(_objects),
            "크롭"  => new CropTool(_sourceImage, img =>
            {
                _sourceImage = img;
                Canvas.BackgroundImage = img;
            }),
            "지우개" => new EraserTool(_objects),
            "스포이드" => CreateEyedropperTool(),
            "이미지" => new ImageInsertTool(_objects, Canvas),
            _ => null
        };
        Canvas.SetTool(_activeTool);

        foreach (var (n, btn) in _toolButtons)
        {
            if (n == name)
            {
                btn.Background = (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBrush");
                btn.BorderBrush = (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBorderBrush");
            }
            else
            {
                btn.Background = System.Windows.Media.Brushes.Transparent;
                btn.BorderBrush = System.Windows.Media.Brushes.Transparent;
            }
        }
    }

    private EyedropperTool CreateEyedropperTool()
    {
        var tool = new EyedropperTool(_sourceImage);
        tool.ColorPicked += color =>
        {
            if (_activeTool != null)
                _activeTool.CurrentColor = color;
        };
        return tool;
    }

    private void OnCommandRequested(object? sender, IEditorCommand cmd)
    {
        _commandStack.Execute(cmd);
        UpdateStatus();
    }

    private Button CreateToolButton(string icon, string tooltip)
    {
        return new Button
        {
            Content = $"{icon} {tooltip}",
            Padding = new Thickness(6),
            Margin = new Thickness(1),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderBrush = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(1),
            FontSize = 12,
            Cursor = Cursors.Hand,
            ToolTip = tooltip
        };
    }

    private UIElement CreateSeparator()
    {
        return new Border
        {
            Width = 1,
            Height = 24,
            Margin = new Thickness(6, 0, 6, 0),
            Background = (System.Windows.Media.Brush)FindResource("DividerBrush")
        };
    }

    private void BuildPropertyPanel()
    {
        var colors = new[] { "#0078D4", "#E81123", "#10893E", "#FF8C00", "#191919", "#FFFFFF" };
        foreach (var hex in colors)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var swatch = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(color),
                BorderThickness = new Thickness(2),
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                Margin = new Thickness(2),
                Cursor = Cursors.Hand
            };
            swatch.MouseDown += (_, _) =>
            {
                if (_activeTool != null)
                    _activeTool.CurrentColor = color;
            };
            PropertyPanel.Children.Add(swatch);
        }
    }

    private void UpdateStatus()
    {
        if (_sourceImage != null)
            StatusText.Text = $"{_sourceImage.PixelWidth} × {_sourceImage.PixelHeight} px · PNG";
    }

    private void DeleteSelectedObjects()
    {
        var selected = _objects.Where(o => o.IsSelected).ToList();
        foreach (var obj in selected)
            _commandStack.Execute(new RemoveObjectCommand(_objects, obj));
        Canvas.InvalidateVisual();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var rendered = RenderFinalImage();
        var path = _saveManager.SaveAuto(rendered, _settings);
        StatusText.Text = $"저장됨: {path}";
        if (_settings.Save.CopyToClipboard)
            System.Windows.Clipboard.SetImage(BitmapHelper.ToBitmapSource(rendered));
        rendered.Dispose();
    }

    private void OnSaveAsClick(object sender, RoutedEventArgs e)
    {
        var rendered = RenderFinalImage();
        var path = _saveManager.SaveAs(rendered);
        if (path != null)
            StatusText.Text = $"저장됨: {path}";
        rendered.Dispose();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var rendered = RenderFinalImage();
        System.Windows.Clipboard.SetImage(BitmapHelper.ToBitmapSource(rendered));
        StatusText.Text = "클립보드에 복사됨";
        rendered.Dispose();
    }

    private Bitmap RenderFinalImage()
    {
        var width = _sourceImage.PixelWidth;
        var height = _sourceImage.PixelHeight;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(_sourceImage, new Rect(0, 0, width, height));
            foreach (var obj in _objects.Where(o => o.IsVisible))
                obj.Render(dc);
        }
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        return BitmapHelper.ToBitmap(rtb);
    }
}
