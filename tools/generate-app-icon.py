#!/usr/bin/env python3
"""Generates the Android launcher icon set (Resources/mipmap-*/appicon.png).

A simple ship-silhouette-against-an-iceberg mark, matching the game's dark
navy/steel/ice palette (see the other generated titanic-demo art). Drawn once
at 192px and downsampled per density - the shapes are simple enough that this
reads fine even at 48px.
"""

from PIL import Image, ImageDraw
import os

SIZES = {
    "mipmap-mdpi": 48,
    "mipmap-hdpi": 72,
    "mipmap-xhdpi": 96,
    "mipmap-xxhdpi": 144,
    "mipmap-xxxhdpi": 192,
}

NAVY = (16, 24, 38, 255)
NAVY_DARK = (10, 15, 26, 255)
HULL = (58, 66, 82, 255)
HULL_LIGHT = (90, 100, 118, 255)
ICE = (196, 214, 226, 255)
ICE_SHADOW = (150, 170, 186, 255)
GOLD = (255, 210, 60, 255)

BASE = 192


def draw_icon():
    img = Image.new("RGBA", (BASE, BASE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # rounded-square navy background (adaptive-icon-ish, safe for legacy launchers too)
    d.rounded_rectangle([0, 0, BASE - 1, BASE - 1], radius=28, fill=NAVY)

    # water line
    d.rectangle([0, 138, BASE, BASE], fill=NAVY_DARK)

    # iceberg (left)
    d.polygon([(18, 150), (48, 96), (66, 130), (58, 150)], fill=ICE)
    d.polygon([(48, 96), (66, 130), (58, 150), (52, 150), (52, 118)], fill=ICE_SHADOW)

    # ship hull (right of center), simple side-profile silhouette
    d.polygon([
        (72, 150), (72, 118), (86, 108), (150, 108), (162, 122), (162, 150)
    ], fill=HULL)
    d.rectangle([90, 96, 150, 110], fill=HULL_LIGHT)  # superstructure
    d.rectangle([100, 76, 112, 98], fill=HULL_LIGHT)  # funnel 1
    d.rectangle([124, 70, 136, 98], fill=HULL_LIGHT)  # funnel 2
    d.line([(72, 150), (162, 150)], fill=NAVY_DARK, width=2)

    # a single gold tix-coin accent, echoing the in-game pickup
    d.ellipse([146, 140, 166, 160], fill=GOLD, outline=NAVY_DARK)

    return img


icon = draw_icon()
dest_root = os.path.join(os.path.dirname(__file__), "..",
                          "Majorsilence.Games.Learning.Android", "Resources")

for folder, size in SIZES.items():
    out_dir = os.path.abspath(os.path.join(dest_root, folder))
    os.makedirs(out_dir, exist_ok=True)
    resized = icon.resize((size, size), Image.LANCZOS)
    out_path = os.path.join(out_dir, "appicon.png")
    resized.save(out_path)
    print("wrote", out_path)
