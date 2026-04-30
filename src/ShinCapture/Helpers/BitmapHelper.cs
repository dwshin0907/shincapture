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
    /// 클립보드에 PNG 형식으로만 이미지를 넣는다 (알파 채널 보존).
    /// 표준 Clipboard.SetImage는 BMP/DIB로 저장해 받는 앱이 알파 영역을 검정으로 표시하는 문제 회피.
    /// 트레이드오프: PNG 형식만 인식하지 못하는 일부 오래된 앱은 이미지를 못 받을 수 있다.
    /// </summary>
    public static void SetClipboardPng(BitmapSource source)
    {
        if (source == null) return;
        try
        {
            var dataObj = new System.Windows.DataObject();
            using var pngStream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            encoder.Save(pngStream);
            pngStream.Position = 0;
            // 다양한 앱이 검색하는 키 이름 모두 등록
            dataObj.SetData("PNG", pngStream);
            System.Windows.Clipboard.SetDataObject(dataObj, copy: true);
        }
        catch
        {
            // 클립보드 접근 실패 시 표준 SetImage로 폴백
            try { System.Windows.Clipboard.SetImage(source); } catch { }
        }
    }
}
