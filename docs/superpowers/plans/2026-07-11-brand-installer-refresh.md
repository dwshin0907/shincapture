# Brand Icon and Installer Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the dated app and installer artwork with one crisp, deterministic ShinCapture identity that remains sharp from 16px tray use through 250% DPI setup screens.

**Architecture:** A single Pillow-based generator owns the app mark, hand-rendered ICO frames, preview PNG, and light/dark Inno Setup art. Standard-library tests generate into a temporary directory and verify every required frame and aspect ratio. The installer uses Inno Setup 6.7.1 dynamic styling and committed generated assets, so end-user builds do not require Python.

**Tech Stack:** Python 3, Pillow, deterministic raster/vector-style drawing, ICO/DIB encoding, Inno Setup 6.7.1, .NET 8 publish

---

## File map

- Create `tools/gen_brand_assets.py`: single source for mark geometry and all raster outputs.
- Create `tools/tests/test_gen_brand_assets.py`: output contract and ICO frame tests.
- Delete `tools/gen_icon.py`: superseded generator.
- Delete `tools/gen_installer_images.py`: superseded generator.
- Regenerate `src/ShinCapture/Assets/icon.ico`: 16–256px multi-frame app/setup/tray icon.
- Regenerate `src/ShinCapture/Assets/icon_preview.png`: 256px review image.
- Delete `installer/wizard_sidebar.bmp`: low-resolution legacy image.
- Delete `installer/wizard_header.bmp`: wrong-aspect legacy image.
- Create `installer/wizard_sidebar.png`: 534×1022 light-mode setup art.
- Create `installer/wizard_sidebar_dark.png`: 534×1022 dark-mode setup art.
- Create `installer/wizard_mark.png`: 159×159 transparent light-mode header mark.
- Create `installer/wizard_mark_dark.png`: 159×159 transparent dark-mode header mark.
- Modify `installer/setup.iss`: dynamic style, PNG resources, concise copy, correct publish path/version.

### Task 1: Lock the generated-asset contract with tests

**Files:**
- Create: `tools/tests/test_gen_brand_assets.py`

- [ ] **Step 1: Write failing generator contract tests**

Create `tools/tests/test_gen_brand_assets.py`:

```python
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from tools import gen_brand_assets


class BrandAssetGeneratorTests(unittest.TestCase):
    def test_generate_all_creates_required_assets(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            gen_brand_assets.generate_all(root)

            expected = [
                root / "src/ShinCapture/Assets/icon.ico",
                root / "src/ShinCapture/Assets/icon_preview.png",
                root / "installer/wizard_sidebar.png",
                root / "installer/wizard_sidebar_dark.png",
                root / "installer/wizard_mark.png",
                root / "installer/wizard_mark_dark.png",
            ]
            self.assertTrue(all(path.is_file() for path in expected))

    def test_ico_contains_every_required_size(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            gen_brand_assets.generate_all(root)

            with Image.open(root / "src/ShinCapture/Assets/icon.ico") as icon:
                encoded_sizes = {width for width, height in icon.info["sizes"] if width == height}
                self.assertTrue(set(gen_brand_assets.ICON_SIZES).issubset(encoded_sizes))

    def test_installer_assets_match_high_dpi_contract(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            gen_brand_assets.generate_all(root)

            for name in ("wizard_sidebar.png", "wizard_sidebar_dark.png"):
                with Image.open(root / "installer" / name) as image:
                    self.assertEqual((534, 1022), image.size)
                    self.assertEqual("RGBA", image.mode)
            for name in ("wizard_mark.png", "wizard_mark_dark.png"):
                with Image.open(root / "installer" / name) as image:
                    self.assertEqual((159, 159), image.size)
                    self.assertEqual("RGBA", image.mode)

    def test_small_icon_remains_simple_and_nonempty(self):
        image = gen_brand_assets.draw_app_icon(16)
        colors = image.convert("RGBA").getcolors(maxcolors=256)

        self.assertEqual((16, 16), image.size)
        self.assertIsNotNone(colors)
        self.assertGreater(len(colors), 2)
        self.assertLess(len(colors), 96)


if __name__ == "__main__":
    unittest.main()
```

- [ ] **Step 2: Run tests and confirm the missing-module failure**

```powershell
python -m unittest discover -s tools/tests -p "test_*.py" -v
```

Expected: FAIL because `tools.gen_brand_assets` does not exist.

