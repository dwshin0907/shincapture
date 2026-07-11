namespace ShinCapture.Models;

public sealed record TrayCaptureAction(
    CaptureMode Mode,
    string Label,
    string Shortcut,
    string IconKey,
    bool IsWide = false);
