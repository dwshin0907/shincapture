import struct
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from tools import gen_brand_assets


ASSET_PATHS = (
    Path("src/ShinCapture/Assets/icon.ico"),
    Path("src/ShinCapture/Assets/icon_preview.png"),
    Path("installer/wizard_sidebar.png"),
    Path("installer/wizard_sidebar_dark.png"),
    Path("installer/wizard_mark.png"),
    Path("installer/wizard_mark_dark.png"),
)


def _transition_signature(image: Image.Image) -> bytes:
    """Return a color-independent mask of every composition boundary."""
    rgba = image.convert("RGBA")
    width, height = rgba.size
    pixels = rgba.load()
    signature = bytearray(width * height)

    for y in range(height):
        row = y * width
        for x in range(width):
            pixel = pixels[x, y]
            if ((x > 0 and pixel != pixels[x - 1, y]) or
                    (y > 0 and pixel != pixels[x, y - 1])):
                signature[row + x] = 1

    return bytes(signature)


class BrandAssetGeneratorTests(unittest.TestCase):
    def test_generate_all_creates_exactly_the_six_required_assets(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            gen_brand_assets.generate_all(root)

            generated = {
                path.relative_to(root)
                for path in root.rglob("*")
                if path.is_file()
            }
            self.assertEqual(set(ASSET_PATHS), generated)

    def test_png_assets_match_dimensions_and_modes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            gen_brand_assets.generate_all(root)

            expected = {
                Path("src/ShinCapture/Assets/icon_preview.png"): (256, 256),
                Path("installer/wizard_sidebar.png"): (534, 1022),
                Path("installer/wizard_sidebar_dark.png"): (534, 1022),
                Path("installer/wizard_mark.png"): (159, 159),
                Path("installer/wizard_mark_dark.png"): (159, 159),
            }
            for relative_path, size in expected.items():
                with self.subTest(asset=str(relative_path)):
                    with Image.open(root / relative_path) as image:
                        self.assertEqual(size, image.size)
                        self.assertEqual("RGBA", image.mode)

            for name in ("wizard_mark.png", "wizard_mark_dark.png"):
                with Image.open(root / "installer" / name) as mark:
                    self.assertEqual((0, 255), mark.getchannel("A").getextrema())

    def test_ico_contains_every_hand_built_frame(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            gen_brand_assets.generate_all(root)
            icon_path = root / "src/ShinCapture/Assets/icon.ico"

            data = icon_path.read_bytes()
            reserved, image_type, count = struct.unpack_from("<HHH", data)
            self.assertEqual((0, 1, len(gen_brand_assets.ICON_SIZES)),
                             (reserved, image_type, count))

            directory_sizes = []
            for index in range(count):
                entry = struct.unpack_from("<BBBBHHII", data, 6 + index * 16)
                width, height, _, _, planes, bit_count, length, offset = entry
                frame_width = 256 if width == 0 else width
                frame_height = 256 if height == 0 else height
                directory_sizes.append(frame_width)
                self.assertEqual(frame_width, frame_height)
                self.assertEqual((1, 32), (planes, bit_count))
                self.assertLessEqual(offset + length, len(data))

                payload = data[offset:offset + length]
                if frame_width >= 128:
                    self.assertTrue(payload.startswith(b"\x89PNG\r\n\x1a\n"))
                else:
                    self.assertEqual(40, struct.unpack_from("<I", payload)[0])

            self.assertEqual(list(gen_brand_assets.ICON_SIZES), directory_sizes)

            with Image.open(icon_path) as icon:
                expected_sizes = {(size, size) for size in gen_brand_assets.ICON_SIZES}
                self.assertEqual(expected_sizes, set(icon.info["sizes"]))
                for size in gen_brand_assets.ICON_SIZES:
                    with self.subTest(size=size):
                        frame = icon.ico.getimage((size, size))
                        self.assertEqual((size, size), frame.size)
                        self.assertEqual("RGBA", frame.mode)

    def test_small_icon_remains_simple_nonempty_and_without_orange(self):
        image = gen_brand_assets.draw_app_icon(16)
        colors = image.convert("RGBA").getcolors(maxcolors=256)

        self.assertEqual((16, 16), image.size)
        self.assertEqual("RGBA", image.mode)
        self.assertIsNotNone(image.getbbox())
        self.assertIsNotNone(colors)
        self.assertGreater(len(colors), 2)
        self.assertLess(len(colors), 96)

        orange_pixels = sum(
            count
            for count, (red, green, blue, alpha) in colors
            if alpha > 0 and red > 180 and 60 < green < 190 and blue < 100
        )
        self.assertEqual(0, orange_pixels)

    def test_generation_is_byte_for_byte_deterministic(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            gen_brand_assets.generate_all(root)
            first = {path: (root / path).read_bytes() for path in ASSET_PATHS}

            gen_brand_assets.generate_all(root)
            second = {path: (root / path).read_bytes() for path in ASSET_PATHS}

            self.assertEqual(first, second)

    def test_light_and_dark_sidebars_share_composition_geometry(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            gen_brand_assets.generate_all(root)

            with Image.open(root / "installer/wizard_sidebar.png") as light_image:
                light = light_image.copy()
            with Image.open(root / "installer/wizard_sidebar_dark.png") as dark_image:
                dark = dark_image.copy()

            self.assertNotEqual(light.tobytes(), dark.tobytes())
            self.assertEqual(
                _transition_signature(light),
                _transition_signature(dark),
            )


if __name__ == "__main__":
    unittest.main()
