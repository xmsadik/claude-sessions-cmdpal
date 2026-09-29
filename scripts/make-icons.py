"""Regenerate the package icons in src/ClaudeSessions/Assets.

Usage: python scripts/make-icons.py   (needs Pillow)

The mark is a terracotta rounded square with a white eight-ray asterisk and a green status dot.
Everything is drawn at 1024 px and downsampled, so small sizes stay smooth.
"""

import math
from pathlib import Path

from PIL import Image, ImageDraw

ASSETS = Path(__file__).resolve().parent.parent / "src" / "ClaudeSessions" / "Assets"
BASE = 1024
TILE = (217, 119, 87, 255)  # terracotta
RAY = (255, 255, 255, 255)
DOT = (34, 197, 94, 255)  # green: a live session


def icon(size: int) -> Image.Image:
    img = Image.new("RGBA", (BASE, BASE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((0, 0, BASE - 1, BASE - 1), radius=int(BASE * 0.22), fill=TILE)

    # Eight rounded rays, slightly up-left of centre to leave room for the dot.
    cx, cy = BASE * 0.46, BASE * 0.46
    inner, outer, width = BASE * 0.07, BASE * 0.30, BASE * 0.085
    for i in range(8):
        a = math.radians(i * 45 + 22.5)
        x0, y0 = cx + inner * math.cos(a), cy + inner * math.sin(a)
        x1, y1 = cx + outer * math.cos(a), cy + outer * math.sin(a)
        d.line((x0, y0, x1, y1), fill=RAY, width=int(width))
        r = width / 2
        d.ellipse((x1 - r, y1 - r, x1 + r, y1 + r), fill=RAY)
    r = inner + width * 0.2
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=RAY)

    # Status dot with a tile-coloured ring so it reads as separate from the rays.
    dx, dy, dr, ring = BASE * 0.76, BASE * 0.76, BASE * 0.13, BASE * 0.045
    d.ellipse((dx - dr - ring, dy - dr - ring, dx + dr + ring, dy + dr + ring), fill=TILE)
    d.ellipse((dx - dr, dy - dr, dx + dr, dy + dr), fill=DOT)

    return img.resize((size, size), Image.LANCZOS)


def centered(width: int, height: int, mark: int) -> Image.Image:
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    img.alpha_composite(icon(mark), ((width - mark) // 2, (height - mark) // 2))
    return img


def main() -> None:
    squares = {
        "StoreLogo.png": 50,
        "LockScreenLogo.scale-200.png": 48,
        "Square44x44Logo.scale-200.png": 88,
        "Square44x44Logo.targetsize-24_altform-unplated.png": 24,
        "Square150x150Logo.scale-200.png": 300,
    }
    for name, size in squares.items():
        icon(size).save(ASSETS / name)

    centered(620, 300, 200).save(ASSETS / "Wide310x150Logo.scale-200.png")
    centered(1240, 600, 360).save(ASSETS / "SplashScreen.scale-200.png")
    print(f"Wrote {len(squares) + 2} icons to {ASSETS}")


if __name__ == "__main__":
    main()
