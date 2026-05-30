using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
        using var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(stream);
        stream.Position = 0;
        return new Bitmap(stream);
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
        if (source == null) return;
        try
        {
            // PNG 스트림 (알파 채널 보존)
            var pngStream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(pngStream);
            pngStream.Position = 0;

            // System.Drawing.Bitmap (흰배경 합성, CF_BITMAP/CF_DIB로 등록될 형식)
            using var rawBmp = ToBitmap(source);
            var flatBmp = FlattenAlphaToWhite(rawBmp);

            var dataObj = new System.Windows.Forms.DataObject();
            dataObj.SetData("PNG", false, pngStream);
            dataObj.SetData(System.Windows.Forms.DataFormats.Bitmap, true, flatBmp);

            System.Windows.Forms.Clipboard.SetDataObject(dataObj, copy: true);
        }
        catch
        {
            // 클립보드 접근 실패 시 표준 SetImage로 폴백
            try { System.Windows.Clipboard.SetImage(source); } catch { }
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
