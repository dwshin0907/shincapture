using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using ShinCapture.Editor;
using ShinCapture.Editor.Tools;
using ShinCapture.Editor.Objects;
using ShinCapture.Helpers;
using ShinCapture.Models;
using ShinCapture.Services;
using ShinCapture.Views.Controls;

namespace ShinCapture.Views;

public partial class EditorWindow : Window
{
    private readonly CommandStack _commandStack = new();
    private readonly List<EditorObject> _objects = new();
    private readonly SaveManager _saveManager;
    private readonly AppSettings _settings;
    private readonly SettingsManager? _settingsManager;
    private readonly EditorOcrService _ocrService = new();
    private BitmapSource _sourceImage;
    private Bitmap _sourceBitmap;
    private ITool? _activeTool;
    private readonly Dictionary<string, Button> _toolButtons = new();
    private readonly Dictionary<string, TextBlock> _toolLabels = new();
    private EditorChromeMode? _lastChromeMode;
    private bool? _historyPanelVisibilityOverride;
    private static readonly TrayIconGeometryConverter IconConverter = new();

    // 캡쳐 기록 (세션 동안 유지, 최대 50개)
    private static readonly List<BitmapSource> _captureHistory = new();
    private const int MaxHistory = 50;
    private readonly Dictionary<BitmapSource, Border> _historyCards = new();

    /// <summary>편집기에서 새 캡쳐를 요청. (mode, autoTranslate)</summary>
    public event Action<Models.CaptureMode, bool>? CaptureRequested;

    private bool _pendingAutoTranslate;

    // 캡쳐별 편집 상태 보존
    private readonly Dictionary<BitmapSource, List<EditorObject>> _captureObjects = new();
    private EditorWindowSizeMode? _appliedWindowSizeMode;
    private bool _applyingWindowSizingPolicy;
    private HwndSource? _hwndSource;

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
        SourceInitialized += OnSourceInitialized;
        Closed += OnEditorClosed;
        Loaded += (_, _) =>
        {
            UpdateLayout(); // chrome 측정 강제 (ActualWidth/CanvasScroller.ActualWidth 보장)
            ApplyWindowSizingPolicy(imageChanged: true);
            UpdateLayout();
            Canvas.BackgroundImage = _sourceImage;
            Canvas.ApplyInitialZoom();
            UpdateChromeLayout();
        };