### Task 2: Implement the deterministic brand generator

**Files:**
- Create: `tools/gen_brand_assets.py`
- Delete: `tools/gen_icon.py`
- Delete: `tools/gen_installer_images.py`
- Generate: `src/ShinCapture/Assets/icon.ico`
- Generate: `src/ShinCapture/Assets/icon_preview.png`
- Generate: `installer/wizard_sidebar.png`
- Generate: `installer/wizard_sidebar_dark.png`
- Generate: `installer/wizard_mark.png`
- Generate: `installer/wizard_mark_dark.png`
- Delete: `installer/wizard_sidebar.bmp`
- Delete: `installer/wizard_header.bmp`

- [ ] **Step 1: Create the palette, supersampling, and mark primitives**

Start `tools/gen_brand_assets.py` with:

```python
"""Generate ShinCapture app, tray, and Inno Setup brand assets."""

from __future__ import annotations

import io
import struct
from pathlib import Path

from PIL import Image, ImageDraw


ICON_SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)
INDIGO = (91, 91, 214, 255)
INDIGO_DARK = (49, 46, 129, 255)
NAVY = (18, 20, 38, 255)
WHITE = (250, 251, 255, 255)
MINT = (61, 214, 174, 255)
PALE = (239, 240, 255, 255)


def draw_mark(canvas: Image.Image, box: tuple[int, int, int, int], *, dark: bool = False) -> None:
    """Draw a capture-ribbon S with one mint focus point."""
    left, top, right, bottom = box
    size = min(right - left, bottom - top)
    ox = left + (right - left - size) / 2
    oy = top + (bottom - top - size) / 2
    draw = ImageDraw.Draw(canvas)
    stroke = max(2, round(size * 0.105))
    points = [
        (ox + size * 0.74, oy + size * 0.26),
        (ox + size * 0.43, oy + size * 0.26),
        (ox + size * 0.28, oy + size * 0.40),
        (ox + size * 0.42, oy + size * 0.50),
        (ox + size * 0.61, oy + size * 0.50),
        (ox + size * 0.74, oy + size * 0.62),
        (ox + size * 0.60, oy + size * 0.75),
        (ox + size * 0.27, oy + size * 0.75),
    ]
    draw.line(points, fill=WHITE if dark else INDIGO_DARK, width=stroke, joint="curve")
    radius = max(1, round(size * 0.055))
    cx, cy = ox + size * 0.77, oy + size * 0.25
    draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=MINT)
```

The mark is intentionally a single thick ribbon plus one focus point. Do not restore the old crosshair, orange badge, feature text, or decorative micro-lines.

- [ ] **Step 2: Add hand-rendered app icon frames**

Append:

```python
def draw_app_icon(size: int) -> Image.Image:
    scale = 4 if size <= 48 else 2
    px = size * scale
    image = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    margin = round(px * 0.055)
    radius = round(px * (0.22 if size >= 32 else 0.18))
    draw.rounded_rectangle(
        (margin, margin, px - margin - 1, px - margin - 1),
        radius=radius,
        fill=INDIGO,
    )
    mark_margin = round(px * (0.18 if size >= 24 else 0.15))
    draw_mark(image, (mark_margin, mark_margin, px - mark_margin, px - mark_margin), dark=True)
    return image.resize((size, size), Image.Resampling.LANCZOS)


def _image_to_dib(image: Image.Image) -> bytes:
    width, height = image.size
    pixels = image.convert("RGBA").load()
    header = struct.pack(
        "<IiiHHIIiiII", 40, width, height * 2, 1, 32, 0,
        width * height * 4, 0, 0, 0, 0,
    )
    xor = bytearray()
    for y in range(height - 1, -1, -1):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            xor.extend((b, g, r, a))
    mask_row = ((width + 31) // 32) * 4
    return header + bytes(xor) + bytes(mask_row * height)


def write_ico(path: Path) -> None:
    frames = []
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
        directory.extend(struct.pack(
            "<BBBBHHII", encoded_size, encoded_size, 0, 0,
            1, 32, len(payload), offset,
        ))
        body.extend(payload)
        offset += len(payload)
    path.write_bytes(header + bytes(directory) + bytes(body))
```

- [ ] **Step 3: Add light/dark installer art**

Append:

