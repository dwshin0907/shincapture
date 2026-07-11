"""Generate ShinCapture app, tray, and Inno Setup brand assets."""

from __future__ import annotations

import io
import struct
from functools import lru_cache
from pathlib import Path

from PIL import Image, ImageDraw


ICON_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)
INDIGO = (91, 91, 214, 255)
INDIGO_DARK = (49, 46, 129, 255)
NAVY = (18, 20, 38, 255)
WHITE = (250, 251, 255, 255)
MINT = (61, 214, 174, 255)
PALE = (239, 240, 255, 255)


@lru_cache(maxsize=None)
def _scaled_mark_geometry(
    size: int,
) -> tuple[int, tuple[tuple[int, int], ...], int, int, int]:
    """Return cached integer geometry for a mark of ``size`` pixels."""
    stroke = max(2, round(size * 0.105))
    points = tuple(
        (round(size * x), round(size * y))
        for x, y in (
            (0.74, 0.26),
            (0.43, 0.26),
            (0.28, 0.40),
            (0.42, 0.50),
            (0.61, 0.50),
            (0.74, 0.62),
            (0.60, 0.75),
            (0.27, 0.75),
        )
    )
    radius = max(1, round(size * 0.055))
    focus_x = round(size * 0.77)
    focus_y = round(size * 0.25)
    return stroke, points, focus_x, focus_y, radius


def draw_mark(
    canvas: Image.Image,
    box: tuple[int, int, int, int],
    *,
    dark: bool = False,
) -> None:
    """Draw a capture-ribbon S with one mint focus point."""
    left, top, right, bottom = box
    size = min(right - left, bottom - top)
    offset_x = left + (right - left - size) // 2
    offset_y = top + (bottom - top - size) // 2
    stroke, cached_points, focus_x, focus_y, radius = _scaled_mark_geometry(size)
    points = [(offset_x + x, offset_y + y) for x, y in cached_points]

    draw = ImageDraw.Draw(canvas)
    draw.line(
        points,
        fill=WHITE if dark else INDIGO_DARK,
        width=stroke,
        joint="curve",
    )
    center_x = offset_x + focus_x
    center_y = offset_y + focus_y
    draw.ellipse(
        (
            center_x - radius,
            center_y - radius,
            center_x + radius,
            center_y + radius,
        ),
        fill=MINT,
    )


