using System.Drawing;
using System.Drawing.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace ShinCapture.Capture;

/// <summary>
/// GrabCut 알고리즘으로 이미지 배경을 제거하는 헬퍼.
/// 사용자가 polygon을 안 그린 일반 사각형 캡쳐 결과에 적용 — 이미지 가장자리를 sure background,
/// 안쪽을 probable foreground로 가정하고 GrabCut(InitWithRect)으로 자동 분리.
/// </summary>
public static class GrabCutHelper
{
    /// <param name="source">원본 비트맵</param>
    /// <param name="marginPercent">가장자리 마진 비율 (기본 5%) — 이 영역은 sure background로 처리</param>
    /// <param name="iterations">GrabCut 반복 횟수 (기본 5)</param>
    public static Bitmap RemoveBackground(Bitmap source, double marginPercent = 0.05, int iterations = 5)
    {
        int w = source.Width;
        int h = source.Height;

        using var src = BitmapConverter.ToMat(source);
        Mat src3;
        if (src.Channels() == 4)
        {
            src3 = new Mat();
            Cv2.CvtColor(src, src3, ColorConversionCodes.BGRA2BGR);
        }
        else if (src.Channels() == 3)
        {
            src3 = src.Clone();
        }
        else
        {
            src3 = new Mat();
            Cv2.CvtColor(src, src3, ColorConversionCodes.GRAY2BGR);
        }

        // 가장자리 마진 영역 = sure background, 안쪽 rect = probable foreground
        int marginX = System.Math.Max(1, (int)(w * marginPercent));
        int marginY = System.Math.Max(1, (int)(h * marginPercent));
        var rect = new OpenCvSharp.Rect(
            marginX, marginY,
            System.Math.Max(1, w - 2 * marginX),
            System.Math.Max(1, h - 2 * marginY));

        using var mask = new Mat();
        using var bgdModel = new Mat();
        using var fgdModel = new Mat();

        Cv2.GrabCut(src3, mask, rect, bgdModel, fgdModel, iterations, GrabCutModes.InitWithRect);

        // 결과 마스크에서 FGD/PR_FGD 픽셀만 전경으로
        using var fgMask = new Mat();
        Cv2.Compare(mask, new Scalar((double)GrabCutClasses.PR_FGD), fgMask, CmpType.EQ);
        using var fgMask2 = new Mat();
        Cv2.Compare(mask, new Scalar((double)GrabCutClasses.FGD), fgMask2, CmpType.EQ);
        Cv2.BitwiseOr(fgMask, fgMask2, fgMask);

        src3.Dispose();

        // 32bpp ARGB 결과: 전경 = 원본 + 알파 255, 배경 = 알파 0
        var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var data = result.LockBits(
            new Rectangle(0, 0, w, h),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            unsafe
            {
                byte* dst = (byte*)data.Scan0;
                int stride = data.Stride;
                var indexer = fgMask.GetGenericIndexer<byte>();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int offset = y * stride + x * 4;
                        if (indexer[y, x] != 0)
                        {
                            var p = source.GetPixel(x, y);
                            dst[offset + 0] = p.B;
                            dst[offset + 1] = p.G;
                            dst[offset + 2] = p.R;
                            dst[offset + 3] = 255;
                        }
                        else
                        {
                            dst[offset + 0] = 0;
                            dst[offset + 1] = 0;
                            dst[offset + 2] = 0;
                            dst[offset + 3] = 0;
                        }
                    }
                }
            }
        }
        finally
        {
            result.UnlockBits(data);
        }
        return result;
    }
}
