"""인스톨러용 사이드바(164x314), 헤더(150x57) 이미지 생성 — 고급 디자인"""
from PIL import Image, ImageDraw, ImageFont
import os

def draw_sidebar(width=164, height=314):
    """Inno Setup WizardImageFile — 좌측 사이드바 (고급 디자인)"""
    img = Image.new('RGB', (width, height))
    d = ImageDraw.Draw(img)

    # 그라데이션 배경 (상단 밝은 파랑 → 하단 짙은 남색)
    for y in range(height):
        ratio = y / height
        r = int(0 + 8 * ratio)
        g = int(120 - 70 * ratio)
        b = int(215 - 60 * ratio)
        d.line([(0, y), (width, y)], fill=(r, g, b))

    # 상단 장식 — 반투명 원형 보케 (고급 느낌)
    overlay = Image.new('RGBA', (width, height), (0, 0, 0, 0))
    od = ImageDraw.Draw(overlay)
    od.ellipse([-30, -40, 120, 80], fill=(255, 255, 255, 12))
    od.ellipse([80, 20, 200, 140], fill=(255, 255, 255, 8))
    od.ellipse([-20, 180, 80, 280], fill=(255, 255, 255, 6))
    img.paste(Image.alpha_composite(Image.new('RGBA', (width, height), (0,0,0,0)), overlay).convert('RGB'),
              mask=overlay.split()[3])

    # 중앙 아이콘 모티프 (캡쳐 브라켓 + 크로스헤어)
    cx, cy = width // 2, 88
    sz = 36
    lw = 3
    white = (255, 255, 255)
    wdim = (200, 220, 255)
    corner = 12

    # 외곽 브라켓
    d.line([(cx-sz, cy-sz), (cx-sz+corner, cy-sz)], fill=white, width=lw)
    d.line([(cx-sz, cy-sz), (cx-sz, cy-sz+corner)], fill=white, width=lw)
    d.line([(cx+sz, cy-sz), (cx+sz-corner, cy-sz)], fill=white, width=lw)
    d.line([(cx+sz, cy-sz), (cx+sz, cy-sz+corner)], fill=white, width=lw)
    d.line([(cx-sz, cy+sz), (cx-sz+corner, cy+sz)], fill=white, width=lw)
    d.line([(cx-sz, cy+sz), (cx-sz, cy+sz-corner)], fill=white, width=lw)
    d.line([(cx+sz, cy+sz), (cx+sz-corner, cy+sz)], fill=white, width=lw)
    d.line([(cx+sz, cy+sz), (cx+sz, cy+sz-corner)], fill=white, width=lw)

    # 크로스헤어
    cr = 10
    d.line([(cx-cr, cy), (cx+cr, cy)], fill=white, width=2)
    d.line([(cx, cy-cr), (cx, cy+cr)], fill=white, width=2)
    d.ellipse([cx-3, cy-3, cx+3, cy+3], fill=white)

    # 편집 펜 뱃지 (우하단)
    px, py = cx + 22, cy + 22
    d.ellipse([px-10, py-10, px+10, py+10], fill=(255, 150, 30))
    d.line([(px-4, py-4), (px+4, py+4)], fill=white, width=2)

    # 텍스트
    try:
        font_title = ImageFont.truetype("malgunbd.ttf", 20)
        font_sub = ImageFont.truetype("malgun.ttf", 11)
        font_small = ImageFont.truetype("malgun.ttf", 9)
    except:
        font_title = ImageFont.load_default()
        font_sub = font_title
        font_small = font_title

    # 타이틀
    d.text((width // 2, 148), "신캡쳐", fill=white, font=font_title, anchor="mt")
    d.text((width // 2, 174), "ShinCapture", fill=wdim, font=font_sub, anchor="mt")

    # 구분선
    line_y = 198
    d.line([(30, line_y), (width - 30, line_y)], fill=(100, 160, 230), width=1)

    # 특장점 목록
    features = [
        "7가지 캡쳐 모드",
        "14가지 편집 도구",
        "글로벌 단축키",
        "완전 무료 · 광고 없음",
    ]
    y_start = 212
    for i, feat in enumerate(features):
        y = y_start + i * 18
        d.text((24, y), "›", fill=(120, 200, 255), font=font_sub)
        d.text((36, y), feat, fill=(210, 225, 255), font=font_small)

    # 하단 버전
    d.text((width // 2, height - 14), "v1.0.0", fill=(100, 150, 210), font=font_small, anchor="mm")

    return img


def draw_header(width=150, height=57):
    """Inno Setup WizardSmallImageFile — 우상단 로고"""
    img = Image.new('RGB', (width, height), (245, 247, 250))
    d = ImageDraw.Draw(img)

    # 미니 아이콘 (우측)
    ix, iy = width - 28, height // 2
    sz = 16
    lw = 2
    blue = (0, 100, 200)
    corner = 6

    d.line([(ix-sz, iy-sz), (ix-sz+corner, iy-sz)], fill=blue, width=lw)
    d.line([(ix-sz, iy-sz), (ix-sz, iy-sz+corner)], fill=blue, width=lw)
    d.line([(ix+sz, iy-sz), (ix+sz-corner, iy-sz)], fill=blue, width=lw)
    d.line([(ix+sz, iy-sz), (ix+sz, iy-sz+corner)], fill=blue, width=lw)
    d.line([(ix-sz, iy+sz), (ix-sz+corner, iy+sz)], fill=blue, width=lw)
    d.line([(ix-sz, iy+sz), (ix-sz, iy+sz-corner)], fill=blue, width=lw)
    d.line([(ix+sz, iy+sz), (ix+sz-corner, iy+sz)], fill=blue, width=lw)
    d.line([(ix+sz, iy+sz), (ix+sz, iy+sz-corner)], fill=blue, width=lw)

    cr = 5
    d.line([(ix-cr, iy), (ix+cr, iy)], fill=blue, width=1)
    d.line([(ix, iy-cr), (ix, iy+cr)], fill=blue, width=1)
    d.ellipse([ix-2, iy-2, ix+2, iy+2], fill=blue)

    try:
        font = ImageFont.truetype("malgunbd.ttf", 14)
    except:
        font = ImageFont.load_default()
    d.text((10, height // 2), "신캡쳐", fill=(30, 30, 30), font=font, anchor="lm")

    return img


if __name__ == '__main__':
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(script_dir)
    installer_dir = os.path.join(project_root, 'installer')

    sidebar = draw_sidebar()
    sidebar_path = os.path.join(installer_dir, 'wizard_sidebar.bmp')
    sidebar.save(sidebar_path, 'BMP')
    print(f"Sidebar: {sidebar_path} ({os.path.getsize(sidebar_path):,} bytes)")

    header = draw_header()
    header_path = os.path.join(installer_dir, 'wizard_header.bmp')
    header.save(header_path, 'BMP')
    print(f"Header: {header_path} ({os.path.getsize(header_path):,} bytes)")