def draw_app_icon(size: int) -> Image.Image:
    """Render one supersampled, flat app-icon frame."""
    if size <= 0:
        raise ValueError("Icon size must be positive.")

    scale = 4 if size <= 48 else 2
    pixels = size * scale
    image = Image.new("RGBA", (pixels, pixels), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    margin = round(pixels * 0.055)
    radius = round(pixels * (0.22 if size >= 32 else 0.18))
    draw.rounded_rectangle(
        (margin, margin, pixels - margin - 1, pixels - margin - 1),
        radius=radius,
        fill=INDIGO,
    )
    mark_margin = round(pixels * (0.18 if size >= 24 else 0.15))
    draw_mark(
        image,
        (mark_margin, mark_margin, pixels - mark_margin, pixels - mark_margin),
        dark=True,
    )
    result = image.resize((size, size), Image.Resampling.LANCZOS)
    if size <= 20:
        result = result.quantize(
            colors=64,
            method=Image.Quantize.FASTOCTREE,
            dither=Image.Dither.NONE,
        ).convert("RGBA")
    return result


def _image_to_dib(image: Image.Image) -> bytes:
    """Encode one RGBA image as an ICO-compatible 32-bit DIB frame."""
    width, height = image.size
    pixels = image.convert("RGBA").load()
    header = struct.pack(
        "<IiiHHIIiiII",
        40,
        width,
        height * 2,
        1,
        32,
        0,
        width * height * 4,
        0,
        0,
        0,
        0,
    )
    xor = bytearray()
    mask_stride = ((width + 31) // 32) * 4
    mask = bytearray(mask_stride * height)
    for bottom_up_row, y in enumerate(range(height - 1, -1, -1)):
        for x in range(width):
            red, green, blue, alpha = pixels[x, y]
            xor.extend((blue, green, red, alpha))
            if alpha == 0:
                mask[bottom_up_row * mask_stride + x // 8] |= 0x80 >> (x % 8)
    return header + bytes(xor) + bytes(mask)


def write_ico(path: Path) -> None:
    """Write every required app-icon frame into one hand-built ICO."""
    frames: list[tuple[int, bytes]] = []
    for size in ICON_SIZES:
        image = draw_app_icon(size)
        if size >= 128:
            buffer = io.BytesIO()
            image.save(buffer, "PNG", optimize=True)
            payload = buffer.getvalue()
        else:
            payload = _image_to_dib(image)
        frames.append((size, payload))

    header = struct.pack("<HHH", 0, 1, len(frames))
    directory = bytearray()
    body = bytearray()
    offset = 6 + len(frames) * 16
    for size, payload in frames:
        encoded_size = 0 if size == 256 else size
        directory.extend(
            struct.pack(
                "<BBBBHHII",
                encoded_size,
                encoded_size,
                0,
                0,
                1,
                32,
                len(payload),
                offset,
            )
        )
        body.extend(payload)
        offset += len(payload)
    path.write_bytes(header + bytes(directory) + bytes(body))


def draw_sidebar(*, dark: bool) -> Image.Image:
    """Render light or dark installer sidebar art with shared geometry."""
    width, height = 534, 1022
    background = NAVY if dark else PALE
    image = Image.new("RGBA", (width, height), background)
    draw = ImageDraw.Draw(image)
    accent = INDIGO if dark else (220, 222, 255, 255)
    draw.rounded_rectangle((-170, -80, 430, 420), radius=150, fill=accent)
    draw.rounded_rectangle(
        (150, 700, 690, 1120),
        radius=140,
        fill=INDIGO_DARK if dark else (227, 249, 242, 255),
    )

    plate_size = 260
    plate_left = (width - plate_size) // 2
    plate_top = 260
    plate_fill = INDIGO_DARK if dark else INDIGO
    draw.rounded_rectangle(
        (plate_left, plate_top, plate_left + plate_size, plate_top + plate_size),
        radius=58,
        fill=plate_fill,
    )
    draw_mark(
        image,
        (
            plate_left + 38,
            plate_top + 38,
            plate_left + plate_size - 38,
            plate_top + plate_size - 38,
        ),
        dark=True,
    )
    draw.rounded_rectangle((72, 620, 462, 628), radius=4, fill=MINT)
    return image


def draw_header_mark(*, dark: bool) -> Image.Image:
    """Render a transparent light or dark installer header mark."""
    size = 159
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    fill = INDIGO_DARK if dark else INDIGO
    draw.rounded_rectangle((16, 16, 143, 143), radius=30, fill=fill)
    draw_mark(image, (36, 36, 123, 123), dark=True)
    return image


def generate_all(project_root: Path) -> None:
    """Generate all committed app and installer brand assets."""
    project_root = Path(project_root)
    assets = project_root / "src/ShinCapture/Assets"
    installer = project_root / "installer"
    assets.mkdir(parents=True, exist_ok=True)
    installer.mkdir(parents=True, exist_ok=True)

    write_ico(assets / "icon.ico")
    draw_app_icon(256).save(assets / "icon_preview.png", "PNG", optimize=True)
    draw_sidebar(dark=False).save(
        installer / "wizard_sidebar.png", "PNG", optimize=True
    )
    draw_sidebar(dark=True).save(
        installer / "wizard_sidebar_dark.png", "PNG", optimize=True
    )
    draw_header_mark(dark=False).save(
        installer / "wizard_mark.png", "PNG", optimize=True
    )
    draw_header_mark(dark=True).save(
        installer / "wizard_mark_dark.png", "PNG", optimize=True
    )


if __name__ == "__main__":
    generate_all(Path(__file__).resolve().parents[1])
