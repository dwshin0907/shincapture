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
    /// <summary>지정 언어로 OCR 실행. 이미지 전처리(업스케일 + 자동 반전) 활성화(기본 true).</summary>
    public static Task<string> ExtractTextAsync(Bitmap image, string langTag)
        => ExtractTextAsync(image, langTag, preprocess: true);

    /// <summary>지정 언어로 OCR 실행.</summary>
    /// <param name="preprocess">true면 OCR 친화적 전처리(업스케일, 어두운 배경 자동 반전) 적용.</param>
    /// <exception cref="InvalidOperationException">해당 언어팩이 설치되어 있지 않을 때</exception>
    public static async Task<string> ExtractTextAsync(Bitmap image, string langTag, bool preprocess)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        if (string.IsNullOrWhiteSpace(langTag)) throw new ArgumentException("langTag empty", nameof(langTag));

        var language = new Language(langTag);
        var engine = OcrEngine.TryCreateFromLanguage(language)
            ?? throw new InvalidOperationException($"OCR 언어팩이 설치되지 않았습니다: {langTag}");

        Bitmap target = image;
        Bitmap? processed = null;
        try
        {
            if (preprocess)
            {
                processed = PreprocessForOcr(image);
                if (processed != null) target = processed;
            }

            var softwareBitmap = await BitmapToSoftwareBitmapAsync(target);
            var result = await engine.RecognizeAsync(softwareBitmap);
            // result.Text는 모든 단어를 공백으로 join하여 줄바꿈을 잃는다.
            // result.Lines를 \n으로 join하여 시각적 줄 구조 보존.
            if (result.Lines == null || result.Lines.Count == 0)
                return result.Text ?? string.Empty;
            return string.Join("\n", result.Lines.Select(l => l.Text ?? string.Empty));
        }
        finally
        {
            processed?.Dispose();
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

    /// <summary>
    /// OCR 인식률 향상을 위한 이미지 전처리 체인.
    /// 1. 공격적 업스케일 — Windows.Media.Ocr은 텍스트 높이 20~40px에서 최적. 작은 이미지는 3~4배 확대.
    /// 2. 어두운 배경 자동 반전 — Windows OCR은 "흰 배경 검은 글씨" 기준 학습. 다크 테마 캡쳐는 반전 시 인식률 크게 향상.
    /// </summary>
    private static Bitmap? PreprocessForOcr(Bitmap source)
    {
        int minDim = Math.Min(source.Width, source.Height);
        double scale = minDim switch
        {
            < 50  => 4.0,
            < 100 => 3.0,
            < 200 => 2.0,
            _     => 1.0
        };

        int newW = Math.Max(1, (int)(source.Width * scale));
        int newH = Math.Max(1, (int)(source.Height * scale));

        // 스케일 == 1이고 반전도 불필요한 경우 null 반환 → 호출자는 원본을 그대로 사용
        bool needInvert = IsDarkDominant(source);
        if (scale == 1.0 && !needInvert) return null;

        var scaled = new Bitmap(newW, newH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            if (needInvert)
            {
                var matrix = new ColorMatrix(new float[][]
                {
                    new float[] { -1,  0,  0, 0, 0 },
                    new float[] {  0, -1,  0, 0, 0 },
                    new float[] {  0,  0, -1, 0, 0 },
                    new float[] {  0,  0,  0, 1, 0 },
                    new float[] {  1,  1,  1, 0, 1 }
                });
                using var attrs = new ImageAttributes();
                attrs.SetColorMatrix(matrix);
                g.DrawImage(source,
                    new Rectangle(0, 0, newW, newH),
                    0, 0, source.Width, source.Height,
                    GraphicsUnit.Pixel, attrs);
            }
            else
            {
                g.DrawImage(source, 0, 0, newW, newH);
            }
        }
        return scaled;
    }

    /// <summary>
    /// 격자 샘플링으로 평균 명도를 계산. 128 미만이면 어두운 배경으로 판단.
    /// </summary>
    private static bool IsDarkDominant(Bitmap bmp)
    {
        int stepX = Math.Max(1, bmp.Width / 10);
        int stepY = Math.Max(1, bmp.Height / 10);
        int samples = 0;
        long totalBrightness = 0;
        for (int y = 0; y < bmp.Height; y += stepY)
        {
            for (int x = 0; x < bmp.Width; x += stepX)
            {
                var c = bmp.GetPixel(x, y);
                // 표준 휘도 가중치 (Rec. 601)
                int brightness = (c.R * 299 + c.G * 587 + c.B * 114) / 1000;
                totalBrightness += brightness;
                samples++;
            }
        }
        return samples > 0 && (totalBrightness / samples) < 128;
    }
}
