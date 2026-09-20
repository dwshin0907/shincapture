using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShinCapture.Editor;

namespace ShinCapture.Views;

public partial class EditorWindow
{
    private readonly DispatcherTimer _toolStyleSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly HashSet<string> _pendingToolStyles = new();

    private void RememberToolStyle()
    {
        if (_activeTool == null || !EditorToolStyleMemory.Supports(_activeTool)) return;
        EditorToolStyleMemory.Remember(_settings.Editor, _activeTool);
        _pendingToolStyles.Add(_activeTool.Name);
        _toolStyleSaveTimer.Stop();
        _toolStyleSaveTimer.Start();
    }

    private void FlushToolStyles()
    {
        _toolStyleSaveTimer.Stop();
        if (_pendingToolStyles.Count == 0) return;
        try
        {
            _settingsManager?.Update(settings =>
            {
                settings.Editor.ToolStyles ??= new();
                foreach (string key in _pendingToolStyles)
                    settings.Editor.ToolStyles[key] = _settings.Editor.ToolStyles[key];
            }, raiseChanged: false);
            _pendingToolStyles.Clear();
        }
        catch
        {
            SetStatus("색상·굵기는 현재 창에 유지됩니다. 설정 파일 저장에 실패했습니다.");
        }
    }

    private Button CreateImageTransformMenuButton()
    {
        var menu = new ContextMenu { Placement = PlacementMode.Bottom };
        void Add(string label, string icon, ImageTransformKind kind) =>
            menu.Items.Add(CreateActionMenuItem(label, icon, (_, _) => ApplyImageTransform(kind, label)));

        Add("오른쪽으로 90° 회전", "rotate-right", ImageTransformKind.RotateClockwise);
        Add("왼쪽으로 90° 회전", "rotate-left", ImageTransformKind.RotateCounterclockwise);
        Add("180° 회전", "rotate-right", ImageTransformKind.Rotate180);
        menu.Items.Add(new Separator());
        Add("좌우 반전", "flip-horizontal", ImageTransformKind.FlipHorizontal);
        Add("상하 반전", "flip-vertical", ImageTransformKind.FlipVertical);

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(CreateIconPath("rotate-right", 18));
        panel.Children.Add(new TextBlock
        {
            Text = "회전·반전", FontSize = 12, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(6, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center
        });
        var button = new Button
        {
            Content = panel, ContextMenu = menu,
            Style = (Style)FindResource("EditorCommandButton"),
            Margin = new Thickness(2, 0, 2, 0),
            ToolTip = "이미지 전체를 회전·반전합니다. 그린 개체는 이미지에 합쳐지며, 실행취소로 복원할 수 있습니다."
        };
        AutomationProperties.SetName(button, "이미지 회전·반전 메뉴");
        button.Click += (_, _) => { menu.PlacementTarget = button; menu.IsOpen = true; };
        return button;
    }

    private void ApplyImageTransform(ImageTransformKind kind, string label)
    {
        SelectTool("선택");
        var command = new ImageTransformCommand(_sourceImage, _objects, kind, SetTransformedImage);
        _commandStack.Execute(command);
        SetStatus($"{label} 완료 — Ctrl+Z로 실행취소");
    }

    private void SetTransformedImage(BitmapSource image)
    {
        int historyIndex = _captureHistory.IndexOf(_sourceImage);
        _historySelection.Replace(_sourceImage, image);
        RemoveCaptureState(_sourceImage);
        if (historyIndex >= 0) _captureHistory[historyIndex] = image;
        _sourceImage = image;
        SaveCurrentObjects();
        Canvas.BackgroundImage = image;
        Canvas.ApplyInitialZoom();
        SelectTool("선택");
        BuildHistory();
        OcrPanel.Visibility = Visibility.Collapsed;
    }
}
