using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using ShinCapture.Helpers;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaPen = System.Windows.Media.Pen;
using PixFmt = System.Drawing.Imaging.PixelFormat;

namespace ShinCapture.Capture;

/// <summary>
/// 스크롤 캡쳐: 클릭 지점의 스크롤 컨테이너를 자동 감지 → 자동 스크롤+캡쳐+스티칭.
/// Phase 1 (오버레이): 클릭 지점 선택 (시작선 + 화살표 표시)
/// Phase 2 (오버레이 닫힌 후): UI Automation으로 컨테이너 감지 → 마우스 휠 → 스티칭
/// </summary>
public class ScrollCaptureMode : ICaptureMode
{
    private Bitmap? _screenBitmap;
    private FrameworkElement? _overlay;
    private double _scaleX = 1.0, _scaleY = 1.0;

    private System.Windows.Point _clickPos; // 논리 좌표 (오버레이 기준)
    private int _clickPhysX, _clickPhysY;   // 물리 픽셀 좌표
    private Bitmap? _stitchedBitmap;

    public bool IsComplete { get; private set; }
    public bool IsCancelled { get; private set; }

    public void Initialize(Bitmap screenBitmap, FrameworkElement overlay)
    {
        _screenBitmap = screenBitmap;
        _overlay = overlay;
        if (overlay.ActualWidth > 0 && screenBitmap.Width > 0)
            _scaleX = screenBitmap.Width / overlay.ActualWidth;
        if (overlay.ActualHeight > 0 && screenBitmap.Height > 0)
            _scaleY = screenBitmap.Height / overlay.ActualHeight;
    }

