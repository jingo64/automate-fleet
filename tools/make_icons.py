"""Builds the operation icons from the game's Transfer icon: speed streaks removed, a cog added.

python make_icons.py <dir with ICO_TransferOperation_on/off.png> <output dir>
(extract the source icons from StreamingAssets/AssetBundles/operations with UnityPy)
"""
import math
import sys

from PIL import Image, ImageChops, ImageDraw

SRC, OUT = sys.argv[1], sys.argv[2]
BACKGROUND = {"off": (74, 109, 115), "on": (99, 199, 214)}
GREEN = (99, 241, 156, 255)
RED = (222, 48, 8, 255)
OUTLINE = (30, 50, 55, 255)
SCALE = 8  # draw large, then shrink for smooth edges


def remove_streaks(im, bg):
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a > 0 and g > b + 20 and g > r + 20:
                px[x, y] = (*bg, a)


def cog(cx, cy, radius, teeth=8):
    """Polygon points for a cog centred on (cx, cy)."""
    points = []
    inner, outer = radius * 0.78, radius
    steps = teeth * 4
    for i in range(steps):
        angle = 2 * math.pi * i / steps
        r = outer if i % 4 in (1, 2) else inner
        points.append((cx + r * math.cos(angle), cy + r * math.sin(angle)))
    return points


def overlay(size, cancel):
    big = Image.new("RGBA", (size * SCALE, size * SCALE), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    s = SCALE
    cx, cy, r = 42 * s, 42 * s, 10 * s
    d.polygon(cog(cx, cy, r + 1.5 * s), fill=OUTLINE)
    d.polygon(cog(cx, cy, r), fill=GREEN)
    d.ellipse([cx - 4.5 * s, cy - 4.5 * s, cx + 4.5 * s, cy + 4.5 * s], fill=OUTLINE)
    d.ellipse([cx - 3.2 * s, cy - 3.2 * s, cx + 3.2 * s, cy + 3.2 * s], fill=(0, 0, 0, 0))
    if cancel:
        c, rr, w = 32 * s, 21 * s, 4 * s
        d.ellipse([c - rr, c - rr, c + rr, c + rr], outline=RED, width=w)
        off = rr * math.cos(math.pi / 4) - w / 2
        d.line([c - off, c - off, c + off, c + off], fill=RED, width=w)
    return big.resize((size, size), Image.LANCZOS)


for state, bg in BACKGROUND.items():
    base = Image.open(f"{SRC}/ICO_TransferOperation_{state}.png").convert("RGBA")
    remove_streaks(base, bg)
    for name, cancel in (("AutomateFleetOperation", False), ("StopFleetAutomationOperation", True)):
        icon = base.copy()
        layer = overlay(icon.width, cancel)
        # Keep the cog inside the hexagon: only draw where the base icon is opaque
        layer.putalpha(ImageChops.darker(layer.getchannel("A"), base.getchannel("A")))
        icon.alpha_composite(layer)
        icon.save(f"{OUT}/ICO_{name}_{state}.png")
        print("wrote", name, state)
