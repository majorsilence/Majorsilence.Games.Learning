#!/usr/bin/env python3
"""Generates the original artwork for the RPG demo.

Everything here is drawn from scratch in a NES-era palette (52 hues, 4 colours
a tile) - the look of the period, none of anyone else's pixels.

Outputs, all 16x16 frames laid out left to right:

  assets/artwork/rpg/tileset.png  - 18 terrain/structure frames
  assets/artwork/rpg/hero.png     - 8 frames: down/up/left/right x 2 walk poses
  assets/artwork/rpg/folk.png     - 24 frames: 3 townsfolk palettes, same poses

Tileset frame order (referenced by level JSON tileFrames maps):

   0 grass     1 tree      2 water     3 sand
   4 mountain  5 road      6 wall      7 roof
   8 door      9 floor    10 counter  11 chest
  12 sign     13 bridge   14 flowers  15 cave
  16 bedHead  17 bedFoot

A bed is two tiles stacked, head above foot - the shape inn rooms are built
from, and too tall to read in a single 16x16 frame.
"""

from PIL import Image, ImageDraw
import os

TILE = 16

# A NES-ish working palette: muted, few steps per hue.
GRASS = (88, 152, 56)
GRASS_D = (56, 112, 40)
GRASS_L = (128, 184, 80)
LEAF = (40, 104, 48)
LEAF_D = (24, 72, 36)
TRUNK = (104, 68, 36)
WATER = (56, 88, 176)
WATER_D = (32, 56, 136)
WATER_L = (104, 144, 216)
SAND = (216, 192, 120)
SAND_D = (176, 148, 88)
ROCK = (128, 120, 112)
ROCK_D = (88, 82, 78)
ROCK_L = (168, 160, 152)
ROAD = (188, 160, 112)
ROAD_D = (148, 124, 84)
BRICK = (176, 128, 96)
BRICK_D = (128, 88, 64)
ROOF = (168, 56, 56)
ROOF_D = (112, 32, 36)
WOOD = (140, 100, 56)
WOOD_D = (96, 66, 36)
FLOOR = (196, 168, 128)
FLOOR_D = (156, 128, 92)
GOLD = (232, 192, 72)
GOLD_D = (168, 128, 32)
DARK = (24, 20, 32)
WHITE = (240, 240, 232)
SKIN = (232, 176, 128)
HAIR = (96, 56, 32)


def frame(draw, index, fill):
    x0 = index * TILE
    draw.rectangle([x0, 0, x0 + TILE - 1, TILE - 1], fill=fill)
    return x0


def speckle(draw, x0, colour, points):
    for px, py in points:
        draw.point((x0 + px, py), fill=colour)


