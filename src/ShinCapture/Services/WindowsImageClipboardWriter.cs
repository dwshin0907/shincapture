using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ShinCapture.Services;

internal sealed class WindowsImageClipboardWriter(IntPtr owner) : IImageClipboardWriter
{
    private readonly uint _pngFormat = Register("PNG");
    private readonly uint _tokenFormat = Register("ShinCapture.ImageClipboard.Session");
    private readonly uint _historyFormat = Register("CanIncludeInClipboardHistory");

    public ClipboardWriteResult Write(
        ClipboardImagePayload payload,
        bool imageOnly,
        ClipboardStamp? expected = null,
        Func<bool>? isCurrent = null)
    {
        var blocks = new List<ClipboardMemory>();
        bool opened = false;
        try
        {
            // Prepare every allocation before opening or emptying the clipboard.
            blocks.Add(new(_pngFormat, payload.Png));
            blocks.Add(new(8, payload.Dib)); // CF_DIB; Windows also synthesizes CF_BITMAP.
            blocks.Add(new(_tokenFormat, payload.Token));
            if (!imageOnly)
            {
                blocks.Add(new(15, payload.FileDrop)); // CF_HDROP
                blocks.Add(new(13, payload.PathText)); // CF_UNICODETEXT
            }
            if (expected.HasValue)
                blocks.Add(new(_historyFormat, new byte[4])); // Don't duplicate history on app switches.

            if (!OpenClipboard(owner)) return new(ClipboardWriteStatus.Busy);
            opened = true;

            // The check and replacement share the same OS clipboard lock.
            if (isCurrent?.Invoke() == false ||
                (expected is { } stamp && !Matches(stamp)))
                return new(ClipboardWriteStatus.Superseded);

            if (!EmptyClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error());
            foreach (ClipboardMemory block in blocks) block.Transfer();
            // CloseClipboard synthesizes additional formats and advances the sequence again.
            // The next conditional write also verifies owner + token under lock, so a foreign
            // copy between CloseClipboard and this read can never authorize overwriting it.
            if (!CloseClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error());
            opened = false;
            return new(ClipboardWriteStatus.Published,
                new ClipboardStamp(GetClipboardSequenceNumber(), payload.Token));
        }
        catch (Exception ex)
        {
            return new(ClipboardWriteStatus.Failed, Error: ex);
        }
        finally
        {
            if (opened) CloseClipboard();
            foreach (ClipboardMemory block in blocks) block.Dispose();
        }
    }

    private bool Matches(ClipboardStamp expected)
    {
        if (GetClipboardOwner() != owner || GetClipboardSequenceNumber() != expected.Sequence)
            return false;
        IntPtr handle = GetClipboardData(_tokenFormat);
        if (handle == IntPtr.Zero || GlobalSize(handle).ToUInt64() < (ulong)expected.Token.Length)
            return false;
        IntPtr memory = GlobalLock(handle);
        if (memory == IntPtr.Zero) return false;
        try
        {
            var actual = new byte[expected.Token.Length];
            Marshal.Copy(memory, actual, 0, actual.Length);
            return actual.AsSpan().SequenceEqual(expected.Token);
        }
        finally { GlobalUnlock(handle); }
    }

    private static uint Register(string name)
    {
        uint format = RegisterClipboardFormat(name);
        return format != 0 ? format : throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private sealed class ClipboardMemory : IDisposable
    {
        private IntPtr _handle;
        private readonly uint _format;

        public ClipboardMemory(uint format, byte[] bytes)
        {
            _format = format;
            _handle = GlobalAlloc(0x0042, (UIntPtr)bytes.Length); // GMEM_MOVEABLE | GMEM_ZEROINIT
            if (_handle == IntPtr.Zero) throw new OutOfMemoryException();
            IntPtr memory = GlobalLock(_handle);
            if (memory == IntPtr.Zero)
            {
                Dispose();
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            try { Marshal.Copy(bytes, 0, memory, bytes.Length); }
            catch { DisposeAfterUnlock(); throw; }
            finally { if (_handle != IntPtr.Zero) GlobalUnlock(_handle); }
        }

        private void DisposeAfterUnlock()
        {
            GlobalUnlock(_handle);
            Dispose();
        }

        public void Transfer()
        {
            if (SetClipboardData(_format, _handle) == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            _handle = IntPtr.Zero; // The OS owns successful transfers, including partial publication.
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            GlobalFree(_handle);
            _handle = IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr handle);
    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardOwner();
    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr handle);
    [DllImport("kernel32.dll")]
    private static extern UIntPtr GlobalSize(IntPtr handle);
}
