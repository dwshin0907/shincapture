using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ShinCapture.Helpers;

public static class BitmapHelper
{
    /// <summary>
    /// System.Drawing.Bitmap → WPF BitmapSource. PNG 인코딩/디코딩 없이 픽셀 버퍼를 직접 복사한다.
    /// 캡쳐 시작(전체 화면)과 돋보기(마우스 이동마다)에서 호출되는 핫패스라 PNG 왕복은 버벅임의 원인이었음.
    /// GDI+ Format32bppArgb(비프리멀티 BGRA)와 WPF Bgra32가 메모리 레이아웃이 동일해 그대로 복사 가능.
    /// </summary>
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        // 요청 포맷이 원본과 달라도 GDI+가 잠금 시점에 변환해준다 (24bpp → 32bpp 등).
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var source = BitmapSource.Create(
                bitmap.Width, bitmap.Height,
                96, 96,
                System.Windows.Media.PixelFormats.Bgra32, null,
                data.Scan0, data.Stride * bitmap.Height, data.Stride);
            source.Freeze();
            return source;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public static Bitmap ToBitmap(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        BitmapSource bgraSource = source;
        if (source.Format != System.Windows.Media.PixelFormats.Bgra32)
        {
            var converted = new FormatConvertedBitmap(
                source,
                System.Windows.Media.PixelFormats.Bgra32,
                destinationPalette: null,
                alphaThreshold: 0);
            converted.Freeze();
            bgraSource = converted;
        }

        int width = bgraSource.PixelWidth;
        int height = bgraSource.PixelHeight;
        int sourceStride = checked(width * 4);
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try
        {
            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                if (data.Stride == sourceStride)
                {
                    bgraSource.CopyPixels(
                        Int32Rect.Empty,
                        data.Scan0,
                        checked(data.Stride * height),
                        data.Stride);
                }
                else
                {
                    var pixels = new byte[checked(sourceStride * height)];
                    bgraSource.CopyPixels(pixels, sourceStride, 0);
                    for (int y = 0; y < height; y++)
                    {
                        Marshal.Copy(
                            pixels,
                            y * sourceStride,
                            IntPtr.Add(data.Scan0, y * data.Stride),
                            sourceStride);
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    public static byte[] EncodePng(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// 클립보드에 이미지를 PNG + System.Drawing.Bitmap 듀얼로 등록한다.
    /// - PNG 형식: 포토샵/디스코드 등이 우선 인식 → 알파 채널 보존
    /// - System.Drawing.Bitmap: WinForms 클립보드가 자동으로 CF_BITMAP + CF_DIB로 등록
    ///   → PowerPoint/Word/한글/카카오톡/메모장 등 데스크톱 앱 호환성 최대.
    /// 알파 영역은 흰배경으로 합성된 Bitmap을 별도 등록 (검정 사각형 방지).
    /// </summary>
    public static void SetClipboardPng(BitmapSource source)
    {
        _ = TrySetClipboardPng(source, out _);
    }

    public static bool TrySetClipboardPng(BitmapSource source, out Exception? error)
    {
        ArgumentNullException.ThrowIfNull(source);
        error = null;

        try
        {
            byte[] pngBytes = EncodePng(source);
            using var pngStream = new MemoryStream(pngBytes, writable: false);
            using var rawBitmap = ToBitmap(source);
            using var flatBitmap = FlattenAlphaToWhite(rawBitmap);

            var dataObject = new System.Windows.Forms.DataObject();
            dataObject.SetData("PNG", autoConvert: false, pngStream);
            dataObject.SetData(
                System.Windows.Forms.DataFormats.Bitmap,
                autoConvert: true,
                flatBitmap);

            int[] retryDelaysMs = [0, 15, 40, 80];
            foreach (int delayMs in retryDelaysMs)
            {
                if (delayMs > 0) Thread.Sleep(delayMs);
                try
                {
                    pngStream.Position = 0;
                    System.Windows.Forms.Clipboard.SetDataObject(dataObject, copy: true);
                    return true;
                }
                catch (ExternalException ex)
                {
                    error = ex;
                }
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }

        try
        {
            System.Windows.Clipboard.SetImage(source);
            return true;
        }
        catch (Exception fallbackError)
        {
            error = error == null
                ? fallbackError
                : new AggregateException(error, fallbackError);
            return false;
        }
    }

    /// <summary>System.Drawing.Bitmap의 알파 채널을 흰색 배경으로 합성. 32bpp ARGB → 24bpp RGB.</summary>
    private static Bitmap FlattenAlphaToWhite(Bitmap source)
    {
        var flat = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        flat.SetResolution(source.HorizontalResolution, source.VerticalResolution);
        using (var g = Graphics.FromImage(flat))
        {
            g.Clear(Color.White);
            g.DrawImage(source, 0, 0, source.Width, source.Height);
        }
        return flat;
    }
}
