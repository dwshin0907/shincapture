using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShinCapture.Capture;
using ShinCapture.Helpers;
using ShinCapture.Models;
using ShinCapture.Views.Overlay;
using SDColor = System.Drawing.Color;

namespace ShinCapture.Tests.Views;

[CollectionDefinition("WPF overlay lifecycle", DisableParallelization = true)]
public sealed class OverlayLifecycleCollection;

[Collection("WPF overlay lifecycle")]
public sealed class OverlayLifecycleTests
{
    [Fact]
    public void CancelWhileSmartCutWaitsForProcessorClosesImmediatelyAndDiscardsResult()
    {
        RunSta(() =>
        {
            SemaphoreSlim processorGate = GetStaticField<SemaphoreSlim>(
                typeof(SmartCutProcessor),
                "ProcessingGate");
            Assert.True(
                processorGate.Wait(TimeSpan.FromSeconds(5)),
                "Smart-cut processor gate remained held by an earlier operation.");
            CaptureOverlay? overlay = null;
            try
            {
                overlay = CreateReadySmartCutOverlay(out _);
                overlay.Left = -10000;
                overlay.Top = -10000;
                overlay.Show();

                InvokePrivate(overlay, "FinishCapture");
                Assert.True(GetField<bool>(overlay, "_finishStarted"));
                Assert.NotNull(GetField<CancellationTokenSource?>(overlay, "_finishCancellation"));

                var stopwatch = Stopwatch.StartNew();
                InvokePrivate(overlay, "CancelCapture");
                stopwatch.Stop();

                Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500));
                Assert.False(overlay.IsVisible);
                Assert.Null(overlay.Result);
            }
            finally
            {
                if (overlay?.IsVisible == true)
                    InvokePrivate(overlay, "CancelCapture");
                overlay?.Result?.Image.Dispose();
                processorGate.Release();
            }

            Assert.NotNull(overlay);
            PumpDispatcherUntil(
                () => GetField<CancellationTokenSource?>(overlay!, "_finishCancellation") == null,
                TimeSpan.FromSeconds(5));
            Assert.Null(overlay!.Result);
        });
    }

    [Fact]
    public void ClosingBeforeLoadedInitializationPreventsLateModeInitialization()
    {
        RunSta(() =>
        {
            var mode = new TrackingCaptureMode();
            var overlay = new CaptureOverlay(new CaptureSettings())
            {
                WindowState = WindowState.Minimized,
                ShowActivated = false
            };

            overlay.Start(mode);
            overlay.Close();
            PumpDispatcher(TimeSpan.FromMilliseconds(100));

            Assert.Equal(0, mode.InitializeCount);
            Assert.Null(overlay.Result);
            Assert.False(overlay.IsVisible);
        });
    }

    [Fact]
    public void InvalidSmartCutLassoStaysOpenForAValidRetry()
    {
        RunSta(() =>
        {
            using var screen = CreateBitmap(320, 200, SDColor.SlateGray);
            var overlay = new CaptureOverlay(new CaptureSettings());
            FrameworkElement root = (FrameworkElement)overlay.FindName("RootGrid");
            root.Measure(new System.Windows.Size(320, 200));
            root.Arrange(new Rect(0, 0, 320, 200));
            var mode = new SmartCutCaptureMode();
            mode.Initialize(screen, root);
            SetField(overlay, "_mode", mode);
            SetField(overlay, "_screenBitmap", (Bitmap)screen.Clone());
            try
            {
                SetSmartCutPoints(mode, [new(30, 30), new(31, 31)]);
                SetField(mode, "_isDrawing", true);
                InvokeMouseUp(overlay);

                Assert.False(mode.IsComplete);
                Assert.False(GetField<bool>(overlay, "_finishStarted"));
                Assert.Equal(
                    Visibility.Visible,
                    ((FrameworkElement)overlay.FindName("CaptureStatusBorder")).Visibility);

                SetField(mode, "_isDrawing", true);
                MouseButtonEventArgs retryMouseUp = CreateMouseUpArgs();
                System.Windows.Point end = retryMouseUp.GetPosition(root);
                double dx = end.X < 160 ? 100 : -100;
                double dy = end.Y < 100 ? 70 : -70;
                SetSmartCutPoints(mode,
                [
                    end,
                    new(end.X + dx, end.Y),
                    new(end.X + dx, end.Y + dy),
                    new(end.X, end.Y + dy)
                ]);
                InvokePrivate(overlay, "OnMouseUp", overlay, retryMouseUp);

                Assert.True(mode.IsComplete);
                Assert.True(GetField<bool>(overlay, "_finishStarted"));
                InvokePrivate(overlay, "CancelCapture");
                PumpDispatcherUntil(
                    () => GetField<CancellationTokenSource?>(overlay, "_finishCancellation") == null,
                    TimeSpan.FromSeconds(5));
                Assert.Null(overlay.Result);
            }
            finally
            {
                if (!GetField<bool>(overlay, "_isClosed"))
                    InvokePrivate(overlay, "CancelCapture");
                overlay.Result?.Image.Dispose();
            }
        });
    }

    [Theory]
    [InlineData(960, 600)]
    [InlineData(1920, 1080)]
    public void RendersActualOverlayWithRetryAndVisibleProcessingHint(int width, int height)
    {
        RunSta(() =>
        {
            using Bitmap background = CreateBitmap(width, height, SDColor.FromArgb(38, 54, 73));
            var overlay = new CaptureOverlay(new CaptureSettings());
            var root = (FrameworkElement)overlay.FindName("RootGrid");
            var screenImage = (System.Windows.Controls.Image)overlay.FindName("ScreenImage");
            var statusBorder = (FrameworkElement)overlay.FindName("CaptureStatusBorder");
            var statusText = (System.Windows.Controls.TextBlock)overlay.FindName("CaptureStatusText");
            screenImage.Source = BitmapHelper.ToBitmapSource(background);
            statusText.Text = "스마트 컷 처리 중... Esc로 취소할 수 있습니다.";
            statusBorder.Visibility = Visibility.Visible;
            SetField(overlay, "_mode", new RenderPreviewMode(width, height));
            InvokePrivate(overlay, "Redraw");

            RenderTargetBitmap rendered = RenderWithRetry(root, width, height);
            Assert.True(ContainsStatusHintPixels(rendered));

            string? directory = Environment.GetEnvironmentVariable("SHINCAPTURE_OVERLAY_ARTIFACT_DIR");
            if (!string.IsNullOrWhiteSpace(directory))
                SavePng(rendered, Path.Combine(directory, $"overlay-{width}x{height}.png"));
        });
    }

    private static CaptureOverlay CreateReadySmartCutOverlay(out SmartCutCaptureMode mode)
    {
        var overlay = new CaptureOverlay(new CaptureSettings())
        {
            Width = 400,
            Height = 300,
            ShowActivated = false
        };
        var screen = CreateBitmap(400, 300, SDColor.White);
        using (Graphics graphics = Graphics.FromImage(screen))
            graphics.FillEllipse(System.Drawing.Brushes.CornflowerBlue, 100, 50, 200, 200);
        FrameworkElement root = (FrameworkElement)overlay.FindName("RootGrid");
        root.Measure(new System.Windows.Size(400, 300));
        root.Arrange(new Rect(0, 0, 400, 300));
        mode = new SmartCutCaptureMode();
        mode.Initialize(screen, root);
        SetSmartCutPoints(mode,
        [
            new(70, 30), new(330, 30), new(330, 270), new(70, 270)
        ]);
        SetProperty(mode, "IsComplete", true);
        SetField(overlay, "_mode", mode);
        SetField(overlay, "_screenBitmap", screen);
        return overlay;
    }

    private static void InvokeMouseUp(CaptureOverlay overlay)
    {
        MouseButtonEventArgs args = CreateMouseUpArgs();
        InvokePrivate(overlay, "OnMouseUp", overlay, args);
    }

    private static MouseButtonEventArgs CreateMouseUpArgs() =>
        new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = Mouse.MouseUpEvent
        };

    private static void SetSmartCutPoints(
        SmartCutCaptureMode mode,
        IEnumerable<System.Windows.Point> points)
    {
        var target = GetField<List<System.Windows.Point>>(mode, "_points");
        target.Clear();
        target.AddRange(points);
    }

    private static RenderTargetBitmap RenderWithRetry(
        FrameworkElement root,
        int width,
        int height)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            root.Measure(new System.Windows.Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();
            var target = new RenderTargetBitmap(
                width,
                height,
                96,
                96,
                PixelFormats.Pbgra32);
            target.Render(root);
            if (HasVisiblePixel(target)) return target;
            PumpDispatcher(TimeSpan.FromMilliseconds(30));
        }
        throw new Xunit.Sdk.XunitException("CaptureOverlay rendered no visible pixels after retries.");
    }

    private static bool HasVisiblePixel(BitmapSource source)
    {
        int stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        for (int index = 3; index < pixels.Length; index += 4)
        {
            if (pixels[index] != 0) return true;
        }
        return false;
    }

    private static bool ContainsStatusHintPixels(BitmapSource source)
    {
        int yLimit = Math.Min(source.PixelHeight, 100);
        int xStart = source.PixelWidth / 4;
        int xEnd = source.PixelWidth * 3 / 4;
        int stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        for (int y = 10; y < yLimit; y++)
        for (int x = xStart; x < xEnd; x++)
        {
            int offset = y * stride + x * 4;
            byte blue = pixels[offset];
            byte green = pixels[offset + 1];
            byte red = pixels[offset + 2];
            if (red > 210 && green > 210 && blue > 210) return true;
        }
        return false;
    }

    private static void SavePng(BitmapSource image, string path)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(fullPath);
        encoder.Save(stream);
    }

    private static Bitmap CreateBitmap(int width, int height, SDColor color)
    {
        var bitmap = new Bitmap(width, height);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "STA overlay test timed out.");
        if (error != null) throw new Xunit.Sdk.XunitException(error.ToString());
    }

    private static void PumpDispatcherUntil(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < timeout)
            PumpDispatcher(TimeSpan.FromMilliseconds(10));
        Assert.True(condition(), "Dispatcher condition did not complete before timeout.");
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static T GetField<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(target)!);

    private static T GetStaticField<T>(Type type, string name) =>
        (T)(type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!);

    private static void SetField(object target, string name, object? value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);

    private static void SetProperty(object target, string name, object value) =>
        target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(target, value);

    private static void InvokePrivate(object target, string name, params object[] args) =>
        target.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Single(method => method.Name == name && method.GetParameters().Length == args.Length)
            .Invoke(target, args);

    private sealed class TrackingCaptureMode : ICaptureMode
    {
        public int InitializeCount { get; private set; }
        public bool IsComplete => false;
        public bool IsCancelled => false;
        public Cursor? RequestedCursor => null;
        public void Initialize(Bitmap screenBitmap, FrameworkElement overlay) => InitializeCount++;
        public void OnMouseDown(MouseButtonEventArgs e) { }
        public void OnMouseMove(MouseEventArgs e) { }
        public void OnMouseUp(MouseButtonEventArgs e) { }
        public void OnKeyDown(KeyEventArgs e) { }
        public void Render(DrawingContext dc, double overlayWidth, double overlayHeight) { }
        public Rectangle? GetSelectedRegion() => null;
    }

    private sealed class RenderPreviewMode : ICaptureMode
    {
        private readonly double _width;
        private readonly double _height;
        public RenderPreviewMode(double width, double height) { _width = width; _height = height; }
        public bool IsComplete => false;
        public bool IsCancelled => false;
        public Cursor? RequestedCursor => null;
        public void Initialize(Bitmap screenBitmap, FrameworkElement overlay) { }
        public void OnMouseDown(MouseButtonEventArgs e) { }
        public void OnMouseMove(MouseEventArgs e) { }
        public void OnMouseUp(MouseButtonEventArgs e) { }
        public void OnKeyDown(KeyEventArgs e) { }
        public Rectangle? GetSelectedRegion() => null;
        public void Render(DrawingContext dc, double overlayWidth, double overlayHeight)
        {
            var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Magenta, 4)
            {
                DashStyle = DashStyles.Dash
            };
            dc.DrawRectangle(
                null,
                pen,
                new Rect(_width * 0.18, _height * 0.2, _width * 0.64, _height * 0.62));
        }
    }
}
