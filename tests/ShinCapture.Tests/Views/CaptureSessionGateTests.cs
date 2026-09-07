using ShinCapture.Models;
using ShinCapture.Views;

namespace ShinCapture.Tests.Views;

public class CaptureSessionGateTests
{
    [Fact]
    public void RejectsRapidSecondRequestUntilCancelledSessionCompletes()
    {
        var gate = new CaptureSessionGate();

        Assert.True(gate.TryBegin(CaptureMode.SmartCut, false, out CaptureSession? smartCut));
        Assert.NotNull(smartCut);
        Assert.False(gate.TryBegin(CaptureMode.Region, false, out CaptureSession? rejected));
        Assert.Null(rejected);
        Assert.Same(smartCut, gate.ActiveSession);

        Assert.True(gate.Complete(smartCut));
        Assert.True(gate.TryBegin(CaptureMode.Region, false, out CaptureSession? region));
        Assert.NotNull(region);
        Assert.NotEqual(smartCut.Id, region.Id);
        Assert.Equal(CaptureMode.Region, region.Mode);
    }

    [Fact]
    public void LateScrollCompletionCannotReleaseNewCaptureSession()
    {
        var gate = new CaptureSessionGate();
        Assert.True(gate.TryBegin(CaptureMode.Scroll, false, out CaptureSession? scroll));
        Assert.NotNull(scroll);
        Assert.True(gate.Complete(scroll));

        Assert.True(gate.TryBegin(CaptureMode.Translate, true, out CaptureSession? translate));
        Assert.NotNull(translate);
        Assert.Equal(CaptureMode.Translate, translate.Mode);
        Assert.True(translate.EditorAutoTranslate);

        Assert.False(gate.Complete(scroll));
        Assert.Same(translate, gate.ActiveSession);
        Assert.False(gate.TryBegin(CaptureMode.Freeform, false, out _));
    }

    [Fact]
    public void ResetReleasesActiveSessionForApplicationExit()
    {
        var gate = new CaptureSessionGate();
        Assert.True(gate.TryBegin(CaptureMode.SmartCut, false, out CaptureSession? smartCut));

        CaptureSession? released = gate.Reset();

        Assert.Same(smartCut, released);
        Assert.Null(gate.ActiveSession);
        Assert.True(gate.TryBegin(CaptureMode.Region, false, out _));
    }
}
