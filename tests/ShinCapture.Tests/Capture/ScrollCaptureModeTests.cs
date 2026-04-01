using ShinCapture.Capture;

namespace ShinCapture.Tests.Capture;

public class ScrollCaptureModeTests
{
    [Fact]
    public void ScrollCaptureMode_InitialState_NotComplete()
    {
        var mode = new ScrollCaptureMode();
        Assert.False(mode.IsComplete);
        Assert.False(mode.IsCancelled);
        Assert.Null(mode.GetStitchedBitmap());
    }
}
