using System;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MediaPen = System.Windows.Media.Pen;
using MediaBrushes = System.Windows.Media.Brushes;

namespace ShinCapture.Capture;

public class FixedSizeCaptureMode : ICaptureMode
{
    private int _width;
    private int _height;

    private Bitmap? _screenBitmap;
    private FrameworkElement? _overlay;
    private double _scaleX = 1.0, _scaleY = 1.0;

    // 프레임 중심 좌표 (논리 px)
    private double _cx, _cy;
    private bool _placed; // 첫 클릭으로 위치 고정됨

    private enum Interaction { None, ResizeL, ResizeR, ResizeT, ResizeB, ResizeTL, ResizeTR, ResizeBL, ResizeBR, Move }
    private Interaction _drag = Interaction.None;
    private Interaction _hover = Interaction.None; // 커서용
    private System.Windows.Point _dragStart;
    private double _dragCxStart, _dragCyStart;
    private bool _didDrag;

    private const double EdgeHit = 14;

    // 위치 기억 (다음 캡쳐 시 재사용)
    private static double _savedCx, _savedCy;
    private static int _savedW, _savedH;
    private static bool _hasSaved;

    public bool IsComplete { get; private set; }
    public bool IsCancelled { get; private set; }

    public Cursor? RequestedCursor => _drag switch
    {
        Interaction.Move => Cursors.SizeAll,
        Interaction.ResizeTL or Interaction.ResizeBR => Cursors.SizeNWSE,
        Interaction.ResizeTR or Interaction.ResizeBL => Cursors.SizeNESW,
        Interaction.ResizeL or Interaction.ResizeR => Cursors.SizeWE,
        Interaction.ResizeT or Interaction.ResizeB => Cursors.SizeNS,
        _ => _hover switch
        {
            Interaction.Move => Cursors.Hand,
            Interaction.ResizeTL or Interaction.ResizeBR => Cursors.SizeNWSE,
            Interaction.ResizeTR or Interaction.ResizeBL => Cursors.SizeNESW,
            Interaction.ResizeL or Interaction.ResizeR => Cursors.SizeWE,
            Interaction.ResizeT or Interaction.ResizeB => Cursors.SizeNS,
            _ => _placed ? Cursors.Arrow : Cursors.Cross
        }
    };

    public FixedSizeCaptureMode(int width, int height)
    {
        _width = width;
        _height = height;

        // 이전 캡쳐 위치 복원
        if (_hasSaved)
        {
            _cx = _savedCx;
            _cy = _savedCy;
            _width = _savedW;
            _height = _savedH;
            _placed = true;
        }
    }

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _screenBitmap = screenBitmap;
        _overlay = overlay;
        if (overlay.ActualWidth > 0 && screenBitmap.Width > 0)
            _scaleX = screenBitmap.Width / overlay.ActualWidth;
        if (overlay.ActualHeight > 0 && screenBitmap.Height > 0)
            _scaleY = screenBitmap.Height / overlay.ActualHeight;

        if (overlay is UIElement el)
            el.PreviewMouseWheel += OnWheel;

