"""신캡쳐 아이콘 생성 — 캡쳐 프로그램 느낌의 모던 아이콘"""
from PIL import Image, ImageDraw, ImageFont
import math, struct, io, os

def draw_icon(size):
    """캡쳐 프로그램 스타일 아이콘: 선택 영역 + 크로스헤어"""
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = size  # alias
    p = max(1, s // 32)  # pixel unit

    # ── 배경: 둥근 사각형 (파란 그라데이션 느낌) ──
    margin = s * 0.06
    r = s * 0.2  # corner radius
    bg_rect = [margin, margin, s - margin, s - margin]

    # 그라데이션 배경 시뮬레이션 (상단 밝은 파랑 → 하단 진한 파랑)
    for y in range(int(margin), int(s - margin)):
        ratio = (y - margin) / (s - 2 * margin)
        r_c = int(0 + 0 * ratio)
        g_c = int(130 - 40 * ratio)
        b_c = int(220 - 30 * ratio)
        d.line([(margin, y), (s - margin, y)], fill=(r_c, g_c, b_c, 255))

    # 둥근 모서리 마스크
    mask = Image.new('L', (size, size), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle(bg_rect, radius=r, fill=255)

    # 마스크 적용
    bg = img.copy()
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    img.paste(bg, mask=mask)
    d = ImageDraw.Draw(img)

    # ── 캡쳐 선택 영역 (점선 사각형) ──
    sel_margin = s * 0.22
    sel_rect = [sel_margin, sel_margin, s - sel_margin, s - sel_margin]
    sel_w = s - 2 * sel_margin
    sel_h = s - 2 * sel_margin

    line_w = max(1, int(s * 0.035))
    white = (255, 255, 255, 240)
    white_dim = (255, 255, 255, 100)

    # 선택 사각형 — 4개 코너 브라켓 스타일
    corner_len = sel_w * 0.3
    cx1, cy1 = sel_margin, sel_margin
    cx2, cy2 = s - sel_margin, s - sel_margin

    # 좌상
    d.line([(cx1, cy1), (cx1 + corner_len, cy1)], fill=white, width=line_w)
    d.line([(cx1, cy1), (cx1, cy1 + corner_len)], fill=white, width=line_w)
    # 우상
    d.line([(cx2, cy1), (cx2 - corner_len, cy1)], fill=white, width=line_w)
    d.line([(cx2, cy1), (cx2, cy1 + corner_len)], fill=white, width=line_w)
    # 좌하
    d.line([(cx1, cy2), (cx1 + corner_len, cy2)], fill=white, width=line_w)
    d.line([(cx1, cy2), (cx1, cy2 - corner_len)], fill=white, width=line_w)
    # 우하
    d.line([(cx2, cy2), (cx2 - corner_len, cy2)], fill=white, width=line_w)
    d.line([(cx2, cy2), (cx2, cy2 - corner_len)], fill=white, width=line_w)

    # ── 중앙 크로스헤어 ──
    cx, cy = s / 2, s / 2
    cross_r = s * 0.10
    cross_w = max(1, int(s * 0.025))

    # 십자선
    d.line([(cx - cross_r, cy), (cx + cross_r, cy)], fill=white, width=cross_w)
    d.line([(cx, cy - cross_r), (cx, cy + cross_r)], fill=white, width=cross_w)

    # 중앙 원
    dot_r = s * 0.03
    d.ellipse(
        [cx - dot_r, cy - dot_r, cx + dot_r, cy + dot_r],
        fill=white
    )

    # ── 우하단 펜 아이콘 (편집 기능 표시) ──
    if size >= 48:
        pen_size = s * 0.28
        pen_x = s * 0.72
        pen_y = s * 0.72

        # 펜 배경 원 (주황)
        pen_bg_r = pen_size * 0.5
        d.ellipse(
            [pen_x - pen_bg_r, pen_y - pen_bg_r,
             pen_x + pen_bg_r, pen_y + pen_bg_r],
            fill=(255, 140, 0, 230)
        )

        # 펜 모양 (간단한 대각선 + 팁)
        pw = max(1, int(s * 0.02))
        pen_len = pen_size * 0.28
        # 대각선 (좌상→우하)
        d.line(
            [(pen_x - pen_len, pen_y - pen_len),
             (pen_x + pen_len * 0.5, pen_y + pen_len * 0.5)],
            fill=white, width=max(2, int(s * 0.03))
        )
        # 팁
        tip_r = s * 0.015
        d.ellipse(
            [pen_x + pen_len * 0.5 - tip_r, pen_y + pen_len * 0.5 - tip_r,
             pen_x + pen_len * 0.5 + tip_r, pen_y + pen_len * 0.5 + tip_r],
            fill=(255, 255, 200, 255)
        )

    return img


def img_to_bmp_data(img):
    """RGBA 이미지를 ICO용 BMP 데이터(DIB)로 변환 — AND 마스크 포함"""
    w, h = img.size
    pixels = img.load()

    # BMP InfoHeader (40 bytes) — height는 2배 (XOR + AND)
    header = struct.pack('<IiiHHIIiiII',
        40, w, h * 2, 1, 32, 0, w * h * 4, 0, 0, 0, 0)

    # XOR 픽셀 데이터 (하단→상단, BGRA)
    xor_data = b''
    for y in range(h - 1, -1, -1):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            xor_data += struct.pack('BBBB', b, g, r, a)

    # AND 마스크 (1bpp, 행당 4바이트 정렬)
    row_bytes = ((w + 31) // 32) * 4
    and_data = b'\x00' * (row_bytes * h)

    return header + xor_data + and_data


def create_ico(path, sizes=(16, 24, 32, 48, 64, 128, 256)):
    """멀티사이즈 ICO 파일 생성 — 작은 사이즈는 BMP, 256은 PNG"""
    entries = []  # (size, data_bytes)
    for sz in sizes:
        img = draw_icon(sz)
        if sz >= 256:
            buf = io.BytesIO()
            img.save(buf, format='PNG')
            entries.append((sz, buf.getvalue(), True))
        else:
            entries.append((sz, img_to_bmp_data(img), False))

    num = len(entries)
    header = struct.pack('<HHH', 0, 1, num)

    dir_entries = b''
    data_blocks = b''
    offset = 6 + num * 16

    for sz, data, is_png in entries:
        w = 0 if sz >= 256 else sz
        h = 0 if sz >= 256 else sz
        dir_entries += struct.pack('<BBBBHHII',
            w, h, 0, 0, 1, 32, len(data), offset)
        data_blocks += data
        offset += len(data)

    with open(path, 'wb') as f:
        f.write(header + dir_entries + data_blocks)

    total = os.path.getsize(path)
    print(f"Created: {path} ({total:,} bytes)")
    for sz in sizes:
        print(f"  {sz}x{sz}")


if __name__ == '__main__':
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(script_dir)

    ico_path = os.path.join(project_root, 'src', 'ShinCapture', 'Assets', 'icon.ico')
    create_ico(ico_path)

    # 프리뷰용 PNG도 생성
    preview = draw_icon(256)
    preview_path = os.path.join(project_root, 'src', 'ShinCapture', 'Assets', 'icon_preview.png')
    preview.save(preview_path)
    print(f"Preview: {preview_path}")
