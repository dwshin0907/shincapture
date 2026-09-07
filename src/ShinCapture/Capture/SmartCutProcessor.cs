using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using CvPoint = OpenCvSharp.Point;

namespace ShinCapture.Capture;

public static class SmartCutProcessor
{
    private const int MaxWorkingPixels = 1_000_000;
    private const int MaxWorkingLongEdge = 1400;
    private const int GrabCutIterations = 3;
    private static readonly SemaphoreSlim ProcessingGate = new(1, 1);

    public static Bitmap Process(
        Bitmap croppedBitmap,
        IReadOnlyList<PointF> polygon,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(croppedBitmap);
        ArgumentNullException.ThrowIfNull(polygon);
        cancellationToken.ThrowIfCancellationRequested();

        if (croppedBitmap.Width <= 0 || croppedBitmap.Height <= 0)
            throw new ArgumentException("스마트 컷 이미지는 비어 있을 수 없습니다.", nameof(croppedBitmap));

        ProcessingGate.Wait(cancellationToken);
        try
        {
            PointF[] sourcePolygon = NormalizePolygon(
                polygon,
                croppedBitmap.Width,
                croppedBitmap.Height);
            using Mat lassoAlpha = CreatePolygonMask(
                croppedBitmap.Width,
                croppedBitmap.Height,
                sourcePolygon,
                antiAlias: true);

            try
            {
                using Mat refinedAlpha = ExtractAlpha(
                    croppedBitmap,
                    sourcePolygon,
                    lassoAlpha,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return ApplyAlpha(croppedBitmap, refinedAlpha, cancellationToken);
            }
            catch (OpenCVException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ApplyAlpha(croppedBitmap, lassoAlpha, cancellationToken);
            }
        }
        finally
        {
            ProcessingGate.Release();
        }
    }

    private static Mat ExtractAlpha(
        Bitmap sourceBitmap,
        PointF[] sourcePolygon,
        Mat lassoAlpha,
        CancellationToken cancellationToken)
    {
        (int workWidth, int workHeight, double scale) = CalculateWorkingSize(
            sourceBitmap.Width,
            sourceBitmap.Height);
        PointF[] workPolygon = sourcePolygon
            .Select(point => new PointF((float)(point.X * scale), (float)(point.Y * scale)))
            .ToArray();

        using Mat original = BitmapConverter.ToMat(sourceBitmap);
        using var bgr = new Mat();
        ConvertToBgr(original, bgr);
        using var workImage = new Mat();
        if (workWidth == sourceBitmap.Width && workHeight == sourceBitmap.Height)
            bgr.CopyTo(workImage);
        else
            Cv2.Resize(bgr, workImage, new OpenCvSharp.Size(workWidth, workHeight),
                interpolation: InterpolationFlags.Area);

        cancellationToken.ThrowIfCancellationRequested();
        using Mat workLasso = CreatePolygonMask(
            workWidth,
            workHeight,
            workPolygon,
            antiAlias: false);
        int foregroundPixels = Cv2.CountNonZero(workLasso);
        int backgroundPixels = checked(workWidth * workHeight) - foregroundPixels;
        if (foregroundPixels == 0 || backgroundPixels == 0)
            return lassoAlpha.Clone();

        using var grabMask = new Mat(
            workHeight,
            workWidth,
            MatType.CV_8UC1,
            new Scalar((double)GrabCutClasses.BGD));
        grabMask.SetTo(new Scalar((double)GrabCutClasses.PR_FGD), workLasso);

        using var backgroundModel = new Mat();
        using var foregroundModel = new Mat();
        for (int iteration = 0; iteration < GrabCutIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Cv2.GrabCut(
                workImage,
                grabMask,
                default(OpenCvSharp.Rect),
                backgroundModel,
                foregroundModel,
                1,
                iteration == 0 ? GrabCutModes.InitWithMask : GrabCutModes.Eval);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var definiteForeground = new Mat();
        using var probableForeground = new Mat();
        using var workForeground = new Mat();
        Cv2.Compare(grabMask, new Scalar((double)GrabCutClasses.FGD),
            definiteForeground, CmpType.EQ);
        Cv2.Compare(grabMask, new Scalar((double)GrabCutClasses.PR_FGD),
            probableForeground, CmpType.EQ);
        Cv2.BitwiseOr(definiteForeground, probableForeground, workForeground);
        Cv2.BitwiseAnd(workForeground, workLasso, workForeground);
        if (Cv2.CountNonZero(workForeground) == 0)
            return lassoAlpha.Clone();

        using var hardAlpha = new Mat();
        Cv2.Resize(
            workForeground,
            hardAlpha,
            new OpenCvSharp.Size(sourceBitmap.Width, sourceBitmap.Height),
            interpolation: InterpolationFlags.Linear);
        return RefineBoundary(hardAlpha, lassoAlpha);
    }

    private static Mat RefineBoundary(Mat hardAlpha, Mat lassoAlpha)
    {
        using var binary = new Mat();
        using var eroded = new Mat();
        using var dilated = new Mat();
        using var smoothed = new Mat();
        using Mat kernel = Cv2.GetStructuringElement(
            MorphShapes.Ellipse,
            new OpenCvSharp.Size(3, 3));
        Cv2.Threshold(hardAlpha, binary, 127, 255, ThresholdTypes.Binary);
        Cv2.Erode(binary, eroded, kernel);
        Cv2.Dilate(binary, dilated, kernel);
        Cv2.GaussianBlur(hardAlpha, smoothed, new OpenCvSharp.Size(3, 3), 0.65);

        var alpha = new Mat(hardAlpha.Rows, hardAlpha.Cols, MatType.CV_8UC1, Scalar.All(0));
        alpha.SetTo(Scalar.All(255), eroded);
        using var boundary = new Mat();
        Cv2.Subtract(dilated, eroded, boundary);
        smoothed.CopyTo(alpha, boundary);
        Cv2.Min(alpha, lassoAlpha, alpha);
        return alpha;
    }

    private static void ConvertToBgr(Mat source, Mat destination)
    {
        switch (source.Channels())
        {
            case 4:
                Cv2.CvtColor(source, destination, ColorConversionCodes.BGRA2BGR);
                break;
            case 3:
                source.CopyTo(destination);
                break;
            case 1:
                Cv2.CvtColor(source, destination, ColorConversionCodes.GRAY2BGR);
                break;
            default:
                throw new OpenCVException("지원하지 않는 이미지 채널 형식입니다.");
        }
    }

    private static PointF[] NormalizePolygon(
        IReadOnlyList<PointF> polygon,
        int width,
        int height)
    {
        if (polygon.Count < 3)
            throw new ArgumentException("스마트 컷 경로에는 점이 3개 이상 필요합니다.", nameof(polygon));

        var normalized = new List<PointF>(polygon.Count);
        foreach (PointF point in polygon)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                throw new ArgumentException("스마트 컷 경로 좌표는 유한해야 합니다.", nameof(polygon));

            var clipped = new PointF(
                Math.Clamp(point.X, 0, width - 1),
                Math.Clamp(point.Y, 0, height - 1));
            if (normalized.Count == 0 || normalized[^1] != clipped)
                normalized.Add(clipped);
        }

        if (normalized.Count > 1 && normalized[0] == normalized[^1])
            normalized.RemoveAt(normalized.Count - 1);
        if (normalized.Count < 3 || PolygonArea(normalized) < 1.0)
            throw new ArgumentException("스마트 컷 경로의 면적이 너무 작습니다.", nameof(polygon));
        return normalized.ToArray();
    }

    private static double PolygonArea(IReadOnlyList<PointF> polygon)
    {
        double twiceArea = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            PointF current = polygon[i];
            PointF next = polygon[(i + 1) % polygon.Count];
            twiceArea += current.X * next.Y - next.X * current.Y;
        }
        return Math.Abs(twiceArea) * 0.5;
    }

