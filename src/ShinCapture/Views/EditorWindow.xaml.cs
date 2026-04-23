using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
    private readonly SettingsManager? _settingsManager;
    private BitmapSource _sourceImage;
    private Bitmap _sourceBitmap;
    private ITool? _activeTool;
    private readonly Dictionary<string, Button> _toolButtons = new();

    // 캡쳐 기록 (세션 동안 유지, 최대 50개)
    private static readonly List<BitmapSource> _captureHistory = new();
    private const int MaxHistory = 50;

    // 캡쳐별 편집 상태 보존
    private readonly Dictionary<BitmapSource, List<EditorObject>> _captureObjects = new();

    public EditorWindow(Bitmap capturedImage, SaveManager saveManager, AppSettings settings, SettingsManager? settingsManager = null)
    {
        InitializeComponent();
        _saveManager = saveManager;
        _settings = settings;
        _settingsManager = settingsManager;
        _sourceBitmap = capturedImage;
        _sourceImage = BitmapHelper.ToBitmapSource(capturedImage);

        // 캡쳐 기록에 추가
        AddToHistory(_sourceImage);

        Canvas.Objects = _objects;
        Canvas.CommandRequested += OnCommandRequested;
        Canvas.ZoomChanged += (_, zoom) => UpdateZoomDisplay(zoom);
        _commandStack.Changed += (_, _) => { Canvas.InvalidateVisual(); UpdateStatus(); };

        BuildToolbar();
        BuildHistory();
        UpdateStatus();

        PreviewKeyDown += OnEditorKeyDown;
        Closing += OnEditorClosing;
        Loaded += (_, _) =>
        {
            SizeWindowToImage();
            Canvas.BackgroundImage = _sourceImage;
        };
    }

    private void SizeWindowToImage()
    {
        if (_sourceImage == null) return;

        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        double dpiScale = dpi.PixelsPerDip;

        // 이미지의 논리 크기 (WPF DIPs)
        double imgLogicalW = _sourceImage.PixelWidth / dpiScale;
        double imgLogicalH = _sourceImage.PixelHeight / dpiScale;

        // 툴바+속성+상태바 높이 추정 (창 크기 - 캔버스 크기)
        double chromeW = Width - Canvas.ActualWidth;
        double chromeH = Height - Canvas.ActualHeight;
        if (chromeW < 0) chromeW = 0;
        if (chromeH < 0) chromeH = 80;

        // 패딩 (상하좌우 20px)
        double padding = 40;

        double desiredW = imgLogicalW + chromeW + padding;
        double desiredH = imgLogicalH + chromeH + padding;

        // 화면 크기 제한
        var screen = System.Windows.Forms.Screen.FromHandle(
            new System.Windows.Interop.WindowInteropHelper(this).Handle);
        var workArea = screen.WorkingArea;
        double maxW = workArea.Width / dpiScale;
        double maxH = workArea.Height / dpiScale;

        Width = Math.Min(desiredW, maxW);
        Height = Math.Min(desiredH, maxH);

        // 화면 중앙 배치
        Left = (maxW - Width) / 2 + workArea.Left / dpiScale;
        Top = (maxH - Height) / 2 + workArea.Top / dpiScale;
    }

    /// <summary>기존 에디터에 새 캡쳐를 로드 (창을 재사용)</summary>
    public void LoadNewCapture(Bitmap capturedImage)
    {
        // 현재 편집 상태 저장
        SaveCurrentObjects();

        _sourceBitmap = capturedImage;
        _sourceImage = BitmapHelper.ToBitmapSource(capturedImage);

        AddToHistory(_sourceImage);

        _objects.Clear();
        _commandStack.Clear();
        _activeTool?.Reset();

        Canvas.BackgroundImage = _sourceImage;
        SizeWindowToImage();
        UpdateLayout();
        Canvas.FitToView();
        BuildHistory();
        UpdateStatus();
    }

    /// <summary>기록에서 이미지를 메인 화면에 로드</summary>
    private void LoadFromHistory(BitmapSource image)
    {
        // 현재 편집 상태 저장
        SaveCurrentObjects();

        _sourceImage = image;
        _activeTool?.Reset();
        _commandStack.Clear();

        // 저장된 편집 상태 복원
        _objects.Clear();
        if (_captureObjects.TryGetValue(image, out var saved))
            _objects.AddRange(saved);

        Canvas.BackgroundImage = _sourceImage;
        SizeWindowToImage();
        UpdateLayout();
        Canvas.FitToView();
        BuildHistory();
        UpdateStatus();
    }

    private void SaveCurrentObjects()
    {
        if (_sourceImage != null)
            _captureObjects[_sourceImage] = new List<EditorObject>(_objects);
    }

    private bool _forceClose;

    /// <summary>X 버튼 → 숨기기 (편집 상태 유지). 앱 종료 시에만 ForceClose.</summary>
    private void OnEditorClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
        }
    }

    /// <summary>앱 종료 시 실제로 창을 닫음</summary>
    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        bool inTextBox = e.OriginalSource is TextBox tb && tb.IsFocused && tb.Text?.Length > 0;

        // 텍스트 입력 중: Ctrl+C는 선택 텍스트가 있으면 텍스트 복사, 없으면 이미지 복사
        if (e.OriginalSource is TextBox)
        {
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var box = (TextBox)e.OriginalSource;
                if (!string.IsNullOrEmpty(box.SelectedText)) return; // 텍스트 복사 허용
                // 선택 텍스트 없으면 아래에서 이미지 복사
            }
            else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // 클립보드에 이미지가 있으면 캔버스에 붙여넣기, 텍스트면 텍스트박스에 허용
                if (System.Windows.Clipboard.ContainsImage())
                {
                    PasteImageFromClipboard();
                    e.Handled = true;
                }
                return;
            }
            else return; // 다른 단축키는 가로채지 않음
        }

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
        else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
        {
            PasteImageFromClipboard();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            DeleteSelectedObjects();
            e.Handled = true;
        }
        else if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            MoveSelectedByArrow(e.Key, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            e.Handled = true;
        }
        // 도구 단축키 (Ctrl/Shift 없을 때만, 텍스트 입력 중 아닐 때)
        else if (Keyboard.Modifiers == ModifierKeys.None)
        {
            var toolName = e.Key switch
            {
                Key.V => "선택",
                Key.P => "펜",
                Key.H => "형광펜",
                Key.U => "도형",
                Key.A => "화살표",
                Key.T => "텍스트",
                Key.B => "말풍선",
                Key.M => "모자이크",
                Key.N => "번호",
                Key.I => "이미지",
                Key.C => "크롭",
                Key.E => "지우개",
                _ => (string?)null
            };
            if (toolName != null) { SelectTool(toolName); e.Handled = true; }
        }
    }

    private void MoveSelectedByArrow(Key key, bool fine)
    {
        var selected = _objects.Where(o => o.IsSelected).ToList();
        if (selected.Count == 0) return;

        double step = fine ? 1 : 10;
        var delta = key switch
        {
            Key.Left  => new Vector(-step, 0),
            Key.Right => new Vector(step, 0),
            Key.Up    => new Vector(0, -step),
            Key.Down  => new Vector(0, step),
            _ => new Vector(0, 0)
        };

        foreach (var obj in selected)
            obj.Move(delta);
        Canvas.InvalidateVisual();
    }

    private void BuildToolbar()
    {
        // 아이콘 전용 도구 목록 (유니코드 심볼)
        var tools = new (string name, string icon, string group, string shortcut)[]
        {
            ("선택",    "cursor", "select", "V"),   // 윈도우 마우스 포인터 (Path)
            ("펜",      "pen", "draw",   "P"),      // 펜 Path 아이콘
            ("형광펜",  "\u301C", "draw",   "H"),   // 〜 꼬불
            ("도형",    "\u25A1", "draw",   "U"),   // □
            ("화살표",  "\u2197", "draw",   "A"),   // ↗
            ("텍스트",  "T",      "text",   "T"),
            ("말풍선",  "\u2606", "text",   "B"),   // ☆
            ("모자이크","\u25A6", "effect", "M"),   // ▦
            ("블러",    "\u25CE", "effect", ""),     // ◎
            ("번호",    "\u2460", "effect", "N"),   // ①
            ("이미지",  "\u229E", "insert", "I"),   // ⊞
            ("색상추출","\u2316", "insert", ""),    // ⌖
            ("OCR",     "\U0001F524", "insert", ""), // 🔤 텍스트 추출
            ("크롭",    "\u2702", "edit",   "C"),   // ✂
            ("지우개",  "\u2312", "edit",   "E"),   // ⌒
        };

        string? lastGroup = null;
        foreach (var (name, icon, group, sc) in tools)
        {
            if (lastGroup != null && lastGroup != group)
                ToolbarPanel.Children.Add(CreateSeparator());

            var tip = string.IsNullOrEmpty(sc) ? name : $"{name} ({sc})";
            var btn = CreateToolButton(icon, tip);
            if (name == "OCR")
            {
                btn.ToolTip = "텍스트 추출 (OCR)";
                btn.Click += (_, _) => RunEditorOcr();
            }
            else
            {
                btn.Click += (_, _) => SelectTool(name);
            }
            ToolbarPanel.Children.Add(btn);
            _toolButtons[name] = btn;
            lastGroup = group;
        }

        ToolbarPanel.Children.Add(CreateSeparator());

        var undoBtn = CreateToolButton("\u21A9", "실행취소 (Ctrl+Z)"); // ↩
        undoBtn.Click += (_, _) => _commandStack.Undo();
        ToolbarPanel.Children.Add(undoBtn);

        var redoBtn = CreateToolButton("\u21AA", "다시실행 (Ctrl+Y)"); // ↪
        redoBtn.Click += (_, _) => _commandStack.Redo();
        ToolbarPanel.Children.Add(redoBtn);

        // 채널 배너 (우측 고정, 네이버 녹색 그라데이션 + 플래시카드 애니메이션)
        LinearGradientBrush MakeBg(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            var brush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1)
            };
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(r1, g1, b1), 0));
            brush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromRgb(r2, g2, b2), 1));
            return brush;
        }

        var shadow = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 8,
            ShadowDepth = 1,
            Direction = 270,
            Color = System.Windows.Media.Colors.Black,
            Opacity = 0.22
        };

        var banner = new Border
        {
            Background = MakeBg(0x03, 0xC7, 0x5A, 0x02, 0xB0, 0x50),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(13, 6, 13, 6),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = shadow,
            ToolTip = "ChatGPT도 모르는 AI실전활용법 — 네이버 프리미엄콘텐츠 AI 활용법 분야 1위 채널"
        };

        var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };
        var subText = new TextBlock
        {
            Text = "네이버 프리미엄콘텐츠 AI 분야 1위",
            FontSize = 9.5,
            FontWeight = FontWeights.Normal,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 1)
        };

        var flashMessages = new[]
        {
            "아직도 ChatGPT만 쓰세요? \u2192",
            "ChatGPT도 모르는 실전 활용법 \u2192",
            "AI 시대, 뒤처지지 않으려면 \u2192",
            "지금 바로 공부하러 가기 \u2192"
        };
        int flashIndex = 0;
        var mainText = new TextBlock
        {
            Text = flashMessages[0],
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            // 가장 긴 카피 기준으로 폭 확보 → 전환 시 레이아웃 흔들림 방지
            MinWidth = 200
        };
        stack.Children.Add(subText);
        stack.Children.Add(mainText);
        banner.Child = stack;

        // 2.5초 간격 플래시카드: fade-out (280ms) → 텍스트 교체 → fade-in (280ms)
        var flashTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2.5)
        };
        flashTimer.Tick += (_, _) =>
        {
            var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(
                1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(280)))
            {
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn
                }
            };
            fadeOut.Completed += (__, ___) =>
            {
                flashIndex = (flashIndex + 1) % flashMessages.Length;
                mainText.Text = flashMessages[flashIndex];
                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(
                    0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(280)))
                {
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };
                mainText.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };
            mainText.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        };
        flashTimer.Start();
        this.Closed += (_, _) => flashTimer.Stop();

        banner.MouseDown += (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://contents.premium.naver.com/market/ai",
                UseShellExecute = true
            });
        };
        banner.MouseEnter += (_, _) =>
        {
            banner.Background = MakeBg(0x05, 0xDB, 0x66, 0x03, 0xBE, 0x57);
            shadow.Opacity = 0.35;
            shadow.BlurRadius = 12;
        };
        banner.MouseLeave += (_, _) =>
        {
            banner.Background = MakeBg(0x03, 0xC7, 0x5A, 0x02, 0xB0, 0x50);
            shadow.Opacity = 0.22;
            shadow.BlurRadius = 8;
        };
        BannerArea.Child = banner;
    }

    private void SelectTool(string name)
    {
        // 이전 도구 기록 (스포이드 복귀용)
        if (_activeTool != null && name == "색상추출")
        {
            var prevName = _toolButtons.FirstOrDefault(kv => kv.Value.Style == (Style)FindResource("ToolbarButtonActive")).Key;
            if (prevName != null) _lastToolName = prevName;
        }

        if (name == "크롭" && _objects.Count > 0)
        {
            StatusText.Text = "편집 개체가 있으면 크롭할 수 없습니다. 개체를 모두 삭제하거나 실행취소 후 사용하세요.";
            return;
        }

        _activeTool?.Reset();
        _activeTool = name switch
        {
            "선택"  => new SelectTool(_objects),
            "펜"    => new PenTool(_objects),
            "형광펜" => new HighlighterTool(_objects),
            "도형"  => new ShapeTool(_objects) { SelectedShape = _selectedShapeType, SelectedFill = _selectedFillMode },
            "화살표" => new ArrowTool(_objects) { SelectedHeadStyle = _selectedArrowHead, SelectedLineStyle = _selectedArrowLine },
            "텍스트" => new TextTool(_objects, Canvas),
            "모자이크" => new MosaicTool(_objects, _sourceImage),
            "블러"  => new BlurTool(_objects, _sourceImage),
            "번호"  => new NumberTool(_objects),
            "말풍선" => new BalloonTool(_objects, Canvas),
            "이미지" => new ImageInsertTool(_objects, Canvas),
            "색상추출" => CreateEyedropper(),
            "크롭"  => new CropTool(_sourceImage, img =>
            {
                _sourceImage = img;
                Canvas.BackgroundImage = img;
            }, _objects),
            "지우개" => new EraserTool(_objects) { Mode = _eraserMode },
            _ => null
        };
        // 속성 동기화
        if (_activeTool != null)
        {
            _activeTool.CurrentColor = _currentColor;
            _currentWidth = _activeTool.CurrentWidth;
            _activeTool.CurrentFontName = _currentFontName;
            _activeTool.CurrentFontSize = _currentFontSize;
            _activeTool.GlassBackground = _currentGlass;
            _activeTool.Bold = _currentBold;
            _activeTool.TextFillColor = _textFillColor;
            _activeTool.TextBorderColor = _textBorderColor;
        }
        Canvas.SetTool(_activeTool);
        BuildContextProperties(name);
        BuildSubOptions(name);

        foreach (var (n, btn) in _toolButtons)
        {
            btn.Style = (Style)FindResource(n == name ? "ToolbarButtonActive" : "ToolbarButton");
        }
    }

    private void OnCommandRequested(object? sender, IEditorCommand cmd)
    {
        _commandStack.Execute(cmd);
        UpdateStatus();
    }

    private Button CreateToolButton(string icon, string tooltip)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };

        if (icon is "cursor" or "pen")
        {
            var pathData = icon switch
            {
                "cursor" => "M 2,0 L 2,14 L 5.5,10.5 L 9,16 L 11,15 L 7.5,9 L 12,9 Z",
                "pen" => "M 13.5,1.5 L 15,3 L 5,13 L 2,14 L 3,11 Z M 11.5,3.5 L 13,5",
                _ => ""
            };
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(pathData),
                Fill = icon == "cursor"
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A))
                    : null,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A)),
                StrokeThickness = icon == "cursor" ? 0.8 : 1.4,
                StrokeLineJoin = PenLineJoin.Round,
                Width = 14, Height = 16,
                Stretch = System.Windows.Media.Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (icon == "cursor")
            {
                path.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
                path.StrokeThickness = 0.8;
            }
            sp.Children.Add(path);
        }
        else
        {
            sp.Children.Add(new TextBlock { Text = icon, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
        }

        var name = tooltip.Contains('(') ? tooltip[..tooltip.IndexOf('(')].Trim() : tooltip;
        sp.Children.Add(new TextBlock { Text = name, FontSize = 13, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return new Button
        {
            Content = sp,
            Style = (Style)FindResource("ToolbarButton"),
            Margin = new Thickness(1),
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

    // 속성 패널 상태
    private double _currentWidth = 3;
    private double _currentFontSize = 40;
    private string _currentFontName = "Paperlogy 5";
    private bool _currentGlass = false;
    private bool _currentBold = false;
    private System.Windows.Media.Color? _textFillColor = null;
    private System.Windows.Media.Color? _textBorderColor = null;
    private System.Windows.Media.Color _currentColor = System.Windows.Media.Colors.Black;

    /// <summary>도구에 맞는 속성만 표시</summary>
    private void BuildContextProperties(string toolName)
    {
        PropertyPanel.Children.Clear();

        bool needsColor = toolName != "선택" && toolName != "크롭" && toolName != "모자이크" && toolName != "블러" && toolName != "지우개" && toolName != "색상추출" && toolName != "이미지";
        bool needsWidth = toolName is "펜" or "형광펜" or "도형" or "화살표" or "번호";
        bool needsFont = toolName is "텍스트" or "말풍선";
        bool needsGlass = toolName is "텍스트" or "말풍선";

        // 선택 도구: 안내만
        if (toolName == "선택")
        {
            PropertyPanel.Children.Add(MakeLabel("개체를 클릭하여 선택 / 드래그로 이동 / 코너 핸들로 크기 조절"));
            return;
        }

        // ── 색상 ──
        if (needsColor)
        {
            var colorLabel = toolName switch
            {
                "텍스트" or "말풍선" => "글자색",
                "도형" => "윤곽선색",
                "화살표" => "화살표색",
                "펜" or "형광펜" => "펜색",
                "번호" => "번호색",
                "모자이크" or "블러" => "영역색",
                _ => "색상"
            };
            PropertyPanel.Children.Add(MakeSectionLabel(colorLabel));

            // 현재 선택된 색상 미리보기 (항상 맨 앞에 크게 표시)
            var currentSwatch = new Border
            {
                Width = 22, Height = 22,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(_currentColor),
                BorderThickness = new Thickness(2),
                BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush"),
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}",
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.DodgerBlue, BlurRadius = 4,
                    ShadowDepth = 0, Opacity = 0.5
                }
            };
            currentSwatch.MouseDown += (_, _) => OpenColorPicker();
            PropertyPanel.Children.Add(currentSwatch);

            // 팔레트 색상들
            var colors = new[]
            {
                "#191919", "#FFFFFF", "#E81123", "#0078D4", "#10893E", "#FF8C00",
                "#7B2FF7", "#E74856", "#00B7C3", "#847545",
                "#FFB3BA", "#FFDFBA", "#FFFFBA", "#BAFFC9", "#BAE1FF",
                "#E8BAFF", "#FFC8DD", "#BDE0FE", "#A2D2FF", "#CDB4DB"
            };
            foreach (var hex in colors)
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                bool selected = _currentColor == color;
                var swatch = new Border
                {
                    Width = 14, Height = 14,
                    CornerRadius = new CornerRadius(7),
                    Background = new SolidColorBrush(color),
                    BorderThickness = new Thickness(selected ? 2 : 1),
                    BorderBrush = selected
                        ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                        : new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0)),
                    Margin = new Thickness(1), Cursor = Cursors.Hand
                };
                swatch.MouseDown += (_, _) =>
                {
                    _currentColor = color;
                    if (_activeTool != null) _activeTool.CurrentColor = color;
                    BuildContextProperties(toolName);
                };
                PropertyPanel.Children.Add(swatch);
            }

            // 스포이드 버튼 (이미지에서 색상 추출)
            var eyedropBtn = new Border
            {
                Width = 22, Height = 22,
                CornerRadius = new CornerRadius(4),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderLightBrush"),
                Margin = new Thickness(3, 0, 0, 0), Cursor = Cursors.Hand,
                ToolTip = "이미지에서 색상 추출 (스포이드)",
                Child = new TextBlock { Text = "\u2316", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            eyedropBtn.MouseDown += (_, _) => SelectTool("색상추출");
            eyedropBtn.MouseEnter += (_, _) => eyedropBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8));
            eyedropBtn.MouseLeave += (_, _) => eyedropBtn.Background = System.Windows.Media.Brushes.Transparent;
            PropertyPanel.Children.Add(eyedropBtn);

            // 컬러 피커 버튼 (전체 색상 다이얼로그)
            var pickerBtn = new Border
            {
                Width = 22, Height = 22,
                CornerRadius = new CornerRadius(4),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderLightBrush"),
                Margin = new Thickness(2, 0, 0, 0), Cursor = Cursors.Hand,
                ToolTip = "색상 선택 (전체 팔레트)"
            };
            // 무지개 그라데이션 아이콘
            var pickerIcon = new Border
            {
                Width = 12, Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = new LinearGradientBrush(
                    new GradientStopCollection {
                        new GradientStop(Colors.Red, 0),
                        new GradientStop(Colors.Yellow, 0.25),
                        new GradientStop(Colors.Lime, 0.5),
                        new GradientStop(Colors.Cyan, 0.75),
                        new GradientStop(Colors.Blue, 1)
                    }, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            pickerBtn.Child = pickerIcon;
            pickerBtn.MouseDown += (_, _) => OpenColorPicker();
            pickerBtn.MouseEnter += (_, _) => pickerBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8));
            pickerBtn.MouseLeave += (_, _) => pickerBtn.Background = System.Windows.Media.Brushes.Transparent;
            PropertyPanel.Children.Add(pickerBtn);

            PropertyPanel.Children.Add(MakeDivider());
        }

        // ── 선 두께 ──
        if (needsWidth)
        {
            bool isHighlighter = toolName == "형광펜";
            bool isNumber = toolName == "번호";
            double maxWidth = isHighlighter ? 40 : isNumber ? 40 : 20;
            double widthVal = isHighlighter ? Math.Max(_currentWidth, 20) : _currentWidth;
            var widthLabel = toolName == "번호" ? "크기" : "굵기";
            PropertyPanel.Children.Add(MakeSectionLabel(widthLabel));
            var slider = new Slider
            {
                Minimum = 1, Maximum = maxWidth, Value = widthVal,
                Width = 140, VerticalAlignment = VerticalAlignment.Center
            };
            var label = MakeLabel($"{_currentWidth:0.#}px");
            label.Width = 36;
            slider.ValueChanged += (_, e) =>
            {
                _currentWidth = Math.Round(e.NewValue, 1);
                label.Text = $"{_currentWidth:0.#}px";
                if (_activeTool != null) _activeTool.CurrentWidth = _currentWidth;
            };
            PropertyPanel.Children.Add(slider);
            PropertyPanel.Children.Add(label);
            PropertyPanel.Children.Add(MakeDivider());
        }

        // ── 폰트 / 크기 ──
        if (needsFont)
        {
            PropertyPanel.Children.Add(MakeSectionLabel("폰트"));
            var combo = new ComboBox
            {
                Width = 216, Height = 28, FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                MaxDropDownHeight = 400
            };
            var sampleText = "신캡쳐 좋죠";
            var installed = System.Windows.Media.Fonts.SystemFontFamilies
                .Select(f => f.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fontList = new (string label, string name)[]
            {
                ("맑은 고딕", "Malgun Gothic"), ("나눔고딕", "NanumGothic"),
                ("나눔고딕코딩", "NanumGothicCoding"), ("한컴 고딕", "Hancom Gothic"),
                ("굴림", "Gulim"), ("돋움", "Dotum"),
                ("바탕", "Batang"), ("궁서", "Gungsuh"),
                ("Paperlogy", "Paperlogy 5"),
                ("Segoe UI", "Segoe UI"), ("Arial", "Arial"), ("Consolas", "Consolas"),
            };
            int selectedIdx = 0;
            int idx = 0;
            foreach (var (lbl, fn) in fontList.Where(f => installed.Contains(f.name)))
            {
                var ff = new System.Windows.Media.FontFamily(fn);
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock { Text = lbl, FontFamily = ff, FontSize = 13, Width = 80,
                    VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new TextBlock { Text = sampleText, FontFamily = ff, FontSize = 13,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) });
                combo.Items.Add(new ComboBoxItem { Content = sp, Tag = fn, Height = 28 });
                if (fn == _currentFontName) selectedIdx = idx;
                idx++;
            }
            if (combo.Items.Count == 0)
                combo.Items.Add(new ComboBoxItem { Content = "Segoe UI", Tag = "Segoe UI" });
            combo.SelectedIndex = selectedIdx;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is ComboBoxItem ci && ci.Tag is string fn)
                {
                    _currentFontName = fn;
                    if (_activeTool != null) _activeTool.CurrentFontName = fn;
                }
            };
            PropertyPanel.Children.Add(combo);

            PropertyPanel.Children.Add(MakeSectionLabel("크기"));
            var sizeSlider = new Slider
            {
                Minimum = 8, Maximum = 200, Value = _currentFontSize,
                Width = 60, VerticalAlignment = VerticalAlignment.Center
            };
            var sizeLabel = MakeLabel($"{_currentFontSize:0}pt");
            sizeLabel.Width = 36;
            sizeSlider.ValueChanged += (_, e) =>
            {
                _currentFontSize = Math.Round(e.NewValue);
                sizeLabel.Text = $"{_currentFontSize:0}pt";
                if (_activeTool != null) _activeTool.CurrentFontSize = _currentFontSize;
            };
            PropertyPanel.Children.Add(sizeSlider);
            PropertyPanel.Children.Add(sizeLabel);
            PropertyPanel.Children.Add(MakeDivider());
        }

        // ── 배경색 (텍스트 전용) ──
        if (needsGlass)
        {
            PropertyPanel.Children.Add(MakeSectionLabel("배경"));
            PropertyPanel.Children.Add(MakeColorOption("없음", null, _textFillColor, c =>
            {
                _textFillColor = c;
                if (_activeTool != null) _activeTool.TextFillColor = c;
                BuildContextProperties(toolName);
            }));
            var bgColors = new[] { "#FFFFFF", "#FFF3CD", "#D1ECF1", "#D4EDDA", "#F8D7DA", "#E2D9F3", "#FFE0B2", "#191919" };
            foreach (var hex in bgColors)
            {
                var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                PropertyPanel.Children.Add(MakeColorSwatch(c, _textFillColor, localC =>
                {
                    _textFillColor = localC;
                    if (_activeTool != null) _activeTool.TextFillColor = localC;
                    BuildContextProperties(toolName);
                }));
            }
            PropertyPanel.Children.Add(MakePickerButton(c => {
                _textFillColor = c;
                if (_activeTool != null) _activeTool.TextFillColor = c;
                BuildContextProperties(toolName);
            }));
            PropertyPanel.Children.Add(MakeDivider());

            // ── 테두리색 ──
            PropertyPanel.Children.Add(MakeSectionLabel("테두리"));
            PropertyPanel.Children.Add(MakeColorOption("없음", null, _textBorderColor, c =>
            {
                _textBorderColor = c;
                if (_activeTool != null) _activeTool.TextBorderColor = c;
                BuildContextProperties(toolName);
            }));
            var bdColors = new[] { "#191919", "#E81123", "#0078D4", "#10893E", "#FF8C00", "#7B2FF7", "#847545", "#CCCCCC" };
            foreach (var hex in bdColors)
            {
                var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                PropertyPanel.Children.Add(MakeColorSwatch(c, _textBorderColor, localC =>
                {
                    _textBorderColor = localC;
                    if (_activeTool != null) _activeTool.TextBorderColor = localC;
                    BuildContextProperties(toolName);
                }));
            }
            PropertyPanel.Children.Add(MakePickerButton(c => {
                _textBorderColor = c;
                if (_activeTool != null) _activeTool.TextBorderColor = c;
                BuildContextProperties(toolName);
            }));
            PropertyPanel.Children.Add(MakeDivider());

            // ── Glass / Bold (우측 끝) ──
            var glass = new CheckBox
            {
                Content = "반투명", IsChecked = _currentGlass,
                VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 4, 0)
            };
            glass.Checked += (_, _) => { _currentGlass = true; if (_activeTool != null) _activeTool.GlassBackground = true; };
            glass.Unchecked += (_, _) => { _currentGlass = false; if (_activeTool != null) _activeTool.GlassBackground = false; };
            PropertyPanel.Children.Add(glass);

            var bold = new CheckBox
            {
                Content = "Bold", IsChecked = _currentBold,
                VerticalAlignment = VerticalAlignment.Center, FontSize = 11,
                FontWeight = FontWeights.Bold, Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 4, 0)
            };
            bold.Checked += (_, _) => { _currentBold = true; if (_activeTool != null) _activeTool.Bold = true; };
            bold.Unchecked += (_, _) => { _currentBold = false; if (_activeTool != null) _activeTool.Bold = false; };
            PropertyPanel.Children.Add(bold);
        }
    }

    private Border MakeColorSwatch(System.Windows.Media.Color color, System.Windows.Media.Color? selected, Action<System.Windows.Media.Color> onClick)
    {
        bool isSelected = selected.HasValue && selected.Value == color;
        var swatch = new Border
        {
            Width = 14, Height = 14, CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(color),
            BorderThickness = new Thickness(isSelected ? 2.5 : 1),
            BorderBrush = isSelected
                ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                : new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0)),
            Margin = new Thickness(1), Cursor = Cursors.Hand
        };
        if (isSelected)
            swatch.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.DodgerBlue, BlurRadius = 4,
                ShadowDepth = 0, Opacity = 0.6
            };
        swatch.MouseDown += (_, _) => onClick(color);
        return swatch;
    }

    private Button MakeColorOption(string label, System.Windows.Media.Color? value, System.Windows.Media.Color? current, Action<System.Windows.Media.Color?> onClick)
    {
        bool active = (value == null && !current.HasValue) || (value.HasValue && current.HasValue && value.Value == current.Value);
        var btn = MakeSmallButton(label, active);
        btn.Click += (_, _) => onClick(value);
        return btn;
    }

    private TextBlock MakeSectionLabel(string text) => new TextBlock
    {
        Text = text, VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(6, 0, 4, 0), FontSize = 11, FontWeight = FontWeights.SemiBold,
        Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush")
    };

    private TextBlock MakeLabel(string text) => new TextBlock
    {
        Text = text, VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(4, 0, 4, 0),
        Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"), FontSize = 13
    };

    private Border MakeDivider() => new Border
    {
        Width = 1, Height = 20, Margin = new Thickness(4, 0, 4, 0),
        Background = (System.Windows.Media.Brush)FindResource("DividerBrush")
    };

    private Button MakeSmallButton(string text, bool active) => new Button
    {
        Content = text, FontSize = 10, Padding = new Thickness(4, 2, 4, 2),
        Margin = new Thickness(1), Cursor = Cursors.Hand,
        Background = active
            ? (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBrush")
            : System.Windows.Media.Brushes.Transparent,
        BorderBrush = active
            ? (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBorderBrush")
            : System.Windows.Media.Brushes.Transparent,
        BorderThickness = new Thickness(1)
    };

    private ShapeType _selectedShapeType = ShapeType.Rectangle;
    private FillMode _selectedFillMode = FillMode.None;
    private ArrowHeadStyle _selectedArrowHead = ArrowHeadStyle.Arrow;
    private ArrowLineStyle _selectedArrowLine = ArrowLineStyle.Solid;
    private string _highlighterGradient = "없음";

    private void BuildSubOptions(string toolName)
    {
        SubOptionPanel.Children.Clear();

        if (toolName == "형광펜")
        {
            SubOptionPanel.Children.Add(MakeSectionLabel("그라데이션"));
            var modes = new[] { "단색", "🌈 무지개", "🌑 모노", "🍂 가을", "🌿 여름", "🌊 바다", "🎀 파스텔", "💡 네온" };
            var modeKeys = new[] { "없음", "무지개", "모노", "가을", "여름", "바다", "파스텔", "네온" };
            for (int i = 0; i < modes.Length; i++)
            {
                var key = modeKeys[i];
                var btn = MakeSmallButton(modes[i], _highlighterGradient == key);
                btn.Click += (_, _) =>
                {
                    _highlighterGradient = key;
                    if (_activeTool is HighlighterTool ht) ht.GradientMode = key;
                    BuildSubOptions("형광펜");
                };
                SubOptionPanel.Children.Add(btn);
            }
        }
        else if (toolName == "모자이크")
        {
            SubOptionPanel.Children.Add(MakeSectionLabel("해상도"));
            var sizes = new (string label, MosaicSize size)[]
            {
                ("세밀 (5px)", MosaicSize.Small),
                ("보통 (10px)", MosaicSize.Medium),
                ("거칠게 (20px)", MosaicSize.Large),
            };
            foreach (var (label, size) in sizes)
            {
                var btn = MakeSmallButton(label, _activeTool is MosaicTool mt && mt.MosaicSize == size);
                var localSize = size;
                btn.Click += (_, _) =>
                {
                    if (_activeTool is MosaicTool mt2) mt2.MosaicSize = localSize;
                    BuildSubOptions("모자이크");
                };
                SubOptionPanel.Children.Add(btn);
            }
        }
        else if (toolName == "도형")
        {
            var shapes = new (string label, ShapeType type)[]
            {
                ("□ 사각형", ShapeType.Rectangle),
                ("▢ 둥근사각형", ShapeType.RoundedRect),
                ("○ 원", ShapeType.Ellipse),
                ("△ 삼각형", ShapeType.Triangle),
                ("◇ 다이아몬드", ShapeType.Diamond),
                ("☆ 별", ShapeType.Star),
                ("─ 직선", ShapeType.Line),
                ("┄ 점선", ShapeType.DashedLine),
            };

            foreach (var (label, type) in shapes)
            {
                var btn = new Button
                {
                    Content = label, FontSize = 11, Padding = new Thickness(6, 3, 6, 3),
                    Margin = new Thickness(1), Cursor = Cursors.Hand,
                    Background = type == _selectedShapeType
                        ? (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBrush")
                        : System.Windows.Media.Brushes.Transparent,
                    BorderBrush = type == _selectedShapeType
                        ? (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBorderBrush")
                        : System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(1)
                };
                var localType = type;
                btn.Click += (_, _) =>
                {
                    _selectedShapeType = localType;
                    if (_activeTool is ShapeTool st) st.SelectedShape = localType;
                    BuildSubOptions("도형");
                };
                SubOptionPanel.Children.Add(btn);
            }

            // 구분선
            SubOptionPanel.Children.Add(new Border
            {
                Width = 1, Height = 20, Margin = new Thickness(6, 0, 6, 0),
                Background = (System.Windows.Media.Brush)FindResource("DividerBrush")
            });

            // 채우기 모드
            var fills = new (string label, FillMode mode)[]
            {
                ("없음", FillMode.None),
                ("채우기", FillMode.Solid),
                ("반투명", FillMode.SemiTransparent),
            };
            foreach (var (label, mode) in fills)
            {
                var btn = new Button
                {
                    Content = label, FontSize = 11, Padding = new Thickness(6, 3, 6, 3),
                    Margin = new Thickness(1), Cursor = Cursors.Hand,
                    Background = mode == _selectedFillMode
                        ? (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBrush")
                        : System.Windows.Media.Brushes.Transparent,
                    BorderBrush = mode == _selectedFillMode
                        ? (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBorderBrush")
                        : System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(1)
                };
                var localMode = mode;
                btn.Click += (_, _) =>
                {
                    _selectedFillMode = localMode;
                    if (_activeTool is ShapeTool st) st.SelectedFill = localMode;
                    BuildSubOptions("도형");
                };
                SubOptionPanel.Children.Add(btn);
            }
        }
        else if (toolName == "화살표")
        {
            var heads = new (string label, ArrowHeadStyle style)[]
            {
                ("▷ 화살표", ArrowHeadStyle.Arrow),
                ("● 지시선", ArrowHeadStyle.Circle),
                ("◆ 다이아", ArrowHeadStyle.Diamond),
                ("─ 없음", ArrowHeadStyle.None),
            };

            foreach (var (label, style) in heads)
            {
                var btn = new Button
                {
                    Content = label, FontSize = 11, Padding = new Thickness(6, 3, 6, 3),
                    Margin = new Thickness(1), Cursor = Cursors.Hand,
                    Background = style == _selectedArrowHead
                        ? (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBrush")
                        : System.Windows.Media.Brushes.Transparent,
                    BorderBrush = style == _selectedArrowHead
                        ? (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBorderBrush")
                        : System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(1)
                };
                var localStyle = style;
                btn.Click += (_, _) =>
                {
                    _selectedArrowHead = localStyle;
                    if (_activeTool is ArrowTool at) at.SelectedHeadStyle = localStyle;
                    BuildSubOptions("화살표");
                };
                SubOptionPanel.Children.Add(btn);
            }

            // 구분선
            SubOptionPanel.Children.Add(new Border
            {
                Width = 1, Height = 20, Margin = new Thickness(6, 0, 6, 0),
                Background = (System.Windows.Media.Brush)FindResource("DividerBrush")
            });

            // 선 스타일
            var lines = new (string label, ArrowLineStyle style)[]
            {
                ("── 실선", ArrowLineStyle.Solid),
                ("╌╌ 파선", ArrowLineStyle.Dashed),
                ("··· 점선", ArrowLineStyle.Dotted),
                ("─·─ 일점쇄선", ArrowLineStyle.DashDot),
                ("─··─ 이점쇄선", ArrowLineStyle.DashDotDot),
            };

            foreach (var (label, style) in lines)
            {
                var btn = new Button
                {
                    Content = label, FontSize = 11, Padding = new Thickness(6, 3, 6, 3),
                    Margin = new Thickness(1), Cursor = Cursors.Hand,
                    Background = style == _selectedArrowLine
                        ? (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBrush")
                        : System.Windows.Media.Brushes.Transparent,
                    BorderBrush = style == _selectedArrowLine
                        ? (System.Windows.Media.Brush)FindResource("ToolbarButtonActiveBorderBrush")
                        : System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(1)
                };
                var localStyle = style;
                btn.Click += (_, _) =>
                {
                    _selectedArrowLine = localStyle;
                    if (_activeTool is ArrowTool at) at.SelectedLineStyle = localStyle;
                    BuildSubOptions("화살표");
                };
                SubOptionPanel.Children.Add(btn);
            }
        }
        else if (toolName == "지우개")
        {
            SubOptionPanel.Children.Add(MakeSectionLabel("지우개 모드"));
            var modes = new (string label, EraserMode mode)[]
            {
                ("🖱 개체 지우개", EraserMode.Object),
                ("▭ 영역 지우개", EraserMode.Area),
            };
            foreach (var (label, mode) in modes)
            {
                var btn = MakeSmallButton(label, _eraserMode == mode);
                btn.Click += (_, _) =>
                {
                    _eraserMode = mode;
                    if (_activeTool is EraserTool et) et.Mode = mode;
                    BuildSubOptions("지우개");
                };
                SubOptionPanel.Children.Add(btn);
            }
        }
    }

    private EraserMode _eraserMode = EraserMode.Object;

    private void UpdateStatus()
    {
        if (_sourceImage != null)
            StatusText.Text = $"{_sourceImage.PixelWidth} × {_sourceImage.PixelHeight} px · PNG";

        // 편집 개체가 있으면 크롭 버튼 비활성화
        if (_toolButtons.TryGetValue("크롭", out var cropBtn))
        {
            cropBtn.IsEnabled = _objects.Count == 0;
            cropBtn.Opacity = _objects.Count == 0 ? 1.0 : 0.4;
        }
    }

    private void UpdateZoomDisplay(double zoomRatio)
    {
        ZoomText.Text = $"{zoomRatio * 100:0}%";
    }

    // ── 캡쳐 기록 ──────────────────────────────────────────────

    private static void AddToHistory(BitmapSource image)
    {
        _captureHistory.Insert(0, image);
        while (_captureHistory.Count > MaxHistory)
            _captureHistory.RemoveAt(_captureHistory.Count - 1);
    }

    private void BuildHistory()
    {
        HistoryPanel.Children.Clear();
        for (int i = 0; i < _captureHistory.Count; i++)
        {
            var img = _captureHistory[i];
            var thumb = new System.Windows.Controls.Image
            {
                Source = img,
                Stretch = System.Windows.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = $"{img.PixelWidth}×{img.PixelHeight}\n좌클릭: 열기 | 우클릭: 복사"
            };

            bool isSelected = (img == _sourceImage);
            var border = new Border
            {
                Width = 100,
                Height = 100,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF0, 0xF0, 0xF0)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                BorderBrush = isSelected
                    ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                    : (System.Windows.Media.Brush)FindResource("DividerBrush"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 0, 0, 4),
                ClipToBounds = true,
                Child = thumb
            };

            var localImg = img;
            border.MouseDown += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    LoadFromHistory(localImg);
            };

            // 우클릭 컨텍스트 메뉴
            var ctx = new ContextMenu();

            var miOpen = new MenuItem { Header = "열기" };
            miOpen.Click += (_, _) => LoadFromHistory(localImg);
            ctx.Items.Add(miOpen);

            var miCopy = new MenuItem { Header = "클립보드 복사" };
            miCopy.Click += (_, _) =>
            {
                System.Windows.Clipboard.SetImage(localImg);
                StatusText.Text = "클립보드에 복사됨";
            };
            ctx.Items.Add(miCopy);

            var miSave = new MenuItem { Header = "다른 이름으로 저장" };
            miSave.Click += (_, _) =>
            {
                var bmp = BitmapHelper.ToBitmap(localImg);
                var path = _saveManager.SaveAs(bmp);
                bmp.Dispose();
                if (path != null) StatusText.Text = $"저장됨: {path}";
            };
            ctx.Items.Add(miSave);

            var miQuickSave = new MenuItem { Header = "빠른 저장 (자동경로)" };
            miQuickSave.Click += (_, _) =>
            {
                var bmp = BitmapHelper.ToBitmap(localImg);
                var path = _saveManager.SaveAuto(bmp, _settings);
                bmp.Dispose();
                StatusText.Text = $"저장됨: {path}";
            };
            ctx.Items.Add(miQuickSave);

            ctx.Items.Add(new Separator());

            var miDelete = new MenuItem { Header = "기록에서 삭제" };
            miDelete.Click += (_, _) =>
            {
                _captureHistory.Remove(localImg);
                _captureObjects.Remove(localImg);
                if (localImg == _sourceImage && _captureHistory.Count > 0)
                    LoadFromHistory(_captureHistory[0]);
                else
                    BuildHistory();
                StatusText.Text = "캡쳐 기록 삭제됨";
            };
            ctx.Items.Add(miDelete);

            border.ContextMenu = ctx;

            HistoryPanel.Children.Add(border);
        }
    }

    private void OnHistorySaveAll(object sender, RoutedEventArgs e)
    {
        if (_captureHistory.Count == 0) { StatusText.Text = "저장할 기록이 없습니다"; return; }

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "캡쳐 기록을 저장할 폴더 선택",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        int saved = 0;
        for (int i = 0; i < _captureHistory.Count; i++)
        {
            var img = _captureHistory[i];
            var bmp = BitmapHelper.ToBitmap(img);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var path = Path.Combine(dialog.SelectedPath, $"신캡쳐_{timestamp}_{i + 1}.png");
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            bmp.Dispose();
            saved++;
        }
        StatusText.Text = $"{saved}개 이미지 저장됨: {dialog.SelectedPath}";
    }

    private void OnHistoryClear(object sender, RoutedEventArgs e)
    {
        _captureHistory.Clear();
        BuildHistory();
        StatusText.Text = "캡쳐 기록 비움";
    }

    private void DeleteSelectedObjects()
    {
        var selected = _objects.Where(o => o.IsSelected).ToList();
        foreach (var obj in selected)
            _commandStack.Execute(new RemoveObjectCommand(_objects, obj));
        Canvas.InvalidateVisual();
    }

    private void PasteImageFromClipboard()
    {
        try
        {
            BitmapSource? clipImg = null;

            // 1. 표준 이미지 형식
            if (System.Windows.Clipboard.ContainsImage())
                clipImg = System.Windows.Clipboard.GetImage();

            // 2. PNG 스트림 (브라우저 등에서 복사 시)
            if (clipImg == null)
            {
                var data = System.Windows.Clipboard.GetDataObject();
                if (data != null)
                {
                    foreach (var fmt in new[] { "PNG", "image/png" })
                    {
                        if (data.GetDataPresent(fmt) && data.GetData(fmt) is Stream pngStream)
                        {
                            pngStream.Position = 0;
                            var decoder = new PngBitmapDecoder(pngStream,
                                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            if (decoder.Frames.Count > 0) { clipImg = decoder.Frames[0]; break; }
                        }
                    }
                }
            }

            // 3. 파일 복사 (탐색기에서 이미지 파일 복사)
            if (clipImg == null && System.Windows.Clipboard.ContainsFileDropList())
            {
                var files = System.Windows.Clipboard.GetFileDropList();
                foreach (string? f in files)
                {
                    if (f == null) continue;
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp")
                    {
                        clipImg = new BitmapImage(new Uri(f));
                        break;
                    }
                }
            }

            if (clipImg == null) return;

            // 캔버스 중앙에 배치
            double imgW = clipImg.PixelWidth;
            double imgH = clipImg.PixelHeight;
            double canvasW = _sourceImage.PixelWidth;
            double canvasH = _sourceImage.PixelHeight;

            // 이미지가 캔버스보다 크면 축소
            double scale = Math.Min(1.0, Math.Min(canvasW * 0.8 / imgW, canvasH * 0.8 / imgH));
            double w = imgW * scale;
            double h = imgH * scale;

            var obj = new ImageObject
            {
                Source = clipImg,
                Position = new System.Windows.Point((canvasW - w) / 2, (canvasH - h) / 2),
                Width = w,
                Height = h
            };

            _commandStack.Execute(new AddObjectCommand(_objects, obj));

            // 선택 도구로 전환하여 바로 이동/크기 조절 가능
            SelectTool("선택");
            Canvas.InvalidateVisual();
            StatusText.Text = $"이미지 붙여넣기 ({clipImg.PixelWidth}×{clipImg.PixelHeight})";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"붙여넣기 실패: {ex.Message}";
        }
    }

    private EyedropperTool CreateEyedropper()
    {
        var tool = new EyedropperTool(_sourceImage);
        tool.ColorPicked += color =>
        {
            _currentColor = color;
            if (_activeTool != null) _activeTool.CurrentColor = color;
            StatusText.Text = $"색상 선택: #{color.R:X2}{color.G:X2}{color.B:X2}";
            // 이전 도구로 자동 복귀
            if (_lastToolName != null && _lastToolName != "색상추출")
                SelectTool(_lastToolName);
        };
        return tool;
    }

    private string? _lastToolName;

    /// <summary>컬러 피커 다이얼로그 (System.Windows.Forms)</summary>
    private void OpenColorPicker()
    {
        using var dlg = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(_currentColor.R, _currentColor.G, _currentColor.B),
            CustomColors = _settings.CustomColors.Length > 0 ? _settings.CustomColors : null
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dlg.Color;
            _currentColor = System.Windows.Media.Color.FromRgb(c.R, c.G, c.B);
            if (_activeTool != null) _activeTool.CurrentColor = _currentColor;

            // 사용자 지정색 저장 (영구 보존)
            _settings.CustomColors = dlg.CustomColors ?? Array.Empty<int>();
            _settingsManager?.Save(_settings);

            BuildContextProperties(GetCurrentToolDisplayName());
        }
    }

    /// <summary>컬러 피커 미니 버튼 (무지개 원 아이콘)</summary>
    private Border MakePickerButton(Action<System.Windows.Media.Color> onPicked)
    {
        var btn = new Border
        {
            Width = 18, Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = new LinearGradientBrush(
                new GradientStopCollection {
                    new GradientStop(Colors.Red, 0), new GradientStop(Colors.Yellow, 0.25),
                    new GradientStop(Colors.Lime, 0.5), new GradientStop(Colors.Cyan, 0.75),
                    new GradientStop(Colors.Blue, 1)
                }, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0)),
            Margin = new Thickness(3, 0, 0, 0),
            Cursor = Cursors.Hand,
            ToolTip = "색상 선택 (전체 팔레트)"
        };
        btn.MouseDown += (_, _) =>
        {
            using var dlg = new System.Windows.Forms.ColorDialog
            {
                FullOpen = true,
                CustomColors = _settings.CustomColors.Length > 0 ? _settings.CustomColors : null
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var dc = dlg.Color;
                onPicked(System.Windows.Media.Color.FromRgb(dc.R, dc.G, dc.B));
                _settings.CustomColors = dlg.CustomColors ?? Array.Empty<int>();
                _settingsManager?.Save(_settings);
            }
        };
        return btn;
    }

    private string GetCurrentToolDisplayName() => _activeTool?.Name switch
    {
        "Pen" => "펜", "Highlighter" => "형광펜", "Shape" => "도형",
        "Arrow" => "화살표", "Text" => "텍스트", "Balloon" => "말풍선",
        "Number" => "번호", "Select" => "선택", "Eraser" => "지우개",
        _ => "펜"
    };

    private void OnMicroAdClick(object sender, MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "mailto:popolong@naver.com?subject=신캡쳐 문의/제휴",
            UseShellExecute = true
        });
    }

    private void OnHistoryBannerClick(object sender, MouseButtonEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://contents.premium.naver.com/market/ai",
            UseShellExecute = true
        });
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
                obj.RenderWithTransform(dc);
        }
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        return BitmapHelper.ToBitmap(rtb);
    }

    private void SetStatus(string text)
    {
        if (StatusText != null) StatusText.Text = text;
    }

    private async void RunEditorOcr()
    {
        if (_sourceImage == null)
        {
            SetStatus("OCR: 이미지가 없습니다");
            return;
        }

        // 이미 열려있는 편집기가 구식 _settings 객체를 잡고 있을 수 있음 → 디스크에서 최신 설정 로드
        var currentSettings = _settingsManager?.Load() ?? _settings;

        SetStatus("OCR 실행 중...");
        OcrPanel.Visibility = Visibility.Visible;
        OcrPanelTitle.Text = "추출된 텍스트 (추출 중…)";
        OcrTextBox.Text = "";

        try
        {
            var langTag = ShinCapture.Services.OcrService.ResolveLanguageOrFallback(currentSettings.Ocr.Language);
            if (langTag == null)
            {
                OcrPanelTitle.Text = "OCR 언어팩이 필요합니다";
                OcrTextBox.Text =
                    $"설정된 언어({currentSettings.Ocr.Language})의 OCR 언어팩이 설치되어 있지 않습니다.\n" +
                    "Windows 설정 > 시간 및 언어 > 언어에서 언어팩을 설치한 뒤 다시 시도해주세요.";
                SetStatus("OCR 언어팩 없음");
                return;
            }

            using var bmp = BitmapSourceToBitmap(_sourceImage);
            var text = await ShinCapture.Services.OcrService.ExtractTextAsync(
                bmp, langTag, currentSettings.Ocr.UpscaleSmallImages);

            if (string.IsNullOrWhiteSpace(text))
            {
                OcrPanelTitle.Text = "추출된 텍스트 없음";
                OcrTextBox.Text = "";
                SetStatus("OCR: 텍스트를 찾지 못했습니다");
                return;
            }

            OcrTextBox.Text = text;
            var tagNote = string.Equals(langTag, currentSettings.Ocr.Language, StringComparison.OrdinalIgnoreCase)
                ? "" : $" — {langTag} 폴백";
            OcrPanelTitle.Text = $"추출된 텍스트 ({text.Length}자{tagNote})";
            SetStatus($"OCR 완료 ({text.Length}자)");
        }
        catch (Exception ex)
        {
            OcrPanelTitle.Text = "OCR 실패";
            OcrTextBox.Text = ex.Message;
            SetStatus("OCR 실패");
        }
    }

    private static System.Drawing.Bitmap BitmapSourceToBitmap(System.Windows.Media.Imaging.BitmapSource source)
    {
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
        using var ms = new System.IO.MemoryStream();
        encoder.Save(ms);
        ms.Position = 0;
        return new System.Drawing.Bitmap(ms);
    }

    private void OnOcrClose(object sender, RoutedEventArgs e)
    {
        OcrPanel.Visibility = Visibility.Collapsed;
    }

    private void OnOcrSelectAll(object sender, RoutedEventArgs e)
    {
        OcrTextBox.SelectAll();
        OcrTextBox.Focus();
    }

    private void OnOcrCopy(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(OcrTextBox.Text)) return;
        System.Windows.Clipboard.SetText(OcrTextBox.Text);
        SetStatus($"텍스트 복사됨 ({OcrTextBox.Text.Length}자)");
    }
}
