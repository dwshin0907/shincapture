using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using ShinCapture.Helpers;

namespace ShinCapture.Services;

internal sealed class ClipboardImagePayload
{
    public byte[] Png { get; }
    public byte[] Dib { get; }
    public byte[] PathText { get; }
    public byte[] FileDrop { get; }
    public byte[] Token { get; } = Guid.NewGuid().ToByteArray();

    public ClipboardImagePayload(BitmapSource source, string path)
    {
        string fullPath = Path.GetFullPath(path);
        PathText = Encoding.Unicode.GetBytes(BitmapHelper.QuotePathForTerminal(fullPath) + '\0');
        Png = File.ReadAllBytes(fullPath);
        Dib = BitmapHelper.EncodeClipboardDib(source);

        byte[] fileNames = Encoding.Unicode.GetBytes(fullPath + "\0\0");
        FileDrop = new byte[20 + fileNames.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(FileDrop.AsSpan(0, 4), 20); // DROPFILES.pFiles
        BinaryPrimitives.WriteUInt32LittleEndian(FileDrop.AsSpan(16, 4), 1); // DROPFILES.fWide
        fileNames.CopyTo(FileDrop, 20);
    }
}

internal enum ClipboardWriteStatus { Published, Busy, Superseded, Failed, Unchanged }

internal readonly record struct ClipboardStamp(uint Sequence, byte[] Token);

internal readonly record struct ClipboardWriteResult(
    ClipboardWriteStatus Status,
    ClipboardStamp Stamp = default,
    Exception? Error = null);

internal interface IImageClipboardWriter
{
    ClipboardWriteResult Write(
        ClipboardImagePayload payload,
        bool imageOnly,
        ClipboardStamp? expected = null,
        Func<bool>? isCurrent = null);
}

internal sealed class AdaptiveClipboardSession(
    IImageClipboardWriter writer,
    Func<bool> isKakaoTalk,
    Func<long> currentGeneration)
{
    private ClipboardImagePayload? _payload;
    private ClipboardStamp _stamp;
    private bool _imageOnly;
    private long _generation;

    public ClipboardWriteResult Publish(ClipboardImagePayload payload, long generation)
    {
        if (generation != currentGeneration())
            return new(ClipboardWriteStatus.Superseded);

        bool imageOnly = isKakaoTalk();
        ClipboardWriteResult result = writer.Write(payload, imageOnly,
            isCurrent: () => generation == currentGeneration());
        if (result.Status == ClipboardWriteStatus.Published)
        {
            _payload = payload;
            _stamp = result.Stamp;
            _imageOnly = imageOnly;
            _generation = generation;
        }
        else if (result.Status == ClipboardWriteStatus.Failed)
            _payload = null;
        return result;
    }

    public ClipboardWriteResult Refresh()
    {
        if (_generation != currentGeneration()) _payload = null;
        if (_payload == null) return new(ClipboardWriteStatus.Unchanged);

        bool imageOnly = isKakaoTalk();
        if (_imageOnly == imageOnly) return new(ClipboardWriteStatus.Unchanged);

        ClipboardWriteResult result = writer.Write(_payload, imageOnly, _stamp,
            () => _generation == currentGeneration());
        if (result.Status == ClipboardWriteStatus.Published)
        {
            _stamp = result.Stamp;
            _imageOnly = imageOnly;
        }
        else if (result.Status is ClipboardWriteStatus.Superseded or ClipboardWriteStatus.Failed)
            _payload = null;
        return result;
    }

    internal static bool IsKakaoTalkProcess(string? processName) =>
        string.Equals(processName, "KakaoTalk", StringComparison.OrdinalIgnoreCase);
}