    private static Mat CreatePolygonMask(
        int width,
        int height,
        IReadOnlyList<PointF> polygon,
        bool antiAlias)
    {
        var mask = new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));
        try
        {
            CvPoint[] points = polygon
                .Select(point => new CvPoint(
                    Math.Clamp((int)Math.Round(point.X), 0, width - 1),
                    Math.Clamp((int)Math.Round(point.Y), 0, height - 1)))
                .ToArray();
            Cv2.FillPoly(
                mask,
                [points],
                Scalar.All(255),
                antiAlias ? LineTypes.AntiAlias : LineTypes.Link8);
            return mask;
        }
        catch
        {
            mask.Dispose();
            throw;
        }
    }

    private static (int Width, int Height, double Scale) CalculateWorkingSize(
        int width,
        int height)
    {
        double pixelScale = Math.Sqrt(MaxWorkingPixels / ((double)width * height));
        double edgeScale = MaxWorkingLongEdge / (double)Math.Max(width, height);
        double scale = Math.Min(1.0, Math.Min(pixelScale, edgeScale));
        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)),
            scale);
    }

    private static Bitmap ApplyAlpha(
        Bitmap source,
        Mat alpha,
        CancellationToken cancellationToken)
    {
        int width = source.Width;
        int height = source.Height;
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        if (source.HorizontalResolution > 0 && source.VerticalResolution > 0)
            result.SetResolution(source.HorizontalResolution, source.VerticalResolution);

        try
        {
            BitmapData sourceData = source.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                BitmapData resultData = result.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);
                try
                {
                    unsafe
                    {
                        for (int y = 0; y < height; y++)
                        {
                            if ((y & 31) == 0)
                                cancellationToken.ThrowIfCancellationRequested();
                            byte* sourceRow = (byte*)sourceData.Scan0 + y * sourceData.Stride;
                            byte* resultRow = (byte*)resultData.Scan0 + y * resultData.Stride;
                            byte* alphaRow = (byte*)alpha.Ptr(y);
                            for (int x = 0; x < width; x++)
                            {
                                int offset = x * 4;
                                resultRow[offset] = sourceRow[offset];
                                resultRow[offset + 1] = sourceRow[offset + 1];
                                resultRow[offset + 2] = sourceRow[offset + 2];
                                resultRow[offset + 3] = (byte)(
                                    (sourceRow[offset + 3] * alphaRow[x] + 127) / 255);
                            }
                        }
                    }
                }
                finally
                {
                    result.UnlockBits(resultData);
                }
            }
            finally
            {
                source.UnlockBits(sourceData);
            }

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }
}
