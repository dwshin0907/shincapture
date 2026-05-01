using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace ShinCapture.Helpers;

public static class BitmapHelper
{
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.StreamSource = stream;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
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
    /// 클립보드에 이미지를 PNG + 표준 Bitmap 둘 다 넣는다.
    /// - PNG 키: 포토샵/디스코드 등은 PNG 우선 → 알파 채널 보존
    /// - 표준 Bitmap: 카카오톡/MS Office 등은 CF_BITMAP/CF_DIB 우선 → 호환성
    /// 표준 Bitmap fallback에서는 알파 영역이 검정으로 안 보이도록 흰색 배경 합성.
    /// </summary>
    public static void SetClipboardPng(BitmapSource source)
    {
        if (source == null) return;
        try
        {
            var dataObj = new System.Windows.DataObject();

            // 1) PNG 형식 (알파 보존)
            using (var pngStream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(pngStream);
                pngStream.Position = 0;
                dataObj.SetData("PNG", pngStream);
            }

            // 2) 표준 Bitmap (CF_BITMAP/CF_DIB) — 카톡 등 호환. 알파는 흰 배경으로 합성.
            var flat = FlattenAlphaToWhite(source);
            dataObj.SetImage(flat);

            System.Windows.Clipboard.SetDataObject(dataObj, copy: true);
        }
        catch
        {
            // 클립보드 접근 실패 시 표준 SetImage로 폴백
            try { System.Windows.Clipboard.SetImage(source); } catch { }
        }
    }

    /// <summary>알파 채널을 흰색 배경으로 합성한 BitmapSource 반환 (자유형 캡쳐 등 투명 영역 처리).</summary>
    private static BitmapSource FlattenAlphaToWhite(BitmapSource source)
    {
        int w = source.PixelWidth;
        int h = source.PixelHeight;
        var dv = new System.Windows.Media.DrawingVisual();
        using (var ctx = dv.RenderOpen())
        {
            ctx.DrawRectangle(System.Windows.Media.Brushes.White, null,
                new System.Windows.Rect(0, 0, w, h));
            ctx.DrawImage(source, new System.Windows.Rect(0, 0, w, h));
        }
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
            w, h, source.DpiX > 0 ? source.DpiX : 96.0,
            source.DpiY > 0 ? source.DpiY : 96.0,
            System.Windows.Media.PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }
}
