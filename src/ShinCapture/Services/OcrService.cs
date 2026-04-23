using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace ShinCapture.Services;

/// <summary>
/// Windows.Media.Ocr WinRT API를 감싸는 정적 서비스.
/// System.Drawing.Bitmap 입력 → 추출된 텍스트 출력. UI 레이어는 WinRT 타입을 직접 다루지 않음.
/// </summary>
public static class OcrService
{
    /// <summary>지정 언어로 OCR 실행. 작은 이미지는 자동 업스케일(기본 true).</summary>
    public static Task<string> ExtractTextAsync(Bitmap image, string langTag)
        => ExtractTextAsync(image, langTag, upscaleSmall: true);

    /// <summary>지정 언어로 OCR 실행.</summary>
    /// <exception cref="InvalidOperationException">해당 언어팩이 설치되어 있지 않을 때</exception>
    public static async Task<string> ExtractTextAsync(Bitmap image, string langTag, bool upscaleSmall)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        if (string.IsNullOrWhiteSpace(langTag)) throw new ArgumentException("langTag empty", nameof(langTag));

        var language = new Language(langTag);
        var engine = OcrEngine.TryCreateFromLanguage(language)
            ?? throw new InvalidOperationException($"OCR 언어팩이 설치되지 않았습니다: {langTag}");

        Bitmap target = image;
        Bitmap? upscaled = null;
        try
        {
            if (upscaleSmall && (image.Width < 40 || image.Height < 40))
            {
                upscaled = new Bitmap(image.Width * 2, image.Height * 2);
                using (var g = Graphics.FromImage(upscaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image, 0, 0, upscaled.Width, upscaled.Height);
                }
                target = upscaled;
            }

            var softwareBitmap = await BitmapToSoftwareBitmapAsync(target);
            var result = await engine.RecognizeAsync(softwareBitmap);
            return result.Text ?? string.Empty;
        }
        finally
        {
            upscaled?.Dispose();
        }
    }

    /// <summary>현재 Windows에 설치된 OCR 사용 가능 언어 목록 (BCP-47 태그).</summary>
    public static IReadOnlyList<string> GetAvailableLanguages()
        => OcrEngine.AvailableRecognizerLanguages.Select(l => l.LanguageTag).ToList();

    /// <summary>해당 BCP-47 언어 태그로 OCR이 가능한지.</summary>
    public static bool IsLanguageAvailable(string langTag)
    {
        try
        {
            return OcrEngine.IsLanguageSupported(new Language(langTag));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// preferred 언어가 사용 가능하면 그대로 반환. 아니면 ko → en-US → 첫 번째 사용 가능한 언어 순서로 폴백.
    /// 설치된 OCR 언어팩이 전혀 없으면 null.
    /// </summary>
    public static string? ResolveLanguageOrFallback(string preferred)
    {
        if (IsLanguageAvailable(preferred)) return preferred;
        if (IsLanguageAvailable("ko")) return "ko";
        if (IsLanguageAvailable("en-US")) return "en-US";
        var list = GetAvailableLanguages();
        return list.Count > 0 ? list[0] : null;
    }

    private static async Task<SoftwareBitmap> BitmapToSoftwareBitmapAsync(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
        return await decoder.GetSoftwareBitmapAsync();
    }
}