```python
def draw_sidebar(*, dark: bool) -> Image.Image:
    width, height = 534, 1022
    background = NAVY if dark else PALE
    image = Image.new("RGBA", (width, height), background)
    draw = ImageDraw.Draw(image)
    accent = INDIGO if dark else (220, 222, 255, 255)
    draw.rounded_rectangle((-170, -80, 430, 420), radius=150, fill=accent)
    draw.rounded_rectangle((150, 700, 690, 1120), radius=140,
                           fill=INDIGO_DARK if dark else (227, 249, 242, 255))
    plate_size = 260
    plate_left = (width - plate_size) // 2
    plate_top = 260
    plate_fill = INDIGO if not dark else INDIGO_DARK
    draw.rounded_rectangle(
        (plate_left, plate_top, plate_left + plate_size, plate_top + plate_size),
        radius=58, fill=plate_fill,
    )
    draw_mark(
        image,
        (plate_left + 38, plate_top + 38, plate_left + plate_size - 38, plate_top + plate_size - 38),
        dark=True,
    )
    draw.rounded_rectangle((72, 620, 462, 628), radius=4, fill=MINT)
    return image


def draw_header_mark(*, dark: bool) -> Image.Image:
    size = 159
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    fill = INDIGO_DARK if dark else INDIGO
    draw.rounded_rectangle((16, 16, 143, 143), radius=30, fill=fill)
    draw_mark(image, (36, 36, 123, 123), dark=True)
    return image
```

- [ ] **Step 4: Add one public generation entry point**

Append:

```python
def generate_all(project_root: Path) -> None:
    project_root = Path(project_root)
    assets = project_root / "src/ShinCapture/Assets"
    installer = project_root / "installer"
    assets.mkdir(parents=True, exist_ok=True)
    installer.mkdir(parents=True, exist_ok=True)

    write_ico(assets / "icon.ico")
    draw_app_icon(256).save(assets / "icon_preview.png", "PNG", optimize=True)
    draw_sidebar(dark=False).save(installer / "wizard_sidebar.png", "PNG", optimize=True)
    draw_sidebar(dark=True).save(installer / "wizard_sidebar_dark.png", "PNG", optimize=True)
    draw_header_mark(dark=False).save(installer / "wizard_mark.png", "PNG", optimize=True)
    draw_header_mark(dark=True).save(installer / "wizard_mark_dark.png", "PNG", optimize=True)


if __name__ == "__main__":
    generate_all(Path(__file__).resolve().parents[1])
```

- [ ] **Step 5: Run tests, generate committed assets, and remove superseded files**

```powershell
python -m unittest discover -s tools/tests -p "test_*.py" -v
python tools/gen_brand_assets.py
git rm tools/gen_icon.py tools/gen_installer_images.py installer/wizard_sidebar.bmp installer/wizard_header.bmp
```

Expected: 4 tests PASS; six generated files exist; the four obsolete files are staged for deletion.

- [ ] **Step 6: Inspect the generated images at original resolution**

Open `icon_preview.png`, both sidebars, and both marks. Confirm:

```text
No orange badge, text, feature list, version, or tiny decoration
Mark reads as one bold S/capture ribbon at 16px
Mint accent is a single focus point/line, not a competing badge
Light and dark sidebars share identical geometry
All transparent edges are clean
```

If the 16px frame is muddy, adjust `draw_app_icon` geometry and rerun the tests/generator before committing.

- [ ] **Step 7: Commit generator and assets**

```powershell
git add tools/gen_brand_assets.py tools/tests/test_gen_brand_assets.py src/ShinCapture/Assets/icon.ico src/ShinCapture/Assets/icon_preview.png installer/wizard_sidebar.png installer/wizard_sidebar_dark.png installer/wizard_mark.png installer/wizard_mark_dark.png
git commit -m "feat: 신캡쳐 브랜드 아이콘과 고DPI 자산 개편"
```

### Task 3: Modernize the Inno Setup presentation

**Files:**
- Modify: `installer/setup.iss`

- [ ] **Step 1: Point Setup at the current version and build output**

Change the local fallback version and executable source:

```iss
#define MyAppVersion "1.3.6"
```

```iss
Source: "..\publish\ShinCapture.exe"; DestDir: "{app}"; Flags: ignoreversion
```

CI may continue to override `MyAppVersion` with `/DMyAppVersion=...`.

- [ ] **Step 2: Enable dynamic Windows styling and the new PNGs**

Replace the visual block with:

