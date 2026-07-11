using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ShinCapture.Models;
using ShinCapture.Services;
using CaptureMode = ShinCapture.Models.CaptureMode;

namespace ShinCapture.Views;

public partial class TrayFlyoutWindow : Window
{
    private readonly ObservableCollection<TrayCaptureAction> _secondaryCaptureActions = [];

    public event Action<CaptureMode>? CaptureRequested;
    public event Action<TrayMenuCommand>? CommandRequested;

    public TrayFlyoutWindow()
    {
        InitializeComponent();
        SecondaryActions.ItemsSource = _secondaryCaptureActions;
    }

    public void UpdateSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var actions = TrayMenuCatalog.CreateCaptureActions(settings.Hotkeys);
        PrimaryActionHost.Content = actions.Single(action => action.Mode == CaptureMode.Region);
        TranslateActionHost.Content = actions.Single(action => action.Mode == CaptureMode.Translate);

        _secondaryCaptureActions.Clear();
        foreach (var action in actions.Where(action => !action.IsWide))
            _secondaryCaptureActions.Add(action);

        WindowModeText.Text = settings.Editor.WindowSizeMode switch
        {
            EditorWindowSizeMode.Maximized => "편집기 크기 · 현재 모니터에 최대화",
            EditorWindowSizeMode.FitToCapture => "편집기 크기 · 캡처 이미지에 맞춤",
            _ => "편집기 크기 · 마지막 크기 유지"
        };
    }

    private void OnCaptureClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TrayCaptureAction action)
            return;

        Hide();
        Dispatcher.BeginInvoke(
            new Action(() => CaptureRequested?.Invoke(action.Mode)),
            DispatcherPriority.Normal);
    }

    private void OnCommandClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TrayMenuCommand command)
            return;

        Hide();
        Dispatcher.BeginInvoke(
            new Action(() => CommandRequested?.Invoke(command)),
            DispatcherPriority.Normal);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        Hide();
        e.Handled = true;
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsActive)
                return;
            if (Keyboard.FocusedElement is DependencyObject focusedElement &&
                ReferenceEquals(GetWindow(focusedElement), this))
            {
                return;
            }

            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }), DispatcherPriority.Input);
    }

    private void OnDeactivated(object? sender, EventArgs e) => Hide();
}
