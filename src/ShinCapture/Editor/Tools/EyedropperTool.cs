using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShinCapture.Editor.Tools;

public class EyedropperTool : ToolBase
{
    private BitmapSource? _sourceImage;
    private byte[]? _pixels;
    private int _stride;

    private Point _currentPos;
    private Color _hoveredColor;
    private bool _hasHover;

    public override string Name => "Eyedropper";
    public override string Icon => "⌖";

    public event Action<Color>? ColorPicked;

    public override Cursor? RequestedCursor => Cursors.Cross;

    public EyedropperTool(BitmapSource? sourceImage = null)
    {
        _sourceImage = sourceImage;
        CachePixels();
    }

    public void SetSourceImage(BitmapSource? source)
    {
        _sourceImage = source;
        CachePixels();
    }

    private void CachePixels()
    {
        if (_sourceImage == null) return;
        _stride = _sourceImage.PixelWidth * 4;
        _pixels = new byte[_sourceImage.PixelHeight * _stride];
        _sourceImage.CopyPixels(_pixels, _stride, 0);
    }

    private Color GetPixelAt(int px, int py)
    {
        if (_pixels == null || _sourceImage == null) return Colors.Transparent;
        px = Math.Clamp(px, 0, _sourceImage.PixelWidth - 1);
        py = Math.Clamp(py, 0, _sourceImage.PixelHeight - 1);
        int idx = py * _stride + px * 4;
        return Color.FromArgb(_pixels[idx + 3], _pixels[idx + 2], _pixels[idx + 1], _pixels[idx]);
    }

    public override void OnMouseDown(Point position, MouseButtonEventArgs e)
    {
        if (_sourceImage == null) return;
        int px = (int)position.X, py = (int)position.Y;
        var color = GetPixelAt(px, py);
        CurrentColor = color;
        ColorPicked?.Invoke(color);
    }

    public override void OnMouseMove(Point position, MouseEventArgs e)
    {
        _currentPos = position;
        if (_sourceImage != null)
        {
            _hoveredColor = GetPixelAt((int)position.X, (int)position.Y);
            _hasHover = true;
        }
    }

    public override void OnMouseUp(Point position, MouseButtonEventArgs e) { }
    public override IEditorCommand? GetCommand() => null;

    public override void RenderPreview(DrawingContext dc)
    {
        if (!_hasHover || _sourceImage == null) return;

        int px = (int)Math.Clamp(_currentPos.X, 0, _sourceImage.PixelWidth - 1);
        int py = (int)Math.Clamp(_currentPos.Y, 0, _sourceImage.PixelHeight - 1);

        // ── 확대경 (8x 줌, 120x120 영역) ──
        double magSize = 120;
        double zoomFactor = 8;
        double srcSize = magSize / zoomFactor; // 15px 영역
        double halfSrc = srcSize / 2;

        // 확대경 위치 (커서 우하단, 화면 넘어가면 반대쪽)
        double magX = _currentPos.X + 24;
        double magY = _currentPos.Y + 24;
        if (magX + magSize + 60 > _sourceImage.PixelWidth)
            magX = _currentPos.X - magSize - 60;
        if (magY + magSize + 50 > _sourceImage.PixelHeight)
            magY = _currentPos.Y - magSize - 50;

        // 배경 (검은색 둥근 카드)
        double cardW = magSize + 16, cardH = magSize + 48;
        var cardRect = new Rect(magX - 8, magY - 8, cardW, cardH);
        var cardBrush = new SolidColorBrush(Color.FromArgb(230, 30, 30, 30));
        dc.DrawRoundedRectangle(cardBrush, null, cardRect, 8, 8);

        // 확대 이미지 (클리핑)
        dc.PushClip(new RectangleGeometry(new Rect(magX, magY, magSize, magSize), 4, 4));

        // 소스 영역을 확대하여 그리기
        double srcX = px - halfSrc, srcY = py - halfSrc;
        var srcRect = new Rect(srcX, srcY, srcSize, srcSize);
        var dstRect = new Rect(magX, magY, magSize, magSize);

        // CroppedBitmap으로 확대
        int cropX = Math.Clamp((int)srcX, 0, _sourceImage.PixelWidth - 1);
        int cropY = Math.Clamp((int)srcY, 0, _sourceImage.PixelHeight - 1);
        int cropW = Math.Min((int)srcSize + 1, _sourceImage.PixelWidth - cropX);
        int cropH = Math.Min((int)srcSize + 1, _sourceImage.PixelHeight - cropY);
        if (cropW > 0 && cropH > 0)
        {
            try
            {
                var cropped = new CroppedBitmap(_sourceImage, new Int32Rect(cropX, cropY, cropW, cropH));
                dc.DrawImage(cropped, dstRect);
            }
            catch { }
        }

        dc.Pop(); // clip

        // 중앙 십자선 (현재 픽셀 표시)
        double crossX = magX + magSize / 2, crossY = magY + magSize / 2;
        var crossPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1);
        crossPen.Freeze();
        double cellSize = zoomFactor;
        dc.DrawRectangle(null, crossPen, new Rect(crossX - cellSize / 2, crossY - cellSize / 2, cellSize, cellSize));

        // ── 색상 정보 패널 (확대경 아래) ──
        double infoY = magY + magSize + 4;
        string hex = $"#{_hoveredColor.R:X2}{_hoveredColor.G:X2}{_hoveredColor.B:X2}";
        string rgb = $"RGB({_hoveredColor.R}, {_hoveredColor.G}, {_hoveredColor.B})";
        string pos = $"{px}, {py}";

        // 색상 미리보기 원
        double circleR = 8;
        var colorBrush = new SolidColorBrush(_hoveredColor);
        colorBrush.Freeze();
        var circlePen = new Pen(Brushes.White, 1.5);
        circlePen.Freeze();
        dc.DrawEllipse(colorBrush, circlePen, new Point(magX + circleR + 2, infoY + 10), circleR, circleR);

        // 텍스트
        var hexText = new FormattedText(hex, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            13, Brushes.White, 96);
        dc.DrawText(hexText, new Point(magX + circleR * 2 + 8, infoY + 1));

        var rgbText = new FormattedText(rgb, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            10, new SolidColorBrush(Color.FromRgb(160, 160, 160)), 96);
        dc.DrawText(rgbText, new Point(magX + circleR * 2 + 8, infoY + 18));

        var posText = new FormattedText(pos, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            10, new SolidColorBrush(Color.FromRgb(120, 120, 120)), 96);
        dc.DrawText(posText, new Point(magX + circleR * 2 + 8, infoY + 31));
    }

    public override void Reset()
    {
        _hasHover = false;
    }
}