        SizeChanged += (_, _) => UpdateChromeLayout();
    }

    private void UpdateChromeLayout()
    {
        if (HistoryBorder == null) return;

        EditorChromeLayout layout = EditorChromeLayoutPolicy.Resolve(ActualWidth);
        if (_lastChromeMode != layout.Mode)
        {
            _historyPanelVisibilityOverride = null;
            _lastChromeMode = layout.Mode;
        }

        foreach (TextBlock label in _toolLabels.Values)
            label.Visibility = layout.ShowToolLabels ? Visibility.Visible : Visibility.Collapsed;

        bool showHistory = _historyPanelVisibilityOverride ?? layout.ShowHistoryByDefault;
        HistoryBorder.Width = showHistory ? 180 : 0;
        HistoryBorder.Visibility = showHistory ? Visibility.Visible : Visibility.Collapsed;
        if (HistoryToggleBtn != null)
            HistoryToggleBtn.Style = (Style)FindResource(showHistory ? "ToolbarButtonActive" : "EditorIconButton");
    }

    private void SizeWindowToImage()
    {
        if (_sourceImage == null) return;

        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        double dpiScale = dpi.PixelsPerDip;

        // 이미지의 논리 크기 (WPF DIPs)
        double imgLogicalW = _sourceImage.PixelWidth / dpiScale;
        double imgLogicalH = _sourceImage.PixelHeight / dpiScale;

        // chrome(툴바/속성바/상태바/히스토리 패널) 사이즈를 ScrollViewer 기준으로 계산.
        // Width property가 아닌 ActualWidth를 사용 — Width는 NaN(auto) 또는 set값으로 동기화 지연 가능.
        // ActualWidth는 항상 측정값이라 안전.
        double winW = ActualWidth > 0 ? ActualWidth : Width;
        double winH = ActualHeight > 0 ? ActualHeight : Height;
        double svW = CanvasScroller?.ActualWidth ?? 0;
        double svH = CanvasScroller?.ActualHeight ?? 0;
        double chromeW = winW - svW;
        double chromeH = winH - svH;
        if (svW <= 0 || chromeW < 0 || chromeW > 400) chromeW = 160; // 히스토리 패널(140) + 보더 약간
        if (svH <= 0 || chromeH < 0 || chromeH > 400) chromeH = 130; // 툴바+속성+상태바 추정

        // 패딩 (상하좌우 20px)
        double padding = 40;

        double desiredW = imgLogicalW + chromeW + padding;
        double desiredH = imgLogicalH + chromeH + padding;

        // 화면 크기 제한
        MonitorWorkArea workArea = MonitorWorkAreaService.GetForWindow(this);
        double maxW = workArea.DipWidth;
        double maxH = workArea.DipHeight;

        // 최소 보장 — 작은 캡쳐일 때 윈도우가 너무 좁으면 툴바(WrapPanel)가 세로로 wrap되어
        // ScrollViewer 영역이 줄어들고 결과적으로 캡쳐가 안 보이는 문제 방지.
        // 작업영역 50% 기준으로 minimum, 단 와이드/울트라와이드(3440w+)에서 윈도우가 과도하게
        // 커지지 않도록 XAML 디자인 사이즈(1100x750)로 cap.
        double minW = Math.Min(maxW * 0.5, 1100);
        double minH = Math.Min(maxH * 0.5, 750);

        Width = Math.Min(Math.Max(desiredW, minW), maxW);
        Height = Math.Min(Math.Max(desiredH, minH), maxH);

        // 화면 중앙 배치
        _ = MonitorWorkAreaService.CenterWindow(this, workArea);
    }

    public void RefreshWindowSizingPolicy()
    {
        if (EditorWindowSizingPolicy.ShouldDeferRefresh(IsLoaded, IsVisible))
            return;

        EditorWindowSizeMode? previousMode = _appliedWindowSizeMode;
        UpdateLayout();
        bool viewportOrStateChanged = ApplyWindowSizingPolicy(imageChanged: false);
        UpdateLayout();

        if (previousMode != _appliedWindowSizeMode &&
            OcrPanel.Visibility == Visibility.Visible)
        {
            _editorHeightBeforeOcr =
                _appliedWindowSizeMode is EditorWindowSizeMode currentMode &&
                EditorWindowSizingPolicy.ShouldGrowForOcr(currentMode)
                    ? EditorWindowSizingPolicy.CalculateHeightBeforeOcr(
                        Height,
                        OcrPanel.ActualHeight)
                    : -1;
        }

        if (viewportOrStateChanged)
            Canvas.ApplyInitialZoom();
    }

    private EditorSettings CurrentEditorSettings() =>
        (_settingsManager?.Load() ?? _settings).Editor ?? new EditorSettings();

    private bool ApplyWindowSizingPolicy(bool imageChanged)
    {
        EditorSettings editorSettings = CurrentEditorSettings();
        EditorWindowSizeMode mode = editorSettings.WindowSizeMode;
        bool changedMode = _appliedWindowSizeMode != mode;
        bool viewportOrStateChanged = false;

        _applyingWindowSizingPolicy = true;
        try
        {
            if (EditorWindowSizingPolicy.ShouldMaximize(mode))
            {
                if (WindowState != WindowState.Maximized)
                {
                    WindowState = WindowState.Maximized;
                    viewportOrStateChanged = true;
                }
            }
            else if (EditorWindowSizingPolicy.ShouldFitToCapture(mode))
            {
                if (WindowState != WindowState.Normal)
                {
                    WindowState = WindowState.Normal;
                    viewportOrStateChanged = true;
                }

                if (imageChanged || changedMode)
                {
                    SizeWindowToImage();
                    viewportOrStateChanged = true;
                }
            }
            else if (EditorWindowSizingPolicy.ShouldApplyRememberedSize(
                         _appliedWindowSizeMode,
                         mode))
            {
                if (WindowState != WindowState.Normal)
                    WindowState = WindowState.Normal;

                MonitorWorkArea workArea = MonitorWorkAreaService.GetForWindow(this);
                EditorWindowSize size = EditorWindowSizingPolicy.NormalizeRememberedSize(
                    editorSettings.WindowWidth,
                    editorSettings.WindowHeight,
                    workArea.DipWidth,
                    workArea.DipHeight);
                Width = size.Width;
                Height = size.Height;
                _ = MonitorWorkAreaService.CenterWindow(this, workArea);
                viewportOrStateChanged = true;
            }

            _appliedWindowSizeMode = mode;
            return viewportOrStateChanged;
        }
        finally
        {
            _applyingWindowSizingPolicy = false;
        }
    }

    /// <summary>기존 에디터에 새 캡쳐를 로드 (창을 재사용)</summary>
    /// <param name="autoOcr">true이면 로드 직후 OCR을 자동 실행 (Translate 모드 전용)</param>
    /// <param name="autoTranslate">true이면 OCR 직후 자동 번역 실행 (번역 버튼 흐름 전용)</param>
    public void LoadNewCapture(Bitmap capturedImage, bool autoOcr = false, bool autoTranslate = false)
    {
        // 현재 편집 상태 저장
        SaveCurrentObjects();

        _sourceBitmap = capturedImage;
        _sourceImage = BitmapHelper.ToBitmapSource(capturedImage);

        AddToHistory(_sourceImage);

        _objects.Clear();
        _commandStack.Clear();
        _activeTool?.Reset();

        // Window가 Hidden이면 먼저 visible로. SizeWindowToImage의 chrome 측정과
        // ScrollViewer ViewportWidth 갱신은 visible 상태에서만 정확.
        // (Hidden 상태에서 ActualWidth는 0 또는 직전 값 → fit zoom 38% 같은 잘못된 값으로 계산됨.)
        if (!IsVisible) Show();

        // Show() 직후 layout pass가 동기 보장되지 않으므로 UpdateLayout()으로 측정 강제 →
        // 그 후에야 SizeWindowToImage가 정확한 chrome(=ActualWidth-CanvasScroller.ActualWidth)으로
        // 윈도우 사이즈를 결정함. 이 한 줄 없으면 chrome=160 fallback으로 윈도우가 작게 결정되고
        // viewport가 좁아 fit zoom이 38%/42% 같은 작은 값이 나옴.
        UpdateLayout();
        ApplyWindowSizingPolicy(imageChanged: true);
        UpdateLayout();
        Canvas.BackgroundImage = _sourceImage;
        Canvas.ApplyInitialZoom();
        BuildHistory();
        UpdateStatus();

        // 새 캡쳐에는 이전 OCR 결과 숨김
        if (OcrPanel != null)
        {
            OcrPanel.Visibility = Visibility.Collapsed;
            OcrTextBox.Text = "";
            OcrPanelTitle.Text = "텍스트 추출";
            OcrPanelMeta.Text = "";
            OcrSourceLangLabel.Text = "";
            OcrTranslatedBox.Text = "";
            OcrTargetLangLabel.Text = "";
            if (OcrTranslatedPlaceholder != null)
                OcrTranslatedPlaceholder.Visibility = Visibility.Visible;
        }

        if (autoOcr)
        {
            _pendingAutoTranslate = autoTranslate;
            Dispatcher.BeginInvoke(new Action(RunEditorOcr),
                System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    /// <summary>외부(MainWindow)에서 OCR 자동 실행을 트리거할 때 사용 (새 EditorWindow 생성 시)</summary>
    public void TriggerAutoOcr() => TriggerAutoOcr(false);

    /// <summary>autoTranslate 플래그를 함께 지정하여 OCR 자동 실행</summary>
    public void TriggerAutoOcr(bool autoTranslate)
    {
        _pendingAutoTranslate = autoTranslate;
        RunEditorOcr();
    }

    /// <summary>기록에서 이미지를 메인 화면에 로드</summary>
    private void LoadFromHistory(BitmapSource image, bool focusHistoryItem = false)
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

        if (!IsVisible) Show();
        UpdateLayout();
        ApplyWindowSizingPolicy(imageChanged: true);
        UpdateLayout();
        Canvas.BackgroundImage = _sourceImage;
        Canvas.ApplyInitialZoom();
        BuildHistory();
        if (focusHistoryItem)
            ScheduleHistoryFocus(image);
        UpdateStatus();
    }

    private void ScheduleHistoryFocus(BitmapSource image)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (image != _sourceImage || !_historyCards.TryGetValue(image, out var card))
                return;

            card.Focus();
            card.BringIntoView();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void SaveCurrentObjects()
    {
        if (_sourceImage != null)
            _captureObjects[_sourceImage] = new List<EditorObject>(_objects);
    }

    private bool _forceClose;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _hwndSource?.AddHook(EditorWindowProc);
    }

    private void OnEditorClosed(object? sender, EventArgs e)
    {
        _hwndSource?.RemoveHook(EditorWindowProc);
        _hwndSource = null;
    }

    private IntPtr EditorWindowProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (msg == NativeMethods.WM_EXITSIZEMOVE)
            PersistUserWindowSize();

        return IntPtr.Zero;
    }

    private void PersistUserWindowSize()
    {
        if (_applyingWindowSizingPolicy || WindowState != WindowState.Normal)
            return;
        if (CurrentEditorSettings().WindowSizeMode != EditorWindowSizeMode.RememberLast)
            return;

        Rect bounds = RestoreBounds;
        if (!EditorWindowSizingPolicy.IsValidPersistedSize(bounds.Width, bounds.Height))
            return;

        try
        {
            _settings.Editor ??= new EditorSettings();
            _settings.Editor.WindowWidth = bounds.Width;
            _settings.Editor.WindowHeight = bounds.Height;
            _settingsManager?.Update(settings =>
            {
                settings.Editor ??= new EditorSettings();
                settings.Editor.WindowWidth = bounds.Width;
                settings.Editor.WindowHeight = bounds.Height;
            }, raiseChanged: false);
        }
        catch
        {
            // Window resizing must remain usable even if settings persistence fails.
        }
    }

    /// <summary>X 버튼 ▸ 숨기기 (편집 상태 유지). 앱 종료 시에만 ForceClose.</summary>
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
        if ((e.Key is Key.Up or Key.Down) && HistoryPanel.IsKeyboardFocusWithin)
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
                NavigateCaptureHistory(e.Key);

            e.Handled = true;
            return;
        }

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

    private void NavigateCaptureHistory(Key key)
    {
        BitmapSource currentImage =
            (Keyboard.FocusedElement as FrameworkElement)?.Tag as BitmapSource
            ?? _sourceImage;
        int currentIndex = _captureHistory.IndexOf(currentImage);
        CaptureHistoryDirection direction = key == Key.Up
            ? CaptureHistoryDirection.Up
            : CaptureHistoryDirection.Down;
        int targetIndex = CaptureHistoryNavigationPolicy.GetTargetIndex(
            currentIndex,
            _captureHistory.Count,
            direction);

        if (targetIndex >= 0 && targetIndex != currentIndex)
            LoadFromHistory(_captureHistory[targetIndex], focusHistoryItem: true);
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
        string? lastGroup = null;
        foreach (EditorToolDescriptor tool in EditorToolbarCatalog.Tools)
        {
            if (lastGroup != null && lastGroup != tool.Group)
                ToolbarPanel.Children.Add(CreateSeparator());

            Button btn = CreateToolButton(tool);
            btn.Click += (_, _) => SelectTool(tool.Name);
            ToolbarPanel.Children.Add(btn);
            _toolButtons[tool.Name] = btn;
            lastGroup = tool.Group;
        }

        UtilityCommandPanel.Children.Add(CreateAiMenuButton());
        UtilityCommandPanel.Children.Add(CreateSeparator());

        Button undoBtn = CreateIconCommandButton("undo", "실행취소 (Ctrl+Z)");
        undoBtn.Click += (_, _) => _commandStack.Undo();
        UtilityCommandPanel.Children.Add(undoBtn);

        Button redoBtn = CreateIconCommandButton("redo", "다시실행 (Ctrl+Y)");
        redoBtn.Click += (_, _) => _commandStack.Redo();
        UtilityCommandPanel.Children.Add(redoBtn);

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
                Canvas.ApplyInitialZoom();
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

    private Button CreateToolButton(EditorToolDescriptor tool)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(CreateIconPath(tool.IconKey, 19));

        var label = new TextBlock
        {
            Text = tool.Name,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(label);
        _toolLabels[tool.Name] = label;

        var button = new Button
        {
            Content = panel,
            Style = (Style)FindResource("ToolbarButton"),
            Margin = new Thickness(1, 0, 1, 0),
            ToolTip = tool.ToolTip
        };
        AutomationProperties.SetName(button, tool.ToolTip);
        return button;
    }

    private Button CreateIconCommandButton(string iconKey, string accessibleName)
    {
        var button = new Button
        {
            Content = CreateIconPath(iconKey, 18),
            Style = (Style)FindResource("EditorIconButton"),
            Margin = new Thickness(1, 0, 1, 0),
            ToolTip = accessibleName
        };
        AutomationProperties.SetName(button, accessibleName);
        return button;
    }

    private Button CreateAiMenuButton()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(CreateIconPath("ai", 18));
        panel.Children.Add(new TextBlock
        {
            Text = "AI 도구",
            Margin = new Thickness(6, 0, 2, 0),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        var menu = new ContextMenu { Placement = PlacementMode.Bottom };
        menu.Items.Add(CreateActionMenuItem("텍스트 추출", "text", (_, _) => RunEditorOcr()));
        menu.Items.Add(CreateActionMenuItem("번역 캡처", "translate", (_, _) => RequestTranslateCapture()));
        menu.Items.Add(CreateActionMenuItem("스마트 컷", "spark", (_, _) => RequestSmartCutCapture()));

        var button = new Button
        {
            Content = panel,
            ContextMenu = menu,
            Style = (Style)FindResource("EditorCommandButton"),
            Background = (System.Windows.Media.Brush)FindResource("AccentSurfaceBrush"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBorderBrush"),
            Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush"),
            Margin = new Thickness(2, 0, 2, 0),
            ToolTip = "텍스트 추출, 번역 캡처, 스마트 컷"
        };
        AutomationProperties.SetName(button, "AI 도구 메뉴");
        button.Click += (_, _) =>
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        };
        return button;
    }

    private MenuItem CreateActionMenuItem(
        string label,
        string iconKey,
        RoutedEventHandler onClick)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(CreateIconPath(iconKey, 18));
        panel.Children.Add(new TextBlock
        {
            Text = label,
            Margin = new Thickness(9, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        var item = new MenuItem { Header = panel };
        item.Click += onClick;
        AutomationProperties.SetName(item, label);
        return item;
    }

    private static System.Windows.Shapes.Path CreateIconPath(string iconKey, double size)
    {
        Geometry geometry = (Geometry)IconConverter.Convert(
            iconKey,
            typeof(Geometry),
            parameter: null!,
            System.Globalization.CultureInfo.InvariantCulture);

        return new System.Windows.Shapes.Path
        {
            Data = geometry,
            Stroke = (System.Windows.Media.Brush)Application.Current.FindResource("TextSecondaryBrush"),
            StrokeThickness = 1.8,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Width = size,
            Height = size,
            Stretch = System.Windows.Media.Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
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

    private void RequestTranslateCapture()
    {
        CaptureRequested?.Invoke(Models.CaptureMode.Translate, /* autoTranslate */ true);
    }

    private void RequestSmartCutCapture()
    {
        CaptureRequested?.Invoke(Models.CaptureMode.SmartCut, /* autoTranslate */ false);
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
            var currentSwatch = new Button
            {
                Width = 30, Height = 30,
                Style = (Style)FindResource("EditorIconButton"),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}",
                Content = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(_currentColor),
                    BorderThickness = new Thickness(2),
                    BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush")
                }
            };
            AutomationProperties.SetName(currentSwatch, $"현재 색상 #{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}, 색상 선택 열기");
            currentSwatch.Click += (_, _) => OpenColorPicker();
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
                var colorSample = new Border
                {
                    Width = 18, Height = 18,
                    CornerRadius = new CornerRadius(9),
                    Background = new SolidColorBrush(color),
                    BorderThickness = new Thickness(selected ? 2 : 1),
                    BorderBrush = selected
                        ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                        : new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0))
                };
                var swatch = new Button
                {
                    Width = 28,
                    Height = 28,
                    Padding = new Thickness(4),
                    Margin = new Thickness(1),
                    Style = (Style)FindResource("EditorIconButton"),
                    Content = colorSample,
                    ToolTip = hex
                };
                AutomationProperties.SetName(swatch, $"색상 {hex}");
                swatch.Click += (_, _) =>
                {
                    _currentColor = color;
                    if (_activeTool != null) _activeTool.CurrentColor = color;
                    BuildContextProperties(toolName);
                };
                PropertyPanel.Children.Add(swatch);
            }

            // 스포이드 버튼 (이미지에서 색상 추출)
            var eyedropBtn = new Button
            {
                Width = 30, Height = 30,
                Style = (Style)FindResource("EditorIconButton"),
                Margin = new Thickness(3, 0, 0, 0),
                ToolTip = "이미지에서 색상 추출 (스포이드)",
                Content = CreateIconPath("eyedropper", 17)
            };
            AutomationProperties.SetName(eyedropBtn, "이미지에서 색상 추출");
            eyedropBtn.Click += (_, _) => SelectTool("색상추출");
            PropertyPanel.Children.Add(eyedropBtn);

            // 컬러 피커 버튼 (전체 색상 다이얼로그)
            var pickerBtn = new Button
            {
                Width = 30, Height = 30,
                Style = (Style)FindResource("EditorIconButton"),
                Margin = new Thickness(2, 0, 0, 0),
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
            pickerBtn.Content = pickerIcon;
            AutomationProperties.SetName(pickerBtn, "전체 색상 선택 열기");
            pickerBtn.Click += (_, _) => OpenColorPicker();
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

    private Button MakeColorSwatch(System.Windows.Media.Color color, System.Windows.Media.Color? selected, Action<System.Windows.Media.Color> onClick)
    {
        bool isSelected = selected.HasValue && selected.Value == color;
        string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        var sample = new Border
        {
            Width = 18, Height = 18, CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(color),
            BorderThickness = new Thickness(isSelected ? 2 : 1),
            BorderBrush = isSelected
                ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                : new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0))
        };
        var swatch = new Button
        {
            Width = 28,
            Height = 28,
            Padding = new Thickness(4),
            Margin = new Thickness(1),
            Style = (Style)FindResource(isSelected ? "ToolbarButtonActive" : "EditorIconButton"),
            Content = sample,
            ToolTip = hex
        };
        AutomationProperties.SetName(swatch, $"색상 {hex}");
        swatch.Click += (_, _) => onClick(color);
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
        Content = text, FontSize = 11, Padding = new Thickness(7, 4, 7, 4),
        Margin = new Thickness(1), Cursor = Cursors.Hand,
        MinHeight = 30,
        FocusVisualStyle = (Style)FindResource("EditorFocusVisual"),
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
            var modes = new[] { "단색", "무지개", "모노", "가을", "여름", "바다", "파스텔", "네온" };
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
                ("개체 지우개", EraserMode.Object),
                ("영역 지우개", EraserMode.Area),
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

    private void OnHistoryToggleClick(object sender, RoutedEventArgs e)
    {
        _historyPanelVisibilityOverride = HistoryBorder.Visibility != Visibility.Visible;
        UpdateChromeLayout();
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (MoreBtn.ContextMenu is not { } menu) return;
        menu.PlacementTarget = MoreBtn;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnPremiumContentClick(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = EditorPremiumContentCatalog.ChannelUri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch
        {
            StatusText.Text = "추천 콘텐츠 페이지를 열 수 없습니다";
        }
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e) =>
        Canvas.Zoom /= 1.1;

    private void OnZoomInClick(object sender, RoutedEventArgs e) =>
        Canvas.Zoom *= 1.1;

    private void OnZoomResetClick(object sender, RoutedEventArgs e)
    {
        double dpiScale = VisualTreeHelper.GetDpi(Canvas).PixelsPerDip;
        Canvas.Zoom = 1.0 / dpiScale;
    }

    private void OnZoomFitClick(object sender, RoutedEventArgs e) =>
        Canvas.ApplyInitialZoom();

    // ── 캡쳐 기록 ──────────────────────────────────────────────

    private static void AddToHistory(BitmapSource image)
    {
        _captureHistory.Insert(0, image);
        while (_captureHistory.Count > MaxHistory)
            _captureHistory.RemoveAt(_captureHistory.Count - 1);
    }

    private void BuildHistory()
    {
        _historyCards.Clear();
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
                ToolTip = $"{img.PixelWidth}×{img.PixelHeight}\n좌클릭: 열기 | 우클릭: 복사\n↑↓: 이동"
            };

            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            thumb.Margin = new Thickness(4);
            cardGrid.Children.Add(thumb);

            var footer = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("BackgroundTertiaryBrush"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 4, 7, 4)
            };
            Grid.SetRow(footer, 1);
            var footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.Children.Add(new TextBlock
            {
                Text = $"#{i + 1}",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush")
            });
            var dimensions = new TextBlock
            {
                Text = $"{img.PixelWidth}×{img.PixelHeight}",
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush")
            };
            Grid.SetColumn(dimensions, 1);
            footerGrid.Children.Add(dimensions);
            footer.Child = footerGrid;
            cardGrid.Children.Add(footer);

            bool isSelected = (img == _sourceImage);
            var border = new Border
            {
                Tag = img,
                Focusable = true,
                FocusVisualStyle = (Style)FindResource("HistoryItemFocusVisual"),
                Width = 148,
                Height = 124,
                Background = (System.Windows.Media.Brush)FindResource("BackgroundPrimaryBrush"),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                BorderBrush = isSelected
                    ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                    : (System.Windows.Media.Brush)FindResource("DividerBrush"),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 0, 8),
                ClipToBounds = true,
                Cursor = Cursors.Hand,
                Child = cardGrid
            };
            KeyboardNavigation.SetIsTabStop(border, isSelected);
            AutomationProperties.SetName(
                border,
                $"캡처 결과 {i + 1}, {img.PixelWidth} × {img.PixelHeight}");

            var localImg = img;
            border.MouseDown += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    LoadFromHistory(localImg, focusHistoryItem: true);
                    e.Handled = true;
                }
            };

            // 우클릭 컨텍스트 메뉴
            var ctx = new ContextMenu();

            var miOpen = new MenuItem { Header = "열기" };
            miOpen.Click += (_, _) => LoadFromHistory(localImg);
            ctx.Items.Add(miOpen);

            var miCopy = new MenuItem { Header = "클립보드 복사" };
            miCopy.Click += (_, _) =>
            {
                BitmapHelper.SetClipboardPng(localImg);
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

            var miQuickSave = new MenuItem { Header = "빠른 저장" };
            var miCopyPath = new MenuItem { Header = "저장 경로 복사", IsEnabled = false };
            miQuickSave.Click += (_, _) =>
            {
                var bmp = BitmapHelper.ToBitmap(localImg);
                var path = _saveManager.SaveAuto(bmp, _settings);
                bmp.Dispose();
                StatusText.Text = $"저장됨: {path}";
                miCopyPath.IsEnabled = true;
                miCopyPath.Tag = path;
            };
            ctx.Items.Add(miQuickSave);

            miCopyPath.Click += (_, _) =>
            {
                if (miCopyPath.Tag is string p)
                {
                    System.Windows.Clipboard.SetText(p);
                    StatusText.Text = $"경로 복사됨: {p}";
                }
            };
            ctx.Items.Add(miCopyPath);

            var miOpenFolder = new MenuItem { Header = "저장 폴더 열기" };
            miOpenFolder.Click += (_, _) =>
            {
                var path = _settings.Save.AutoSavePath;
                if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start("explorer.exe", path);
            };
            ctx.Items.Add(miOpenFolder);

            ctx.Items.Add(new Separator());

            var miDelete = new MenuItem
            {
                Header = "기록에서 삭제",
                Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush")
            };
            miDelete.Click += (_, _) =>
            {
                _captureHistory.Remove(localImg);
                _captureObjects.Remove(localImg);
                if (localImg == _sourceImage && _captureHistory.Count > 0)
                    LoadFromHistory(_captureHistory[0]);
                else
                    BuildHistory();
                StatusText.Text = "캡처 기록 삭제됨";
            };
            ctx.Items.Add(miDelete);

            border.ContextMenu = ctx;

            _historyCards[img] = border;
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
        if (_captureHistory.Count == 0)
        {
            StatusText.Text = "비울 캡처 기록이 없습니다";
            return;
        }

        MessageBoxResult result = MessageBox.Show(
            this,
            "현재 세션의 캡처 기록을 모두 비울까요?",
            "캡처 기록 비우기",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        _captureHistory.Clear();
        BuildHistory();
        StatusText.Text = "캡처 기록을 비웠습니다";
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
    private Button MakePickerButton(Action<System.Windows.Media.Color> onPicked)
    {
        var sample = new Border
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
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0))
        };
        var btn = new Button
        {
            Width = 30,
            Height = 30,
            Padding = new Thickness(5),
            Margin = new Thickness(3, 0, 0, 0),
            Style = (Style)FindResource("EditorIconButton"),
            Content = sample,
            ToolTip = "색상 선택 (전체 팔레트)"
        };
        AutomationProperties.SetName(btn, "전체 색상 선택 열기");
        btn.Click += (_, _) =>
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

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var rendered = RenderFinalImage();
        var path = _saveManager.SaveAuto(rendered, _settings);
        StatusText.Text = $"저장됨: {path}";
        if (_settings.Save.CopyToClipboard)
            BitmapHelper.SetClipboardPng(BitmapHelper.ToBitmapSource(rendered));
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
        BitmapHelper.SetClipboardPng(BitmapHelper.ToBitmapSource(rendered));
        StatusText.Text = "클립보드에 복사됨";
        rendered.Dispose();
    }

    private void OnEditorSettingsClick(object sender, RoutedEventArgs e)
    {
        if (_settingsManager == null) return;
        // 단축키 탭(4번째, 인덱스 3 — 일반/캡쳐/저장/단축키/지정사이즈/AI)을 기본 선택
        var win = new SettingsWindow(_settingsManager, initialTabIndex: 3);
        win.Owner = this;
        win.ShowDialog();
    }

    private void OnApiKeyHelpClick(object sender, RoutedEventArgs e)
    {
        var win = new ApiKeyHelpWindow(_settingsManager);
        win.Owner = this;
        win.ShowDialog();
    }

    private void RefreshOcrBanner()
    {
        if (OcrApiKeyBanner == null) return;
        OcrApiKeyBanner.Visibility = _ocrService.HasApiKey() ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnApiKeyBannerClick(object sender, MouseButtonEventArgs e)
    {
        var win = new ApiKeyHelpWindow(_settingsManager);
        win.Owner = this;
        win.ShowDialog();
        // 다이얼로그 닫힌 후 키가 등록됐을 수 있음 ▸ 배너 갱신
        RefreshOcrBanner();
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

        // 이미 열려있는 편집기가 구식 _settings 객체를 잡고 있을 수 있음 ▸ 디스크에서 최신 설정 로드
        var currentSettings = _settingsManager?.Load() ?? _settings;

        RefreshOcrBanner();
        SetStatus("OCR 실행 중...");
        OcrPanel.Visibility = Visibility.Visible;
        OcrPanelTitle.Text = "텍스트 추출";
        OcrPanelMeta.Text = "추출 중…";
        OcrTextBox.Text = "";
        OcrSourceLangLabel.Text = "";
        // 우측 번역 패널 초기화 (placeholder 표시)
        OcrTranslatedBox.Text = "";
        OcrTargetLangLabel.Text = "";
        if (OcrTranslatedPlaceholder != null)
            OcrTranslatedPlaceholder.Visibility = Visibility.Visible;

        try
        {
            var result = await _ocrService.ExtractAsync(_sourceImage, currentSettings);
            if (result.Outcome == EditorOcrOutcome.LanguagePackMissing)
            {
                OcrPanelTitle.Text = "텍스트 추출";
                OcrPanelMeta.Text = "OCR 언어팩 필요";
                OcrTextBox.Text =
                    $"설정된 언어({currentSettings.Ocr.Language})의 OCR 언어팩이 설치되어 있지 않습니다.\n" +
                    "Windows 설정 > 시간 및 언어 > 언어에서 언어팩을 설치한 뒤 다시 시도해주세요.";
                SetStatus("OCR 언어팩 없음");
                _pendingAutoTranslate = false;
                return;
            }

            if (result.Outcome == EditorOcrOutcome.NoText)
            {
                OcrPanelMeta.Text = "(텍스트 없음)";
                OcrTextBox.Text = "";
                SetStatus("OCR: 텍스트를 찾지 못했습니다");
                _pendingAutoTranslate = false;
                return;
            }

            if (result.Outcome == EditorOcrOutcome.Failed)
            {
                OcrPanelTitle.Text = "텍스트 추출";
                OcrPanelMeta.Text = "실패";
                OcrTextBox.Text = result.ErrorMessage ?? "";
                SetStatus("OCR 실패");
                _pendingAutoTranslate = false;
                return;
            }

            var text = result.Text;
            OcrTextBox.Text = text;
            var fallbackNote = result.UsedFallback ? " (폴백)" : "";
            OcrPanelTitle.Text = "텍스트 추출";
            OcrPanelMeta.Text = $"{text.Length}자";
            OcrSourceLangLabel.Text = result.LanguageTag + fallbackNote;
            SetStatus($"OCR 완료 ({text.Length}자)");

            // 대상 언어 드롭다운 자동 선택: OCR 결과가 한국어면 영어, 그 외면 한국어
            var detected = ShinCapture.Services.Ai.LanguageDetector.DetectSimple(text);
            string autoTarget = (detected == "ko") ? "en" : "ko";
            foreach (System.Windows.Controls.ComboBoxItem item in OcrTranslateLangBox.Items)
            {
                if ((string)item.Tag == autoTarget)
                {
                    OcrTranslateLangBox.SelectedItem = item;
                    break;
                }
            }

            // 번역 버튼이 트리거한 흐름이면 OCR 직후 자동 번역
            if (_pendingAutoTranslate && !string.IsNullOrWhiteSpace(text))
            {
                _pendingAutoTranslate = false;
                Dispatcher.BeginInvoke(new Action(() => OnOcrTranslateClick(this, new RoutedEventArgs())),
                    System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        catch (Exception ex)
        {
            OcrPanelTitle.Text = "텍스트 추출";
            OcrPanelMeta.Text = "실패";
            OcrTextBox.Text = ex.Message;
            SetStatus("OCR 실패");
            _pendingAutoTranslate = false;
        }
    }

    private void OnOcrClose(object sender, RoutedEventArgs e)
    {
        OcrPanel.Visibility = Visibility.Collapsed;
    }

    private double _editorHeightBeforeOcr = -1;

    private void OnOcrPanelVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not bool isVisible) return;

        EditorWindowSizeMode mode = CurrentEditorSettings().WindowSizeMode;
        if (!EditorWindowSizingPolicy.ShouldGrowForOcr(mode))
        {
            _editorHeightBeforeOcr = -1;
            if (isVisible)
                RefreshOcrBanner();
            return;
        }

        if (isVisible)
        {
            RefreshOcrBanner();
            // 처음 보일 때만 원래 높이 저장
            if (_editorHeightBeforeOcr < 0) _editorHeightBeforeOcr = this.Height;
            // 레이아웃 완료 후 측정해야 ActualHeight가 정확
            Dispatcher.BeginInvoke(new Action(GrowWindowForOcrPanel),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
        else
        {
            // 닫힐 때 원래 높이로 복원 (사용자가 그 사이 리사이즈한 영향은 무시)
            if (_editorHeightBeforeOcr > 0)
            {
                this.Height = _editorHeightBeforeOcr;
                _editorHeightBeforeOcr = -1;
            }
        }
    }

    private void GrowWindowForOcrPanel()
    {
        var panelHeight = OcrPanel.ActualHeight;
        if (panelHeight <= 0) return;
        MonitorWorkArea workArea = MonitorWorkAreaService.GetForWindow(this);
        var maxHeight = Math.Max(1, workArea.DipHeight - 20);
        var desired = Math.Clamp(this.Height + panelHeight, 1, maxHeight);
        this.Height = desired;
        UpdateLayout();
        _ = MonitorWorkAreaService.ClampWindowToWorkArea(this);
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

    private async void OnOcrTranslateClick(object sender, RoutedEventArgs e)
    {
        var text = OcrTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("번역: 추출된 텍스트가 없습니다");
            return;
        }

        var settings = _settingsManager?.Load() ?? _settings;
        if (!settings.Ai.Enabled)
        {
            SetStatus("번역: 설정 > AI 탭에서 활성화 필요");
            return;
        }

        if (!_ocrService.HasApiKey())
        {
            SetStatus("번역: AI 키가 필요합니다 (설정 > AI)");
            return;
        }

        string targetLang = settings.Ai.TargetLanguage;
        if (OcrTranslateLangBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
            targetLang = tag;

        if (OcrTranslatedPlaceholder != null)
            OcrTranslatedPlaceholder.Visibility = Visibility.Collapsed;
        OcrTranslatedBox.Text = "번역 중…";
        OcrTargetLangLabel.Text = targetLang;
        SetStatus("번역 실행 중…");

        try
        {
            var r = await _ocrService.TranslateAsync(text, settings, targetLang);
            switch (r.Outcome)
            {
                case EditorTranslationOutcome.Success:
                    OcrTranslatedBox.Text = r.TranslatedText;
                    OcrTargetLangLabel.Text = targetLang;
                    SetStatus($"번역 완료 ({r.TranslatedText.Length}자, {targetLang})");
                    break;
                case EditorTranslationOutcome.SkippedSameLanguage:
                    OcrTranslatedBox.Text = r.OriginalText;
                    OcrTargetLangLabel.Text = $"{targetLang} (동일)";
                    SetStatus($"이미 {targetLang}입니다");
                    break;
                case EditorTranslationOutcome.Disabled:
                    SetStatus("번역: 설정 > AI 탭에서 활성화 필요");
                    break;
                case EditorTranslationOutcome.NoKey:
                    SetStatus("번역: AI 키가 필요합니다 (설정 > AI)");
                    break;
                default:
                    OcrTranslatedBox.Text = "(번역 결과 없음)";
                    SetStatus("번역 결과 없음");
                    break;
            }
        }
        catch (ShinCapture.Services.Ai.OpenAiException ex)
        {
            OcrTranslatedBox.Text = $"번역 실패: {ex.Message}";
            SetStatus($"번역 실패 — {ex.Kind}");
        }
    }

    private void OnOcrTranslatedCopyClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(OcrTranslatedBox.Text)) return;
        System.Windows.Clipboard.SetText(OcrTranslatedBox.Text);
        SetStatus($"번역문 복사됨 ({OcrTranslatedBox.Text.Length}자)");
    }
}
