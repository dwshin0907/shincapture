namespace ShinCapture.Models;

public enum CaptureMode
{
    Region, Freeform, Window, Element, Fullscreen, Scroll, FixedSize,
    Text,
}

public enum AfterCaptureAction
{
    OpenEditor, SaveDirectly, ClipboardOnly
}
