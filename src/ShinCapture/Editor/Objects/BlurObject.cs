using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor.Objects;

public enum BlurStrength
{
    Light = 5,
    Medium = 15,
    Strong = 30
}

public class BlurObject : EditorObject
{
    public Rect Region { get; set; }
    public BlurStrength BlurStrength { get; set; } = BlurStrength.Medium;
    public BitmapSource? SourceImage { get; set; }

    public override Rect Bounds => Region;

    public override void Render(DrawingContext dc)
    {
        if (Region.Width <= 0 || Region.Height <= 0) return;

        if (SourceImage != null)
        {
            // 실제 픽셀 블러: 축소 후 확대하여 모자이크/블러 효과
            var blurred = CreatePixelBlur();
            if (blurred != null)
            {
                var clip = new RectangleGeometry(Region, 6, 6);
                clip.Freeze();
                dc.PushClip(clip);
                dc.DrawImage(blurred, Region);
                dc.Pop();
                return;
            }
        }

        // 폴백: 반투명 오버레이
        var frost = new SolidColorBrush(Color.FromArgb(180, 220, 220, 230));
        frost.Freeze();
        dc.DrawRectangle(frost, null, Region);
    }

    private BitmapSource? CreatePixelBlur()
    {
        try
        {
            int srcW = SourceImage!.PixelWidth;
            int srcH = SourceImage.PixelHeight;

            // 블러 영역을 소스 이미지 좌표로 클램핑
            int rx = Math.Clamp((int)Region.X, 0, srcW - 1);
            int ry = Math.Clamp((int)Region.Y, 0, srcH - 1);
            int rw = Math.Min((int)Region.Width, srcW - rx);
            int rh = Math.Min((int)Region.Height, srcH - ry);
            if (rw <= 0 || rh <= 0) return null;

            // 소스 영역 잘라내기
            var cropped = new CroppedBitmap(SourceImage, new Int32Rect(rx, ry, rw, rh));

            // 블러 강도에 따라 축소 비율 결정 (작을수록 더 흐릿)
            double scale = BlurStrength switch
            {
                BlurStrength.Light => 0.15,
                BlurStrength.Medium => 0.08,
                BlurStrength.Strong => 0.04,
                _ => 0.08
            };

            int smallW = Math.Max(1, (int)(rw * scale));
            int smallH = Math.Max(1, (int)(rh * scale));

            // 1단계: 축소 (픽셀 평균화)
            var smallImage = new TransformedBitmap(cropped,
                new ScaleTransform((double)smallW / rw, (double)smallH / rh));

            // 2단계: 다시 원래 크기로 확대 (BilinearInterpolation으로 부드러운 블러)
            var visual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.Fant);
            using (var vdc = visual.RenderOpen())
            {
                vdc.DrawImage(smallImage, new Rect(0, 0, rw, rh));
            }

            var rtb = new RenderTargetBitmap(rw, rh, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }
        catch
        {
            return null;
        }
    }

    public override bool HitTest(Point point)
    {
        return Region.Contains(point);
    }

    public override void Scale(double factor, Point anchor)
    {
        double x = anchor.X + (Region.X - anchor.X) * factor;
        double y = anchor.Y + (Region.Y - anchor.Y) * factor;
        Region = new Rect(x, y, Region.Width * factor, Region.Height * factor);
    }

    public override void Move(Vector delta)
    {
        Region = new Rect(Region.X + delta.X, Region.Y + delta.Y, Region.Width, Region.Height);
    }

    public override EditorObject Clone()
    {
        return new BlurObject
        {
            Region = Region,
            BlurStrength = BlurStrength,
            SourceImage = SourceImage,
            IsSelected = IsSelected,
            IsVisible = IsVisible
        };
    }
}
