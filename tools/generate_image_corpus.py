#!/usr/bin/env python3
"""Generate deterministic image fixtures used by SmartDrag's future G3 codec spike.

Requires Pillow. This script is development-only and is not part of the product runtime.
It deliberately creates both valid and malformed samples.
"""
from __future__ import annotations

from pathlib import Path
import hashlib
import json
from PIL import Image, ImageCms, PngImagePlugin

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "tests" / "fixtures" / "images"
OUT.mkdir(parents=True, exist_ok=True)


def gradient(size=(320, 240)) -> Image.Image:
    w, h = size
    img = Image.new("RGB", size)
    px = img.load()
    for y in range(h):
        for x in range(w):
            px[x, y] = (x * 255 // max(1, w - 1), y * 255 // max(1, h - 1), (x + y) * 255 // max(1, w + h - 2))
    return img


base = gradient()
base.save(OUT / "rgb-photo.jpg", quality=92, optimize=False)
base.save(OUT / "rgb-basic.png")

# EXIF Orientation=6 means display rotated 90 degrees clockwise.
exif = Image.Exif()
exif[274] = 6
base.save(OUT / "exif-orientation-6.jpg", quality=92, exif=exif)

# Non-visual textual metadata in a PNG.
png_info = PngImagePlugin.PngInfo()
png_info.add_text("Author", "SmartDrag fixture")
png_info.add_text("Comment", "Must be removable without changing visible pixels")
base.save(OUT / "png-text-metadata.png", pnginfo=png_info)

# Alpha/transparency fixture.
alpha = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
for y in range(128):
    for x in range(128):
        alpha.putpixel((x, y), (255, 80, 20, int(255 * x / 127)))
alpha.save(OUT / "alpha-gradient.png")

# Embedded sRGB ICC profile. Remove Metadata must preserve visual color-management information.
try:
    profile = ImageCms.ImageCmsProfile(ImageCms.createProfile("sRGB")).tobytes()
    base.save(OUT / "icc-srgb.jpg", quality=92, icc_profile=profile)
except Exception as exc:  # pragma: no cover - generator diagnostics only
    print(f"warning: ICC fixture was not generated: {exc}")

# A tiny animated GIF is intentionally outside the current input allowlist; it exists as a negative corpus case.
f1 = Image.new("RGB", (32, 32), "red")
f2 = Image.new("RGB", (32, 32), "blue")
f1.save(OUT / "animated-negative.gif", save_all=True, append_images=[f2], duration=100, loop=0)

# Content/extension mismatch and truncated inputs.
(OUT / "not-an-image.png").write_bytes(b"This is not a PNG despite its extension.\n")
jpeg_bytes = (OUT / "rgb-photo.jpg").read_bytes()
(OUT / "truncated.jpg").write_bytes(jpeg_bytes[: max(16, len(jpeg_bytes) // 5)])

fixture_paths = [path for path in sorted(OUT.iterdir()) if path.is_file() and path.name != "CORPUS_MANIFEST.json"]
manifest = {
    "schema": 1,
    "generatedBy": "tools/generate_image_corpus.py",
    "files": [
        {
            "name": path.name,
            "size": path.stat().st_size,
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        }
        for path in fixture_paths
    ],
}
(OUT / "CORPUS_MANIFEST.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

print(f"Generated fixtures in {OUT}")
for path in fixture_paths:
    print(f"- {path.name}: {path.stat().st_size} bytes")
