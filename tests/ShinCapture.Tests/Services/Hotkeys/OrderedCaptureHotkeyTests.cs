using System.Reflection;
using System.Windows.Threading;
using ShinCapture.Services.Hotkeys;
using ShinCapture.Tests.Editor;

namespace ShinCapture.Tests.Services.Hotkeys;

public class OrderedCaptureHotkeyTests
{
    [Fact]
    public void NativeHookStartsOnDedicatedLoopAndDisposalStopsIt() => WatermarkFactoryTests.RunSta(() =>
    {
        using var hook = new OrderedCaptureHotkey(Dispatcher.CurrentDispatcher, () => { });
        Assert.True(hook.Start());
        var dispatcher = (Dispatcher)typeof(OrderedCaptureHotkey)
            .GetField("_hookDispatcher", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hook)!;
        Assert.NotSame(Dispatcher.CurrentDispatcher, dispatcher);
        hook.SetEnabled(false);
        hook.SetEnabled(true);
        hook.Dispose();
        Assert.True(SpinWait.SpinUntil(() => dispatcher.HasShutdownFinished, TimeSpan.FromSeconds(3)));
    });
}
