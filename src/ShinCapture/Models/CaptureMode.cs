namespace ShinCapture.Models;

public enum CaptureMode
{
    Region, Freeform, Window, Element, Fullscreen, Scroll, FixedSize, Text,
    Translate   // 신규: 영역 → OCR → 번역 한 큐
}

public enum AfterCaptureAction
{
    OpenEditor, SaveDirectly, ClipboardOnly
}