    public void OnMouseDown(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right) { IsCancelled = true; return; }
        if (e.ChangedButton == MouseButton.Left)
        {
            _clickPos = e.GetPosition(_overlay);

            // 물리 좌표 계산
            int vsL = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
            int vsT = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
            _clickPhysX = vsL + (int)(_clickPos.X * _scaleX);
            _clickPhysY = vsT + (int)(_clickPos.Y * _scaleY);

            IsComplete = true;
        }
    }

    public void OnMouseMove(MouseEventArgs e)
    {
        _clickPos = e.GetPosition(_overlay);
    }

    public void OnMouseUp(MouseButtonEventArgs e) { }

    public void Render(DrawingContext dc, double overlayW, double overlayH)
    {
        // 전체 어둡게
        var dim = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x55, 0, 0, 0));
        dc.DrawRectangle(dim, null, new Rect(0, 0, overlayW, overlayH));

        double cx = _clickPos.X, cy = _clickPos.Y;

        // 시작선 (가로, 주황 점선)
        var startPen = new MediaPen(
            new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 255, 120, 0)), 2)
        { DashStyle = DashStyles.Dash };
        dc.DrawLine(startPen, new System.Windows.Point(0, cy), new System.Windows.Point(overlayW, cy));

        // 아래 방향 화살표
        var arrowPen = new MediaPen(MediaBrushes.White, 2.5);
        double arrowTop = cy + 20, arrowBot = cy + 60;
        dc.DrawLine(arrowPen, new System.Windows.Point(cx, arrowTop), new System.Windows.Point(cx, arrowBot));
        dc.DrawLine(arrowPen, new System.Windows.Point(cx - 10, arrowBot - 12), new System.Windows.Point(cx, arrowBot));
        dc.DrawLine(arrowPen, new System.Windows.Point(cx + 10, arrowBot - 12), new System.Windows.Point(cx, arrowBot));

        // 십자선 (커서 위치)
        var crossPen = new MediaPen(
            new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 255, 255, 255)), 1);
        dc.DrawLine(crossPen, new System.Windows.Point(cx - 20, cy), new System.Windows.Point(cx + 20, cy));
        dc.DrawLine(crossPen, new System.Windows.Point(cx, cy - 20), new System.Windows.Point(cx, cy + 20));

        // 안내 텍스트
        var hint = new FormattedText(
            "클릭: 이 지점부터 아래로 스크롤 캡쳐 (자동 영역 감지)",
            System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 13, MediaBrushes.White, 96);
        double tx = cx - hint.Width / 2, ty = cy - 40;
        if (ty < 10) ty = cy + 70;
        tx = Math.Clamp(tx, 10, overlayW - hint.Width - 10);
        var bgRect = new Rect(tx - 8, ty - 3, hint.Width + 16, hint.Height + 6);
        dc.DrawRoundedRectangle(
            new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)),
            null, bgRect, 4, 4);
        dc.DrawText(hint, new System.Windows.Point(tx, ty));
    }

    public Rectangle? GetSelectedRegion()
    {
        if (_stitchedBitmap != null)
            return new Rectangle(0, 0, _stitchedBitmap.Width, _stitchedBitmap.Height);
        return null;
    }

    public Bitmap? GetStitchedBitmap() => _stitchedBitmap;
    public (int x, int y) GetClickPhysical() => (_clickPhysX, _clickPhysY);

    /// <summary>오버레이 닫힌 후 호출: 컨테이너 감지 → 스크롤 캡쳐</summary>
    public void PerformScrollCapture()
    {
        Thread.Sleep(400); // 오버레이 소멸 대기

        // UI Automation으로 스크롤 컨테이너 감지
        var region = DetectScrollContainer(_clickPhysX, _clickPhysY);
        if (region.Width < 20 || region.Height < 20) return;

        // 비트맵 좌표로 변환 (가상 스크린 오프셋 제거)
        int vsL = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int vsT = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        var bmpRegion = new Rectangle(
            region.X - vsL, region.Y - vsT, region.Width, region.Height);

        if (_screenBitmap != null)
        {
            bmpRegion.X = Math.Clamp(bmpRegion.X, 0, _screenBitmap.Width - 1);
            bmpRegion.Y = Math.Clamp(bmpRegion.Y, 0, _screenBitmap.Height - 1);
            bmpRegion.Width = Math.Min(bmpRegion.Width, _screenBitmap.Width - bmpRegion.X);
            bmpRegion.Height = Math.Min(bmpRegion.Height, _screenBitmap.Height - bmpRegion.Y);
        }

        // 커서를 영역 중앙으로
        NativeMethods.SetCursorPos(region.X + region.Width / 2, region.Y + region.Height / 2);
        Thread.Sleep(200);

        // 스크롤 + 캡쳐
        CaptureFrames(bmpRegion);
    }

    /// <summary>UI Automation으로 클릭 지점의 스크롤 가능 컨테이너 감지</summary>
    private static Rectangle DetectScrollContainer(int physX, int physY)
    {
        try
        {
            var point = new System.Windows.Point(physX, physY);
            var element = AutomationElement.FromPoint(point);

            // 현재 요소부터 위로 올라가며 ScrollPattern 찾기
            while (element != null)
            {
                try
                {
                    if (element.TryGetCurrentPattern(ScrollPattern.Pattern, out _))
                    {
                        var r = element.Current.BoundingRectangle;
                        if (!r.IsEmpty && r.Width > 50 && r.Height > 50)
                            return new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                    }
                }
                catch { }

                element = TreeWalker.RawViewWalker.GetParent(element);
            }
        }
        catch { }

        // UI Automation 실패 → 클릭 지점의 윈도우 전체 사용
        var hwnd = NativeMethods.WindowFromPoint(new NativeMethods.POINT(physX, physY));
        if (hwnd != IntPtr.Zero)
        {
            var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            if (root != IntPtr.Zero && NativeMethods.GetWindowRect(root, out var wr))
                return new Rectangle(wr.Left, wr.Top, wr.Width, wr.Height);
        }

        // 최후 폴백: 클릭 지점 기준 기본 영역
        return new Rectangle(physX - 400, physY, 800, 600);
    }

    private void CaptureFrames(Rectangle bmpRegion)
    {
        const int maxScrolls = 50;
        const int scrollDelay = 350;
        const int wheelClicks = -3;

        var frames = new List<Bitmap>();
        frames.Add(CaptureRegion(bmpRegion));

        for (int i = 0; i < maxScrolls; i++)
        {
            NativeMethods.mouse_event(NativeMethods.MOUSEEVENTF_WHEEL,
                0, 0, wheelClicks * NativeMethods.WHEEL_DELTA, IntPtr.Zero);
            Thread.Sleep(scrollDelay);

            var frame = CaptureRegion(bmpRegion);
            if (FramesMatch(frames[frames.Count - 1], frame, 0.96))
            {
                frame.Dispose();
                break;
            }
            frames.Add(frame);
        }

        if (frames.Count <= 1)
        {
            _stitchedBitmap = frames.Count > 0 ? (Bitmap)frames[0].Clone() : null;
        }
        else
        {
            int headerH = DetectFixed(frames, true);
            int footerH = DetectFixed(frames, false);
            _stitchedBitmap = Stitch(frames, headerH, footerH);
        }

        foreach (var f in frames) f.Dispose();
    }

    private static Bitmap CaptureRegion(Rectangle r)
    {
        var full = ScreenHelper.CaptureFullScreen();
        var cropped = ScreenHelper.CropBitmap(full, r);
        full.Dispose();
        return cropped;
    }

    // ── 고정 영역 감지 ──
    private static int DetectFixed(List<Bitmap> frames, bool top)
    {
        if (frames.Count < 2) return 0;
        int w = frames[0].Width, h = frames[0].Height;
        int maxH = h / 3, step = Math.Max(1, w / 40);
        int result = 0;

        for (int off = 0; off < maxH; off++)
        {
            bool same = true;
            for (int fi = 1; fi < frames.Count && same; fi++)
            {
                int y0 = top ? off : h - 1 - off;
                int yf = top ? off : frames[fi].Height - 1 - off;
                if (y0 < 0 || yf < 0 || y0 >= h || yf >= frames[fi].Height) { same = false; break; }
                for (int x = 0; x < w && same; x += step)
                {
                    var c0 = frames[0].GetPixel(x, y0);
                    var cf = frames[fi].GetPixel(x, yf);
                    if (Math.Abs(c0.R - cf.R) > 4 || Math.Abs(c0.G - cf.G) > 4 || Math.Abs(c0.B - cf.B) > 4)
                        same = false;
                }
            }
            if (same) result = off + 1; else break;
        }
        return result;
    }

    // ── 스티칭 ──
    private static Bitmap Stitch(List<Bitmap> frames, int hdrH, int ftrH)
    {
        var strips = new List<Bitmap>();
        strips.Add(ftrH > 0 ? Crop(frames[0], 0, frames[0].Height - ftrH) : frames[0]);

        for (int i = 1; i < frames.Count; i++)
        {
            int overlap = FindOverlap(frames[i - 1], frames[i], hdrH, ftrH);
            int cropTop = Math.Max(hdrH, overlap);
            int newH = frames[i].Height - ftrH - cropTop;
            if (newH <= 0) continue;
            strips.Add(Crop(frames[i], cropTop, newH));
        }

        if (ftrH > 0 && frames.Count > 0)
        {
            var last = frames[frames.Count - 1];
            strips.Add(Crop(last, last.Height - ftrH, ftrH));
        }

        int totalW = strips[0].Width, totalH = 0;
        foreach (var s in strips) totalH += s.Height;
        var result = new Bitmap(totalW, totalH, PixFmt.Format32bppArgb);
        using var g = Graphics.FromImage(result);
        int y = 0;
        foreach (var s in strips) { g.DrawImage(s, 0, y); y += s.Height; }
        return result;
    }

    // ── 겹침 감지: prev 최하단 블록 SSD ──
    private static int FindOverlap(Bitmap prev, Bitmap next, int hdrH, int ftrH)
    {
        int w = Math.Min(prev.Width, next.Width);
        int step = Math.Max(1, w / 50);
        int pBot = prev.Height - ftrH, nTop = hdrH, nBot = next.Height - ftrH;
        int blockH = Math.Clamp(12, 4, (pBot - hdrH) / 3);
        int refY = pBot - blockH;
        if (refY < hdrH) return hdrH;

        long bestSsd = long.MaxValue;
        int bestJ = -1;

        for (int j = nTop; j <= nBot - blockH; j++)
        {
            long ssd = 0;
            bool stop = false;
            for (int row = 0; row < blockH && !stop; row++)
                for (int x = 0; x < w; x += step)
                {
                    var pc = prev.GetPixel(x, refY + row);
                    var nc = next.GetPixel(x, j + row);
                    int dr = pc.R - nc.R, dg = pc.G - nc.G, db = pc.B - nc.B;
                    ssd += dr * dr + dg * dg + db * db;
                    if (ssd > bestSsd) { stop = true; break; }
                }
            if (!stop && ssd < bestSsd) { bestSsd = ssd; bestJ = j; }
        }

        int samples = blockH * Math.Max(1, w / step);
        if (bestJ >= 0 && samples > 0 && bestSsd / samples < 30)
            return hdrH + (bestJ - nTop) + blockH;

        return hdrH;
    }

    // ── 유틸 ──
    private static bool FramesMatch(Bitmap a, Bitmap b, double thr)
    {
        if (a.Width != b.Width || a.Height != b.Height) return false;
        int step = Math.Max(1, Math.Max(a.Width, a.Height) / 40);
        int match = 0, total = 0;
        for (int y = 0; y < a.Height; y += step)
            for (int x = 0; x < a.Width; x += step)
            {
                total++;
                var ac = a.GetPixel(x, y); var bc = b.GetPixel(x, y);
                if (Math.Abs(ac.R - bc.R) <= 4 && Math.Abs(ac.G - bc.G) <= 4 && Math.Abs(ac.B - bc.B) <= 4)
                    match++;
            }
        return total > 0 && (double)match / total > thr;
    }

    private static Bitmap Crop(Bitmap src, int top, int height)
    {
        height = Math.Clamp(height, 1, src.Height - top);
        var r = new Bitmap(src.Width, height, PixFmt.Format32bppArgb);
        using var g = Graphics.FromImage(r);
        g.DrawImage(src, new Rectangle(0, 0, src.Width, height),
            new Rectangle(0, top, src.Width, height), GraphicsUnit.Pixel);
        return r;
    }
}
