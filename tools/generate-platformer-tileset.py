#!/usr/bin/env python3
"""Generates assets/artwork/titanic-demo/platformer-tileset.png.

32x32 frames, drawn at 16x16 and nearest-neighbor doubled for hard pixel edges,
matching the repo's other generated art. Frame order (referenced by level JSON
tileFrames maps):

  0 floor    - riveted steel deck plate (solid)
  1 wall     - darker hull wall plate (solid)
  2 grating  - thin one-way platform bar
  3 ladder   - climbable
  4 pipe     - heavy horizontal pipe (solid)
  5 porthole - background wall with porthole (decor, not solid)
  6 steam    - hissing vent (hazard)
  7 crate    - wooden crate (solid)
"""

from PIL import Image, ImageDraw
import os

TILE = 16
FRAMES = 8

STEEL = (94, 104, 118)
STEEL_DARK = (66, 74, 86)
STEEL_EDGE = (128, 138, 152)
RIVET = (160, 168, 180)
HULL = (52, 56, 66)
HULL_DARK = (38, 41, 49)
BRASS = (168, 138, 74)
BRASS_DARK = (120, 98, 52)
SEA = (54, 96, 128)
WOOD = (140, 104, 62)
WOOD_DARK = (104, 76, 44)
STEAM = (214, 220, 226)
RED = (140, 52, 44)

img = Image.new("RGBA", (TILE * FRAMES, TILE), (0, 0, 0, 0))
d = ImageDraw.Draw(img)


def base(frame, color):
    x = frame * TILE
    d.rectangle([x, 0, x + TILE - 1, TILE - 1], fill=color)
    return x


# 0 floor: steel plate, bright top edge, rivets
x = base(0, STEEL)
d.rectangle([x, 0, x + TILE - 1, 1], fill=STEEL_EDGE)
d.rectangle([x, TILE - 2, x + TILE - 1, TILE - 1], fill=STEEL_DARK)
d.line([x + 7, 2, x + 7, TILE - 3], fill=STEEL_DARK)
for px, py in [(2, 3), (12, 3), (2, 12), (12, 12)]:
    d.point((x + px, y := py), fill=RIVET)

# 1 wall: darker plate, panel seams
x = base(1, HULL)
d.rectangle([x, 0, x + TILE - 1, 0], fill=(70, 75, 86))
d.line([x, 8, x + TILE - 1, 8], fill=HULL_DARK)
d.line([x + 8, 0, x + 8, 7], fill=HULL_DARK)
d.line([x + 3, 9, x + 3, TILE - 1], fill=HULL_DARK)
for px, py in [(1, 2), (14, 6), (6, 12)]:
    d.point((x + px, py), fill=(88, 94, 106))

# 2 grating: thin bar at the tile's top, open below
x = base(2, (0, 0, 0, 0))
d.rectangle([x, 0, x + TILE - 1, 3], fill=STEEL)
d.rectangle([x, 0, x + TILE - 1, 0], fill=STEEL_EDGE)
for gx in range(x + 1, x + TILE - 1, 3):
    d.line([gx, 1, gx, 3], fill=STEEL_DARK)

# 3 ladder: brass rails + rungs on transparent
x = base(3, (0, 0, 0, 0))
d.rectangle([x + 3, 0, x + 4, TILE - 1], fill=BRASS)
d.rectangle([x + 11, 0, x + 12, TILE - 1], fill=BRASS)
d.line([x + 4, 0, x + 4, TILE - 1], fill=BRASS_DARK)
d.line([x + 12, 0, x + 12, TILE - 1], fill=BRASS_DARK)
for ry in range(2, TILE, 5):
    d.rectangle([x + 4, ry, x + 11, ry + 1], fill=BRASS)
    d.line([x + 4, ry + 1, x + 11, ry + 1], fill=BRASS_DARK)

# 4 pipe: fat horizontal pipe with highlight and flanges
x = base(4, (0, 0, 0, 0))
d.rectangle([x, 3, x + TILE - 1, 12], fill=STEEL)
d.rectangle([x, 3, x + TILE - 1, 4], fill=STEEL_EDGE)
d.rectangle([x, 11, x + TILE - 1, 12], fill=STEEL_DARK)
d.rectangle([x + 2, 2, x + 3, 13], fill=STEEL_DARK)
d.rectangle([x + 12, 2, x + 13, 13], fill=STEEL_DARK)

# 5 porthole: hull wall with brass-ringed sea window (background decor)
x = base(5, HULL)
d.line([x, 8, x + TILE - 1, 8], fill=HULL_DARK)
d.ellipse([x + 4, 4, x + 11, 11], fill=SEA, outline=BRASS)
d.point((x + 6, 6), fill=(120, 168, 196))

# 6 steam: vent slab at bottom, rising wisps
x = base(6, (0, 0, 0, 0))
d.rectangle([x, 12, x + TILE - 1, TILE - 1], fill=STEEL_DARK)
d.rectangle([x, 12, x + TILE - 1, 12], fill=RED)
for wx, wy in [(3, 9), (4, 5), (8, 7), (9, 2), (12, 10), (13, 5)]:
    d.point((x + wx, wy), fill=STEAM)
    d.point((x + wx + 1, wy + 1), fill=(180, 188, 196, 180))

# 7 crate: wooden box with cross braces
x = base(7, WOOD)
d.rectangle([x, 0, x + TILE - 1, TILE - 1], outline=WOOD_DARK)
d.line([x + 1, 1, x + TILE - 2, TILE - 2], fill=WOOD_DARK)
d.line([x + TILE - 2, 1, x + 1, TILE - 2], fill=WOOD_DARK)
d.rectangle([x, 0, x + TILE - 1, 0], fill=(164, 126, 78))

out = img.resize((TILE * FRAMES * 2, TILE * 2), Image.NEAREST)
dest = os.path.join(os.path.dirname(__file__), "..",
                    "Majorsilence.Games.Learning", "assets", "artwork",
                    "titanic-demo", "platformer-tileset.png")
out.save(os.path.abspath(dest))
print("wrote", os.path.abspath(dest))