def build_tileset(path):
    img = Image.new("RGBA", (TILE * 18, TILE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # 0 grass - flat base with a few blades so large fields aren't dead flat
    x = frame(d, 0, GRASS)
    speckle(d, x, GRASS_D, [(3, 4), (4, 5), (11, 9), (12, 10), (7, 13)])
    speckle(d, x, GRASS_L, [(8, 3), (2, 11), (13, 5)])

    # 1 tree - round canopy over a short trunk (solid)
    x = frame(d, 1, GRASS)
    d.ellipse([x + 2, 1, x + 13, 11], fill=LEAF)
    d.ellipse([x + 4, 2, x + 9, 7], fill=GRASS_L)
    d.ellipse([x + 3, 5, x + 12, 12], outline=LEAF_D)
    d.rectangle([x + 7, 11, x + 8, 14], fill=TRUNK)

    # 2 water - banded ripples (solid to walkers, crossed by bridges)
    x = frame(d, 2, WATER)
    for row in (2, 7, 12):
        d.line([x + 1, row, x + 6, row], fill=WATER_L)
        d.line([x + 9, row + 2, x + 14, row + 2], fill=WATER_D)

    # 3 sand - beach/desert
    x = frame(d, 3, SAND)
    speckle(d, x, SAND_D, [(2, 3), (9, 5), (5, 10), (12, 12), (7, 7)])

    # 4 mountain - two peaks (solid)
    x = frame(d, 4, GRASS)
    d.polygon([(x + 1, 15), (x + 6, 3), (x + 11, 15)], fill=ROCK)
    d.polygon([(x + 6, 15), (x + 11, 6), (x + 15, 15)], fill=ROCK_D)
    d.polygon([(x + 4, 9), (x + 6, 5), (x + 8, 9)], fill=ROCK_L)

    # 5 road - packed dirt
    x = frame(d, 5, ROAD)
    speckle(d, x, ROAD_D, [(3, 2), (10, 4), (6, 9), (13, 11), (1, 13)])

    # 6 wall - stone block courses (solid)
    x = frame(d, 6, BRICK)
    for row in (0, 5, 10):
        d.line([x, row, x + 15, row], fill=BRICK_D)
    for row, offset in ((2, 0), (7, 8), (12, 0)):
        d.line([x + offset, row - 2, x + offset, row + 2], fill=BRICK_D)

    # 7 roof - shingled, sloping (solid)
    x = frame(d, 7, ROOF)
    for row in range(0, 16, 4):
        d.line([x, row, x + 15, row], fill=ROOF_D)
        for col in range(2, 16, 4):
            d.point((x + col, row + 2), fill=ROOF_D)

    # 8 door - arched opening in a wall (walkable; the level marks it a door)
    x = frame(d, 8, BRICK)
    d.rectangle([x + 4, 5, x + 11, 15], fill=WOOD_D)
    d.pieslice([x + 4, 2, x + 11, 9], 180, 360, fill=WOOD_D)
    d.rectangle([x + 5, 7, x + 10, 15], fill=WOOD)
    d.point((x + 9, 11), fill=GOLD)

    # 9 floor - interior boards
    x = frame(d, 9, FLOOR)
    for row in (3, 8, 13):
        d.line([x, row, x + 15, row], fill=FLOOR_D)

    # 10 counter - shop bench (solid)
    x = frame(d, 10, FLOOR)
    d.rectangle([x, 4, x + 15, 12], fill=WOOD)
    d.rectangle([x, 4, x + 15, 5], fill=WOOD_D)
    d.line([x, 12, x + 15, 12], fill=WOOD_D)

    # 11 chest - treasure (solid until opened)
    x = frame(d, 11, FLOOR)
    d.rectangle([x + 2, 6, x + 13, 13], fill=WOOD)
    d.pieslice([x + 2, 2, x + 13, 10], 180, 360, fill=WOOD_D)
    d.rectangle([x + 2, 8, x + 13, 9], fill=GOLD_D)
    d.rectangle([x + 7, 8, x + 8, 11], fill=GOLD)

    # 12 sign - board on a post (solid)
    x = frame(d, 12, GRASS)
    d.rectangle([x + 2, 3, x + 13, 9], fill=WOOD)
    d.rectangle([x + 2, 3, x + 13, 9], outline=WOOD_D)
    d.line([x + 4, 5, x + 11, 5], fill=WOOD_D)
    d.line([x + 4, 7, x + 9, 7], fill=WOOD_D)
    d.rectangle([x + 7, 10, x + 8, 15], fill=WOOD_D)

    # 13 bridge - planks over water
    x = frame(d, 13, WATER)
    d.rectangle([x, 3, x + 15, 12], fill=WOOD)
    for col in range(1, 16, 3):
        d.line([x + col, 3, x + col, 12], fill=WOOD_D)
    d.line([x, 3, x + 15, 3], fill=WOOD_D)
    d.line([x, 12, x + 15, 12], fill=WOOD_D)

    # 14 flowers - decorative grass
    x = frame(d, 14, GRASS)
    for px, py, colour in ((3, 4, WHITE), (10, 6, GOLD), (6, 11, WHITE), (12, 12, GOLD)):
        d.point((x + px, py), fill=colour)
        d.point((x + px + 1, py), fill=colour)
        d.point((x + px, py + 1), fill=colour)
        d.point((x + px + 1, py + 1), fill=colour)

    # 15 cave - dark mouth in rock (walkable; the level marks it a door)
    x = frame(d, 15, ROCK)
    d.polygon([(x + 1, 15), (x + 4, 4), (x + 11, 4), (x + 14, 15)], fill=ROCK_D)
    d.pieslice([x + 4, 5, x + 11, 18], 180, 360, fill=DARK)
    speckle(d, x, ROCK_L, [(2, 6), (13, 8)])

    # 16 bed head - pillow end, the top half of a two-tile bed (solid)
    x = frame(d, 16, FLOOR)
    d.rectangle([x + 2, 2, x + 13, 15], fill=WOOD)
    d.rectangle([x + 2, 2, x + 13, 3], fill=WOOD_D)
    d.rectangle([x + 3, 4, x + 12, 7], fill=WHITE)
    d.line([x + 3, 7, x + 12, 7], fill=FLOOR_D)
    d.rectangle([x + 3, 8, x + 12, 15], fill=ROOF)
    d.line([x + 3, 8, x + 12, 8], fill=ROOF_D)

    # 17 bed foot - blanket end, sits directly below a bed head (solid)
    x = frame(d, 17, FLOOR)
    d.rectangle([x + 2, 0, x + 13, 13], fill=WOOD)
    d.rectangle([x + 3, 0, x + 12, 11], fill=ROOF)
    d.line([x + 3, 5, x + 12, 5], fill=ROOF_D)
    d.rectangle([x + 2, 12, x + 13, 13], fill=WOOD_D)

    img.save(path)
    return path


def walker(d, x, y, shirt, shirt_dark, hair, pose, facing):
    """One 16x16 character pose. pose 0/1 swap which leg leads."""
    # head
    d.rectangle([x + 5, y + 1, x + 10, y + 6], fill=SKIN)
    d.rectangle([x + 5, y + 1, x + 10, y + 2], fill=hair)
    d.point((x + 4, y + 3), fill=hair)
    d.point((x + 11, y + 3), fill=hair)
    if facing == "down":
        d.point((x + 6, y + 4), fill=DARK)
        d.point((x + 9, y + 4), fill=DARK)
    elif facing == "left":
        d.point((x + 6, y + 4), fill=DARK)
        d.rectangle([x + 9, y + 1, x + 10, y + 5], fill=hair)
    elif facing == "right":
        d.point((x + 9, y + 4), fill=DARK)
        d.rectangle([x + 5, y + 1, x + 6, y + 5], fill=hair)
    else:  # up - back of the head
        d.rectangle([x + 5, y + 1, x + 10, y + 5], fill=hair)

    # body
    d.rectangle([x + 4, y + 7, x + 11, y + 12], fill=shirt)
    d.rectangle([x + 4, y + 7, x + 11, y + 7], fill=shirt_dark)
    # arms
    d.rectangle([x + 3, y + 8, x + 3, y + 10], fill=shirt_dark)
    d.rectangle([x + 12, y + 8, x + 12, y + 10], fill=shirt_dark)
    # legs, alternating with the pose
    left_leg = y + 13 if pose == 0 else y + 12
    right_leg = y + 12 if pose == 0 else y + 13
    d.rectangle([x + 5, left_leg, x + 6, y + 15], fill=TRUNK)
    d.rectangle([x + 9, right_leg, x + 10, y + 15], fill=TRUNK)


FACINGS = ("down", "up", "left", "right")


def build_character(path, shirt, shirt_dark, hair):
    img = Image.new("RGBA", (TILE * 8, TILE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    index = 0
    for facing in FACINGS:
        for pose in (0, 1):
            walker(d, index * TILE, 0, shirt, shirt_dark, hair, pose, facing)
            index += 1
    img.save(path)
    return path


def build_folk(path, palettes):
    """All townsfolk on one sheet: 8 frames each, in palette order."""
    img = Image.new("RGBA", (TILE * 8 * len(palettes), TILE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    index = 0
    for shirt, shirt_dark, hair in palettes:
        for facing in FACINGS:
            for pose in (0, 1):
                walker(d, index * TILE, 0, shirt, shirt_dark, hair, pose, facing)
                index += 1
    img.save(path)
    return path


here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, "..", "Majorsilence.Games.Rpg", "assets", "artwork", "rpg")
os.makedirs(out, exist_ok=True)

print(build_tileset(os.path.join(out, "tileset.png")))
print(build_character(os.path.join(out, "hero.png"), (64, 112, 208), (40, 72, 152), HAIR))
print(build_folk(os.path.join(out, "folk.png"), [
    ((192, 88, 72), (136, 56, 48), (56, 44, 40)),    # 0 villager, red
    ((120, 96, 176), (80, 60, 128), (200, 200, 192)),  # 1 elder, purple + white hair
    ((88, 160, 120), (56, 112, 84), (168, 120, 48)),   # 2 shopkeep, green
]))
