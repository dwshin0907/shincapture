using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ShinCapture.Helpers;
using ShinCapture.Models;
using ShinCapture.Services;
using CaptureMode = ShinCapture.Models.CaptureMode;

namespace ShinCapture.Views;

public partial class TrayFlyoutWindow : Window
{
    private readonly ObservableCollection<TrayCaptureAction> _secondaryCaptureActions = [];
    private bool _isPositioning;

    public event Action<CaptureMode>? CaptureRequested;
    public event Action<TrayMenuCommand>? CommandRequested;

    public TrayFlyoutWindow()
    {
        InitializeComponent();
        SecondaryActions.ItemsSource = _secondaryCaptureActions;
    }

    public void ShowNearCursor()
    {
        _isPositioning = true;
        try
        {
            if (!NativeMethods.GetCursorPos(out NativeMethods.POINT cursor))
                throw new InvalidOperationException("Unable to read the tray cursor position.");

            IntPtr monitor = NativeMethods.MonitorFromPoint(
                cursor,
                NativeMethods.MONITOR_DEFAULTTONEAREST);
            NativeMethods.MONITORINFO monitorInfo = new()
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };
            if (monitor == IntPtr.Zero ||
                !NativeMethods.GetMonitorInfo(monitor, ref monitorInfo) ||
                monitorInfo.rcWork.Width <= 0 ||
                monitorInfo.rcWork.Height <= 0)
            {
                throw new InvalidOperationException("Unable to resolve the tray monitor work area.");
            }

            IntPtr handle = new WindowInteropHelper(this).EnsureHandle();
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException("Unable to create the tray flyout handle.");

            int preliminaryX = InsetWorkAreaCoordinate(
                monitorInfo.rcWork.Left,
                monitorInfo.rcWork.Width);
            int preliminaryY = InsetWorkAreaCoordinate(
                monitorInfo.rcWork.Top,
                monitorInfo.rcWork.Height);
            if (!NativeMethods.SetWindowPos(
                    handle,
                    IntPtr.Zero,
                    preliminaryX,
                    preliminaryY,
                    0,
                    0,
                    NativeMethods.SWP_NOSIZE |
                    NativeMethods.SWP_NOZORDER |
                    NativeMethods.SWP_NOACTIVATE))
            {
                throw CreatePositioningException("Unable to move the tray flyout to its target monitor.");
            }

            double visualDpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            double dpiScale = MonitorWorkAreaService.ResolveDpiScale(
                NativeMethods.GetDpiForWindow(handle),
                visualDpiScale);
            MonitorWorkArea workArea = new(
                monitorInfo.rcWork.Left,
                monitorInfo.rcWork.Top,
                monitorInfo.rcWork.Width,
                monitorInfo.rcWork.Height,
                dpiScale);

            if (!IsVisible)
                Show();
            UpdateLayout();

            WindowPixelBounds target = TrayFlyoutPositioner.CalculateFromDips(
                new PixelPoint(cursor.X, cursor.Y),
                workArea,
                ActualWidth,
                ActualHeight);

            if (!NativeMethods.SetWindowPos(
                    handle,
                    NativeMethods.HWND_TOPMOST,
                    target.Left,
                    target.Top,
                    target.Width,
                    target.Height,
                    0))
            {
                throw CreatePositioningException("Unable to position the tray flyout.");
            }

            TryApplyRoundedCorners(handle);
            bool foregroundActivated = NativeMethods.SetForegroundWindow(handle);
            bool wpfActivated = Activate();
            if (foregroundActivated || wpfActivated || IsActive)
                ScheduleInitialFocus();
        }
        catch
        {
            if (IsVisible)
                Hide();
            throw;
        }
        finally
        {
            _isPositioning = false;
        }
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
        ScheduleInitialFocus();
    }

    private void ScheduleInitialFocus()
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

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (!_isPositioning)
            Hide();
    }

    private static int InsetWorkAreaCoordinate(int origin, int size)
    {
        long inset = Math.Min(TrayFlyoutPositioner.Margin, Math.Max(0, size - 1));
        return (int)Math.Clamp((long)origin + inset, int.MinValue, int.MaxValue);
    }

    private static InvalidOperationException CreatePositioningException(string message) =>
        new(message, new Win32Exception(Marshal.GetLastWin32Error()));

    private static void TryApplyRoundedCorners(IntPtr handle)
    {
        int cornerPreference = NativeMethods.DWMWCP_ROUND;
        try
        {
            _ = NativeMethods.DwmSetWindowAttribute(
                handle,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref cornerPreference,
                sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }
}
