using System;
using System.Drawing;

namespace ShinCapture.Models;

public class CaptureResult
{
    public required Bitmap Image { get; init; }
    public Rectangle Region { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
