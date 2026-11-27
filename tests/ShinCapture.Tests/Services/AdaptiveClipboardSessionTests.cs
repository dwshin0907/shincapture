using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using ShinCapture.Helpers;
using ShinCapture.Services;

namespace ShinCapture.Tests.Services;

public sealed class AdaptiveClipboardSessionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"Clipboard 자동 전환 {Guid.NewGuid():N}");
    private readonly ClipboardImagePayload _payload;
    private readonly FakeWriter _writer = new();
    private bool _isKakao;
    private long _generation = 1;
    private readonly AdaptiveClipboardSession _session;

    public AdaptiveClipboardSessionTests()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "캡처 결과.png");
        using var bitmap = new Bitmap(3, 2, PixelFormat.Format32bppArgb);
        bitmap.SetPixel(0, 0, Color.Red);
        bitmap.SetPixel(2, 0, Color.FromArgb(0, 0, 0, 0));
        bitmap.SetPixel(0, 1, Color.Blue);
        var source = BitmapHelper.ToBitmapSource(bitmap);
        File.WriteAllBytes(path, BitmapHelper.EncodePng(source));
        _payload = new ClipboardImagePayload(source, path);
        _session = new AdaptiveClipboardSession(_writer, () => _isKakao, () => _generation);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task ForegroundMonitorStartsAndStopsWithoutChangingTheClipboard()
    {
        using var monitor = new AdaptiveImageClipboard(() => _generation);
        await monitor.Ready.WaitAsync(TimeSpan.FromSeconds(5));
        monitor.Dispose();
        await monitor.Stopped.WaitAsync(TimeSpan.FromSeconds(5));
        ClipboardWriteResult result = await monitor.PublishAsync(_payload, _generation);
        Assert.Equal(ClipboardWriteStatus.Failed, result.Status);
        Assert.IsType<ObjectDisposedException>(result.Error);
    }

    [Fact]
    public async Task ForegroundMonitorCanBeDisposedBeforeStartupFinishes()
    {
        using var monitor = new AdaptiveImageClipboard(() => _generation);
        monitor.Dispose();
        await monitor.Stopped.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TerminalToKakaoToTerminalSwitchesOnlyFormatsAndKeepsTheSameCapture()
    {
        Assert.Equal(ClipboardWriteStatus.Published, _session.Publish(_payload, _generation).Status);
        _isKakao = true;
        Assert.Equal(ClipboardWriteStatus.Published, _session.Refresh().Status);
        _isKakao = false;
        Assert.Equal(ClipboardWriteStatus.Published, _session.Refresh().Status);

        Assert.Equal(new[] { false, true, false }, _writer.Writes.Select(write => write.ImageOnly));
        Assert.All(_writer.Writes, write => Assert.Same(_payload, write.Payload));
        Assert.Null(_writer.Writes[0].Expected);
        Assert.All(_writer.Writes.Skip(1), write => Assert.NotNull(write.Expected));
    }

    [Fact]
    public void CapturePublishedWhileKakaoIsAlreadyActiveStartsAsImageOnly()
    {
        _isKakao = true;
        _session.Publish(_payload, _generation);
        Assert.True(Assert.Single(_writer.Writes).ImageOnly);
    }

    [Fact]
    public void CopyingOtherContentStopsAutomaticRestoration()
    {
        _isKakao = true;
        _session.Publish(_payload, _generation);
        _writer.ForeignCopy();
        _isKakao = false;
        Assert.Equal(ClipboardWriteStatus.Superseded, _session.Refresh().Status);
        _isKakao = true;
        Assert.Equal(ClipboardWriteStatus.Unchanged, _session.Refresh().Status);
        Assert.Single(_writer.Writes);
    }

    [Fact]
    public void ClipboardTokenMismatchStopsRestorationEvenIfSequenceMatches()
    {
        _session.Publish(_payload, _generation);
        _writer.Token = Guid.NewGuid().ToByteArray();
        _isKakao = true;
        Assert.Equal(ClipboardWriteStatus.Superseded, _session.Refresh().Status);
        Assert.Single(_writer.Writes);
    }

    [Fact]
    public void BusyRetryRechecksTheCurrentAppInsteadOfApplyingAnOldKakaoEvent()
    {
        _session.Publish(_payload, _generation);
        _isKakao = true;
        _writer.Busy = true;
        Assert.Equal(ClipboardWriteStatus.Busy, _session.Refresh().Status);
        _writer.Busy = false;
        _isKakao = false;
        Assert.Equal(ClipboardWriteStatus.Unchanged, _session.Refresh().Status);
        Assert.Single(_writer.Writes);
    }

    [Fact]
    public void AnotherCopyWhileWaitingForClipboardLockIsNeverOverwritten()
    {
        _session.Publish(_payload, _generation);
        _isKakao = true;
        _writer.BeforeLockedCheck = _writer.ForeignCopy;
        Assert.Equal(ClipboardWriteStatus.Superseded, _session.Refresh().Status);
        Assert.Single(_writer.Writes);
    }

    [Fact]
    public void NewGenerationInvalidatesPendingChangesAndOldPublications()
    {
        _session.Publish(_payload, _generation);
        _generation++;
        _isKakao = true;
        Assert.Equal(ClipboardWriteStatus.Unchanged, _session.Refresh().Status);
        Assert.Equal(ClipboardWriteStatus.Superseded, _session.Publish(_payload, _generation - 1).Status);
        Assert.Single(_writer.Writes);
        Assert.Equal(ClipboardWriteStatus.Published, _session.Publish(_payload, _generation).Status);
        Assert.True(_writer.Writes.Last().ImageOnly);
    }

    [Fact]
    public void NewCaptureDuringMemoryPreparationInvalidatesTheOlderWrite()
    {
        _writer.BeforeLockedCheck = () => _generation++;
        Assert.Equal(ClipboardWriteStatus.Superseded, _session.Publish(_payload, _generation).Status);
        Assert.Empty(_writer.Writes);
    }

    [Fact]
    public void PartialPublicationFailureStopsAutomaticChanges()
    {
        _session.Publish(_payload, _generation);
        _isKakao = true;
        _writer.Fail = true;
        Assert.Equal(ClipboardWriteStatus.Failed, _session.Refresh().Status);
        _writer.Fail = false;
        Assert.Equal(ClipboardWriteStatus.Unchanged, _session.Refresh().Status);
        Assert.Single(_writer.Writes);
    }

    [Theory]
    [InlineData("KakaoTalk", true)]
    [InlineData("kakaotalk", true)]
    [InlineData("KakaoTalkUpdater", false)]
    [InlineData("Code", false)]
    [InlineData("Antigravity", false)]
    [InlineData("WindowsTerminal", false)]
    [InlineData(null, false)]
    public void OnlyKakaoTalkProcessGetsTheImageOnlyException(string? processName, bool expected)
        => Assert.Equal(expected, AdaptiveClipboardSession.IsKakaoTalkProcess(processName));

    [Fact]
    public void CachedFormatsPreservePngAlphaDibRowAlignmentAndUnicodeFilePaths()
    {
        using var png = new Bitmap(new MemoryStream(_payload.Png));
        Assert.Equal(0, png.GetPixel(2, 0).A);
        Assert.Equal(Color.Red.ToArgb(), png.GetPixel(0, 0).ToArgb());

        Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(_payload.Dib));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(_payload.Dib.AsSpan(4)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(_payload.Dib.AsSpan(8)));
        Assert.Equal(24, BinaryPrimitives.ReadInt16LittleEndian(_payload.Dib.AsSpan(14)));
        Assert.Equal(40 + 12 * 2, _payload.Dib.Length); // 3 BGR pixels padded to 12 bytes per row.
        byte[] bmp = new byte[14 + _payload.Dib.Length];
        bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(2), bmp.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(10), 54);
        _payload.Dib.CopyTo(bmp, 14);
        using var dib = new Bitmap(new MemoryStream(bmp));
        Assert.Equal(Color.Red.ToArgb(), dib.GetPixel(0, 0).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), dib.GetPixel(0, 1).ToArgb());
        Assert.Equal(Color.White.ToArgb(), dib.GetPixel(2, 0).ToArgb());

        string path = Path.Combine(_directory, "캡처 결과.png");
        Assert.Equal($"\"{path}\"\0", Encoding.Unicode.GetString(_payload.PathText));
        Assert.Equal(20u, BinaryPrimitives.ReadUInt32LittleEndian(_payload.FileDrop));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(_payload.FileDrop.AsSpan(16)));
        Assert.Equal(path + "\0\0", Encoding.Unicode.GetString(_payload.FileDrop.AsSpan(20)));
    }

    private sealed class FakeWriter : IImageClipboardWriter
    {
        public readonly List<(ClipboardImagePayload Payload, bool ImageOnly, ClipboardStamp? Expected)> Writes = new();
        public byte[]? Token;
        public uint Sequence;
        public bool Busy;
        public bool Fail;
        public Action? BeforeLockedCheck;

        public void ForeignCopy() { Sequence++; Token = null; }

        public ClipboardWriteResult Write(ClipboardImagePayload payload, bool imageOnly,
            ClipboardStamp? expected = null, Func<bool>? isCurrent = null)
        {
            if (Busy) return new(ClipboardWriteStatus.Busy);
            BeforeLockedCheck?.Invoke();
            BeforeLockedCheck = null;
            if (isCurrent?.Invoke() == false || (expected is { } stamp &&
                (stamp.Sequence != Sequence || Token == null || !stamp.Token.SequenceEqual(Token))))
                return new(ClipboardWriteStatus.Superseded);
            if (Fail) return new(ClipboardWriteStatus.Failed);
            Writes.Add((payload, imageOnly, expected));
            Token = payload.Token;
            return new(ClipboardWriteStatus.Published, new ClipboardStamp(++Sequence, Token));
        }
    }
}