        // 이전 위치가 있으면 바로 사용
        if (_hasSaved)
            _placed = true;
    }

    private Rect GetFrame()
    {
        double fw = _width / _scaleX, fh = _height / _scaleY;
        return new Rect(_cx - fw / 2, _cy - fh / 2, fw, fh);
    }

    private Interaction HitTestAt(System.Windows.Point p)
    {
        var r = GetFrame();
        bool nL = Math.Abs(p.X - r.Left) < EdgeHit && p.Y >= r.Top - EdgeHit && p.Y <= r.Bottom + EdgeHit;
        bool nR = Math.Abs(p.X - r.Right) < EdgeHit && p.Y >= r.Top - EdgeHit && p.Y <= r.Bottom + EdgeHit;
        bool nT = Math.Abs(p.Y - r.Top) < EdgeHit && p.X >= r.Left - EdgeHit && p.X <= r.Right + EdgeHit;
        bool nB = Math.Abs(p.Y - r.Bottom) < EdgeHit && p.X >= r.Left - EdgeHit && p.X <= r.Right + EdgeHit;

        if (nT && nL) return Interaction.ResizeTL;
        if (nT && nR) return Interaction.ResizeTR;
        if (nB && nL) return Interaction.ResizeBL;
        if (nB && nR) return Interaction.ResizeBR;
        if (nL) return Interaction.ResizeL;
        if (nR) return Interaction.ResizeR;
        if (nT) return Interaction.ResizeT;
        if (nB) return Interaction.ResizeB;
        if (r.Contains(p)) return Interaction.Move;
        return Interaction.None;
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right) { IsCancelled = true; return; }
        if (e.ChangedButton != MouseButton.Left) return;

        var pos = e.GetPosition(_overlay);

        if (!_placed)
        {
            // 첫 클릭 → 위치 고정
            _cx = pos.X;
            _cy = pos.Y;
            _placed = true;
            SavePosition();
            return;
        }

        var hit = HitTestAt(pos);
        if (hit != Interaction.None)
        {
            _drag = hit;
            _dragStart = pos;
            _dragCxStart = _cx;
            _dragCyStart = _cy;
            _didDrag = false;
        }
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        var pos = e.GetPosition(_overlay);

        if (_drag != Interaction.None)
        {
            var delta = pos - _dragStart;
            if (Math.Abs(delta.X) > 3 || Math.Abs(delta.Y) > 3)
                _didDrag = true;

            var r = GetFrame();
            switch (_drag)
            {
                case Interaction.Move:
                    _cx = _dragCxStart + delta.X;
                    _cy = _dragCyStart + delta.Y;
                    break;
                case Interaction.ResizeR: case Interaction.ResizeTR: case Interaction.ResizeBR:
                    _width = Math.Max(50, (int)((pos.X - r.Left) * _scaleX)); break;
                case Interaction.ResizeL: case Interaction.ResizeTL: case Interaction.ResizeBL:
                    _width = Math.Max(50, (int)((r.Right - pos.X) * _scaleX)); break;
            }
            switch (_drag)
            {
                case Interaction.ResizeB: case Interaction.ResizeBL: case Interaction.ResizeBR:
                    _height = Math.Max(50, (int)((pos.Y - r.Top) * _scaleY)); break;
                case Interaction.ResizeT: case Interaction.ResizeTL: case Interaction.ResizeTR:
                    _height = Math.Max(50, (int)((r.Bottom - pos.Y) * _scaleY)); break;
            }
            return;
        }

        // 위치 고정 전: 프레임이 커서를 따라감
        if (!_placed)
        {
            _cx = pos.X;
            _cy = pos.Y;
        }

        // 호버 상태 업데이트 (커서 변경용)
        _hover = _placed ? HitTestAt(pos) : Interaction.None;
    }

    public void OnMouseUp(MouseButtonEventArgs e)
    {
        if (_drag == Interaction.Move && !_didDrag)
        {
            // 이동 없이 클릭 → 캡쳐 확정
            SavePosition();
            IsComplete = true;
        }
        else if (_drag != Interaction.None)
        {
            SavePosition();
        }
        _drag = Interaction.None;
    }

    public void OnKeyDown(KeyEventArgs e)
    {
        if (_placed && (e.Key == Key.Enter || e.Key == Key.Return))
        {
            SavePosition();
            IsComplete = true;
            e.Handled = true;
        }
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        double f = e.Delta > 0 ? 1.1 : 0.9;
        _width = Math.Clamp((int)(_width * f), 50, 3840);
        _height = Math.Clamp((int)(_height * f), 50, 2160);
        e.Handled = true;
    }

    private void SavePosition()
    {
        _savedCx = _cx; _savedCy = _cy;
        _savedW = _width; _savedH = _height;
        _hasSaved = true;
    }

    public void Render(DrawingContext dc, double ow, double oh)
    {
        double cx = _placed ? _cx : _cx;
        if (_cx == 0 && _cy == 0 && !_placed) return;
        var fr = GetFrame();

        // 외부 어둡게
        var dim = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x88, 0, 0, 0));
        dc.DrawRectangle(dim, null, new Rect(0, 0, ow, Math.Max(0, fr.Top)));
        dc.DrawRectangle(dim, null, new Rect(0, fr.Bottom, ow, Math.Max(0, oh - fr.Bottom)));
        dc.DrawRectangle(dim, null, new Rect(0, fr.Top, Math.Max(0, fr.Left), fr.Height));
        dc.DrawRectangle(dim, null, new Rect(fr.Right, fr.Top, Math.Max(0, ow - fr.Right), fr.Height));

        // 테두리
        var borderPen = new MediaPen(MediaBrushes.White, 1.5) { DashStyle = DashStyles.Dash };
        dc.DrawRectangle(null, borderPen, fr);

        // 8개 핸들 (위치 고정 후에만)
        if (_placed)
        {
            var hFill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 255, 255, 255));
            var hPen = new MediaPen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)), 1.2);
            double hs = 5;
            DrawH(dc, hFill, hPen, fr.Left, fr.Top, hs);
            DrawH(dc, hFill, hPen, fr.Right, fr.Top, hs);
            DrawH(dc, hFill, hPen, fr.Left, fr.Bottom, hs);
            DrawH(dc, hFill, hPen, fr.Right, fr.Bottom, hs);
            DrawH(dc, hFill, hPen, fr.Left + fr.Width / 2, fr.Top, hs);
            DrawH(dc, hFill, hPen, fr.Left + fr.Width / 2, fr.Bottom, hs);
            DrawH(dc, hFill, hPen, fr.Left, fr.Top + fr.Height / 2, hs);
            DrawH(dc, hFill, hPen, fr.Right, fr.Top + fr.Height / 2, hs);

            // 이동 아이콘 (중앙 십자)
            var movePen = new MediaPen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 255, 255)), 1.5);
            double mx = fr.Left + fr.Width / 2, my = fr.Top + fr.Height / 2;
            dc.DrawLine(movePen, new System.Windows.Point(mx - 12, my), new System.Windows.Point(mx + 12, my));
            dc.DrawLine(movePen, new System.Windows.Point(mx, my - 12), new System.Windows.Point(mx, my + 12));
        }

        // 크기 라벨
        var label = new FormattedText($"{_width} x {_height}",
            System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 13, MediaBrushes.White, 96);
        double lx = fr.Left + (fr.Width - label.Width) / 2;
        double ly = fr.Top - label.Height - 10;
        if (ly < 4) ly = fr.Bottom + 6;
        dc.DrawRoundedRectangle(
            new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 0, 0, 0)),
            null, new Rect(lx - 6, ly - 2, label.Width + 12, label.Height + 4), 4, 4);
        dc.DrawText(label, new System.Windows.Point(lx, ly));

        // 안내 텍스트
        string hint;
        if (!_placed)
            hint = "클릭: 위치 지정 | 휠: 크기 조절";
        else if (_drag == Interaction.Move)
            hint = "드래그하여 위치 이동 중";
        else if (_drag != Interaction.None)
            hint = "드래그하여 크기 조절 중";
        else
            hint = "핸들 드래그: 크기 조절 | 내부 드래그: 이동 | 클릭 또는 Enter: 캡쳐 | 휠: 비율 조절";

        var hintFt = new FormattedText(hint,
            System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 11,
            new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)), 96);
        double hx = fr.Left + (fr.Width - hintFt.Width) / 2;
        double hy = ly + label.Height + 8;
        if (hy > oh - 20) hy = fr.Top - label.Height - hintFt.Height - 16;
        hx = Math.Clamp(hx, 4, ow - hintFt.Width - 4);
        dc.DrawText(hintFt, new System.Windows.Point(hx, hy));
    }

    private static void DrawH(DrawingContext dc, System.Windows.Media.Brush fill, MediaPen pen, double x, double y, double s)
    {
        dc.DrawRectangle(fill, pen, new Rect(x - s, y - s, s * 2, s * 2));
    }

    public Rectangle? GetSelectedRegion()
    {
        if (!IsComplete) return null;
        int px = (int)(_cx * _scaleX) - _width / 2;
        int py = (int)(_cy * _scaleY) - _height / 2;
        px = Math.Max(0, px);
        py = Math.Max(0, py);
        int w = _screenBitmap != null ? Math.Min(_width, _screenBitmap.Width - px) : _width;
        int h = _screenBitmap != null ? Math.Min(_height, _screenBitmap.Height - py) : _height;
        return new Rectangle(px, py, w, h);
    }
}