```iss
WizardStyle=modern dynamic windows11 hidebevels includetitlebar
WizardSizePercent=110
SetupIconFile=..\src\ShinCapture\Assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardImageFile=wizard_sidebar.png
WizardImageFileDynamicDark=wizard_sidebar_dark.png
WizardSmallImageFile=wizard_mark.png
WizardSmallImageFileDynamicDark=wizard_mark_dark.png
WizardImageBackColor=#EFF0FF
WizardImageBackColorDynamicDark=#121426
WizardSmallImageBackColor=#FFFFFF
WizardSmallImageBackColorDynamicDark=#1A1C2B
```

- [ ] **Step 3: Replace crowded welcome copy with three durable benefits**

Use:

```iss
korean.WelcomeLabel1=%n신캡쳐 v{#MyAppVersion}
korean.WelcomeLabel2=캡처부터 편집까지, 흐름을 끊지 않게.%n%n  ✓  바로 캡처 — 영역·창·스크롤·텍스트를 단축키 한 번으로%n  ✓  바로 편집 — 화살표·모자이크·번호·OCR을 한 화면에서%n  ✓  조용하게 — 광고와 회원가입 없이 트레이에서 가볍게%n%n[다음]을 누르면 설치를 시작합니다.
```

Remove this unused task:

```iss
Name: "quicklaunchicon"; Description: "작업 표시줄에 고정"; GroupDescription: "추가 설정:"; Flags: unchecked
```

- [ ] **Step 4: Shorten the finished-page copy**

Replace only the `WizardForm.FinishedLabel.Caption` assignment with:

```iss
WizardForm.FinishedLabel.Caption :=
  '신캡쳐를 사용할 준비가 되었습니다.' + #13#10 + #13#10 +
  '빠른 시작' + #13#10 +
  '  PrintScreen — 영역 캡처' + #13#10 +
  '  Ctrl+Shift+W — 창 캡처' + #13#10 +
  '  Ctrl+Shift+S — 스크롤 캡처' + #13#10 + #13#10 +
  '앱은 시스템 트레이에 머물며 언제든 바로 열 수 있습니다.';
```

Keep the existing completion heading and uninstall behavior.

- [ ] **Step 5: Publish the app and compile Setup**

```powershell
dotnet publish src/ShinCapture/ShinCapture.csproj -c Release -r win-x64 --self-contained -o publish
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" "installer\setup.iss"
```

Expected: `publish/ShinCapture.exe` is created; ISCC exits 0 and writes `dist/ShinCapture_Setup_v1.3.6.exe`.

- [ ] **Step 6: Commit the setup script**

```powershell
git add installer/setup.iss
git commit -m "feat: 설치 마법사 UI와 카피 현대화"
```

### Task 4: App, tray, and installer visual QA

**Files:**
- Modify generator/setup only if QA finds a defect.

- [ ] **Step 1: Verify generated dimensions and ICO frames again**

```powershell
python -m unittest discover -s tools/tests -p "test_*.py" -v
```

Expected: all 4 tests PASS.

- [ ] **Step 2: Verify the app resource pipeline**

```powershell
dotnet build ShinCapture.sln -c Release
```

Expected: build succeeds and `ShinCapture.exe` uses `Assets/icon.ico` through `ApplicationIcon`.

- [ ] **Step 3: Inspect small icon sizes**

Inspect the tray icon and Explorer list/detail/large icon views at 100%, 150%, and 200% scaling. Expected: the S ribbon remains identifiable, the mint point does not merge into the white stroke, and no old orange pencil badge remains in the executable, Start menu, desktop shortcut, or uninstall entry.

- [ ] **Step 4: Inspect Setup in light and dark modes**

Run the compiled installer in both Windows app modes when available. Check welcome, destination, tasks, progress, finish, and uninstall screens at 100%, 150%, and 200%.

Expected:

```text
Sidebar is sharp and keeps 164:314 composition
Header mark is square and not stretched
Text never overlaps artwork
Dynamic title bar/control colors remain legible
No v1.0.0 or obsolete feature counts appear in images
No unused taskbar-pin option appears
```

- [ ] **Step 5: Run final project verification**

```powershell
dotnet test ShinCapture.sln -c Release
git diff --check
git status --short
```

Expected: all tests PASS, no whitespace errors, only intended tracked changes/commits, and pre-existing untracked user artifacts remain untouched.
