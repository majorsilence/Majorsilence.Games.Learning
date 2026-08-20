#!/usr/bin/env python3
"""
Import a Majorsilence.Games.Learning isometric level JSON into two Godot
scenes: a 2D parity room and a 3D extruded room, both built from the same
source data and the same real game art (no new art invented).

Replicates the C# engine's exact conventions (see godot-titanic/../docs or
the plan this was built from):
  - IsometricGrid.TileToWorld: x=(col-row)*(tileW/2), y=(col+row)*(tileH/2)
  - SpriteSheet: fixed-grid frame slicing, no padding
  - Room.cs:112 hardcodes isometric tile frames at 32x16 regardless of the
    level's declared tileWidth/tileHeight
  - LevelLoader.ResolveElevations: elevationPixels = heightDigit * elevationStep
  - Y-sort key is the tile's *unelevated* ground position; elevation only
    shifts the drawn sprite, never the sort key (StandOnTileElevated)

Usage:
    python3 tools/import_level.py <path-to-level.json> [--name crows-nest]

Only copies art into godot-titanic/assets/ that isn't already there; run
tools/copy_art.sh (or copy by hand) first if a new room needs art this
script doesn't already know about (see ENTITY_ART below).
"""
import argparse
import json
import sys
from functools import lru_cache
from pathlib import Path

from PIL import Image

PROJECT_ROOT = Path(__file__).resolve().parents[1]
ASSET_ROOT = PROJECT_ROOT / "assets"

ISO_FRAME_W = 32
ISO_FRAME_H = 16

# Real prop/NPC art this importer knows how to place, mirroring a subset of
# Game.cs's PropKinds/NpcKinds (ImagePath relative to the source repo's
# Majorsilence.Games.Learning/, single-frame width, height, and how many
# frames the sheet is divided into horizontally -- e.g. watcher.png is a
# 64x32 walk-cycle strip, 4 frames of 16x32 each). Only frame 0 is shown for
# these (static props/NPCs); extend this as more rooms reference more kinds.
ENTITY_ART = {
    ("npc", "watcher"): ("assets/artwork/titanic-demo/watcher.png", 16, 32, 4),
    ("npc", "captain"): ("assets/artwork/titanic-demo/captain.png", 16, 32, 4),
    ("npc", "engineer"): ("assets/artwork/titanic-demo/engineer.png", 16, 32, 4),
    ("tix", None): ("assets/artwork/titanic-demo/tix-coin.png", 16, 16, 1),
    ("door", None): ("assets/artwork/titanic-demo/doorway.png", 24, 36, 1),
    ("wheel", None): ("assets/artwork/titanic-demo/wheel.png", 24, 32, 1),
    ("boiler", None): ("assets/artwork/titanic-demo/boiler.png", 32, 40, 1),
    ("bed", None): ("assets/artwork/titanic-demo/bed.png", 32, 24, 1),
    ("table", None): ("assets/artwork/titanic-demo/table.png", 32, 24, 1),
    ("crate", None): ("assets/artwork/titanic-demo/crate.png", 24, 24, 1),
    ("funnel", None): ("assets/artwork/titanic-demo/funnel.png", 32, 88, 1),
    ("mast", None): ("assets/artwork/titanic-demo/mast.png", 16, 64, 1),
    ("iceberg", None): ("assets/artwork/titanic-demo/iceberg.png", 40, 36, 1),
    ("lifeboat", None): ("assets/artwork/titanic-demo/lifeboat.png", 32, 20, 1),
    ("shopCounter", None): ("assets/artwork/titanic-demo/shop-counter.png", 32, 28, 1),
}

# The player's walk-cycle strip -- mirrors Game.cs's
# `player1.SetAnimation(new Animation(frames: [0,1,2,3], frameDurationMs: 150))`.
PLAYER_ART = "assets/artwork/isometric-demo/character.png"
PLAYER_W, PLAYER_H, PLAYER_FRAMES = 16, 32, 4
PLAYER_FRAME_DURATION_MS = 150

WALL_HEIGHT_UNITS = 1.3  # ~1.3x the player's own height (see PIXEL_SIZE_3D) --
# a snug, human-scaled ceiling; 1.8 (tried previously) still read as too tall
FLOOR_THICKNESS_UNITS = 0.2
ELEVATION_UNIT_PER_PIXEL = 1.0 / 32.0  # 32px (one tile width) == 1 3D unit
PIXEL_SIZE_3D = 1.0 / 32.0  # world units per source pixel, 3D sprites

# Mood presets standing in for "1996 movie" art direction -- since we're not
# generating new art, the lever is lighting/color-grading/atmosphere per
# room, echoing the film's own palette choices: warm brass/candlelight in
# opulent spaces, cold industrial green in the engine room, a cold moonlit
# blue outdoors at night. Applied to the 3D room's WorldEnvironment/light
# fully, and as a gentler CanvasModulate tint in the 2D room.
MOODS = {
    "default": {
        "bg": (0.05, 0.05, 0.07), "ambient_color": (1, 1, 1), "ambient_energy": 0.5,
        "light_color": (1, 1, 1), "light_energy": 1.0,
    },
    "deck_night": {
        "bg": (0.02, 0.03, 0.07), "ambient_color": (0.35, 0.45, 0.65), "ambient_energy": 0.45,
        "light_color": (0.55, 0.65, 0.9), "light_energy": 0.55,
        "fog_color": (0.15, 0.2, 0.32), "fog_density": 0.02,
        "glow_intensity": 0.25, "glow_bloom": 0.05,
        # Procedural starry-sky background instead of a flat color -- see
        # STARFIELD_SKY_SHADER -- for the open-deck-at-night-in-the-North-
        # Atlantic look the 1996 film leans on so heavily.
        "stars": True, "sky_top": (0.01, 0.02, 0.05), "sky_horizon": (0.09, 0.12, 0.2),
    },
    "bridge": {
        "bg": (0.55, 0.65, 0.78), "ambient_color": (0.85, 0.92, 1.0), "ambient_energy": 0.75,
        "light_color": (0.97, 0.98, 1.0), "light_energy": 1.3,
    },
    "engine": {
        "bg": (0.02, 0.04, 0.03), "ambient_color": (0.5, 0.7, 0.55), "ambient_energy": 0.3,
        "light_color": (0.6, 0.85, 0.6), "light_energy": 0.75,
        "fog_color": (0.35, 0.5, 0.4), "fog_density": 0.045,
        "glow_intensity": 0.35, "glow_bloom": 0.1,
    },
    "first_class": {
        "bg": (0.09, 0.06, 0.04), "ambient_color": (1.0, 0.85, 0.65), "ambient_energy": 0.55,
        "light_color": (1.0, 0.78, 0.52), "light_energy": 1.05,
        "glow_intensity": 0.3, "glow_bloom": 0.05,
    },
    "grand": {
        "bg": (0.1, 0.07, 0.03), "ambient_color": (1.0, 0.88, 0.62), "ambient_energy": 0.65,
        "light_color": (1.0, 0.82, 0.5), "light_energy": 1.25,
        "glow_intensity": 0.45, "glow_bloom": 0.08,
    },
    "second_class": {
        # Respectable but modest -- warm-neutral, noticeably dimmer than
        # first class, no glow. Leans into the movie's class-contrast.
        "bg": (0.06, 0.06, 0.06), "ambient_color": (0.85, 0.8, 0.75), "ambient_energy": 0.45,
        "light_color": (0.85, 0.78, 0.68), "light_energy": 0.9,
    },
    "third_class": {
        # Steerage: starkest, coldest, dimmest -- bare-bulb utility
        # lighting, no warmth or glow at all.
        "bg": (0.03, 0.03, 0.035), "ambient_color": (0.55, 0.55, 0.58), "ambient_energy": 0.35,
        "light_color": (0.6, 0.6, 0.65), "light_energy": 0.65,
    },
}
ROOM_MOODS = {
    "crows-nest": "deck_night",
    "titanic": "deck_night",
    "boat-deck-split": "deck_night",
    "bridge": "bridge",
    "engine-room": "engine",
    "first-class-quarters": "first_class",
    "grand-stair-escape": "grand",
    "a-deck-corridor": "first_class",
    "second-class-quarters": "second_class",
    "third-class-berths": "third_class",
    "pursers-office": "second_class",
}

# Bespoke exterior/architecture models (built in Blender, see
# godot-titanic/assets/models/ and the build_*.py scripts that made them --
# not regenerated by this importer) attached to specific rooms' 3D scenes,
# positioned to underlie/surround the room's own data-driven geometry
# rather than replace it (gameplay -- doors, tix, collision -- still comes
# from the tile data as normal).
EXTERIOR_MODELS = {
    "titanic": "res://assets/models/titanic_hull.glb",
    "boat-deck-split": "res://assets/models/titanic_hull.glb",
}

# Bespoke per-room wall-window overrides: a run of wall cells in a specific
# row/col gets a transparent "glass" material instead of the tile's normal
# flat wall color, for rooms whose real-world layout calls for windows the
# tile data itself has no concept of (the bridge's forward-facing windows
# over the bow, in the movie's iconic shots). (axis, index, span) --
# axis "row" fixes r == index and varies c across span; "col" is the mirror.
ROOM_WINDOW_WALLS = {
    "bridge": ("row", 0, range(1, 9)),
}

# Inline shader sources (embedded as Shader sub_resources, no external
# .gdshader files needed) for the two atmosphere upgrades that a flat
# WorldEnvironment color/CSGBox3D can't give us: a starry night sky for
# "deck_night"-mood rooms, and a real animated ocean for the fallback
# "open water" plane those same rooms float in. Both are fully procedural
# (hash-noise stars, sine-wave displacement) -- no textures or external
# assets, in keeping with the no-paid-assets constraint.
STARFIELD_SKY_SHADER = """shader_type sky;

uniform vec3 sky_top : source_color = vec3(0.01, 0.02, 0.05);
uniform vec3 sky_horizon : source_color = vec3(0.09, 0.12, 0.2);
uniform float star_density = 260.0;
uniform vec3 moon_dir = vec3(0.35, 0.6, -0.65);
uniform vec3 moon_color : source_color = vec3(0.85, 0.88, 0.82);

float hash13(vec3 p) {
    p = fract(p * 0.3183099 + 0.1);
    p *= 17.0;
    return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

void sky() {
    float t = clamp(EYEDIR.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 col = mix(sky_horizon, sky_top, pow(t, 0.6));
    vec3 cell = floor(EYEDIR * star_density);
    float r = hash13(cell);
    float star = smoothstep(0.986, 1.0, r) * step(0.02, EYEDIR.y);
    float tw = 0.7 + 0.3 * hash13(cell + 11.0);
    col += vec3(star * tw);

    // A soft pale moon disc plus a faint glow halo -- gives the
    // DirectionalLight3D "moonlight" a visible source in the sky instead
    // of light coming from nowhere.
    float md = dot(EYEDIR, normalize(moon_dir));
    float disc = smoothstep(0.9985, 0.9993, md);
    float halo = smoothstep(0.95, 0.999, md) * 0.15;
    col += moon_color * (disc + halo);

    COLOR = col;
}
"""

OCEAN_WATER_SHADER = """shader_type spatial;
render_mode blend_mix, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec3 deep_color : source_color = vec3(0.01, 0.035, 0.06);
uniform vec3 crest_color : source_color = vec3(0.05, 0.11, 0.16);
uniform float wave_height = 0.06;
uniform float wave_speed = 0.5;

varying float wave_h;

void vertex() {
    float t = TIME * wave_speed;
    float h = sin(VERTEX.x * 0.5 + t) * 0.6 + sin(VERTEX.z * 0.8 - t * 1.4) * 0.4;
    VERTEX.y += h * wave_height;
    wave_h = h;
}

void fragment() {
    ALBEDO = mix(deep_color, crest_color, clamp(wave_h * 0.5 + 0.5, 0.0, 1.0));
    ROUGHNESS = 0.12;
    METALLIC = 0.0;
    SPECULAR = 0.9;
}
"""

# Entity types that get a real 3D model (built in tools/blender/) instead
# of the generic billboarded Sprite3D in 3D rooms. (rotation_degrees) turns
# the model to face the direction that reads right for that kind of prop --
# the lifeboat model's long axis is local X, but real lifeboats lie along
# the ship's length (world Z), hence the 90 degree yaw.
ENTITY_MODELS_3D = {
    "lifeboat": ("res://assets/models/lifeboat.glb", (0.0, 90.0, 0.0)),
    "funnel": ("res://assets/models/funnel.glb", (0.0, 0.0, 0.0)),
    "mast": ("res://assets/models/mast.glb", (0.0, 0.0, 0.0)),
    "iceberg": ("res://assets/models/iceberg.glb", (0.0, 0.0, 0.0)),
    "wheel": ("res://assets/models/wheel.glb", (0.0, 0.0, 0.0)),
    "boiler": ("res://assets/models/boiler.glb", (0.0, 0.0, 0.0)),
}


def room_mood(room_name: str) -> dict:
    return MOODS[ROOM_MOODS.get(room_name, "default")]


def load_level(path: Path) -> dict:
    data = json.loads(path.read_text())
    rows = data["tiles"]
    cols = len(rows[0])
    if "heights" in data and data["heights"]:
        heights = data["heights"]
    else:
        heights = ["0" * cols for _ in rows]
    data["_heights"] = heights
    data["_rows"] = len(rows)
    data["_cols"] = cols
    return data


def tile_type(level: dict, ch: str) -> str:
    return level["legend"][ch]


def elevation_pixels(level: dict, r: int, c: int) -> int:
    digit = level["_heights"][r][c]
    step = level.get("elevationStep", 16)
    return (int(digit) - int("0")) * step if digit != "0" else 0


def floor_top_units(level: dict, r: int, c: int) -> float:
    """World-space Y (3D) of the walkable surface at a cell: elevation plus
    the floor slab's own thickness, i.e. where feet belong."""
    return elevation_pixels(level, r, c) * ELEVATION_UNIT_PER_PIXEL + FLOOR_THICKNESS_UNITS


@lru_cache(maxsize=None)
def _tileset_image(tileset_res_suffix: str) -> Image.Image:
    return Image.open(ASSET_ROOT / tileset_res_suffix).convert("RGBA")


def frame_average_color(tileset_res_suffix: str, idx: int) -> tuple[float, float, float]:
    """Average RGB (0..1) of a frame's non-transparent pixels. The 2D iso
    tile art (a small diamond on a mostly-transparent 32x16 canvas) tiles
    badly when stretched across a tall 3D wall face -- so wall cells use
    this as a flat albedo_color instead of the texture itself."""
    im = _tileset_image(tileset_res_suffix)
    crop = im.crop((idx * ISO_FRAME_W, 0, idx * ISO_FRAME_W + ISO_FRAME_W, ISO_FRAME_H))
    pixels = [p for p in crop.getdata() if p[3] > 0]
    if not pixels:
        return (0.5, 0.5, 0.5)
    n = len(pixels)
    return (
        sum(p[0] for p in pixels) / n / 255.0,
        sum(p[1] for p in pixels) / n / 255.0,
        sum(p[2] for p in pixels) / n / 255.0,
    )


def tile_to_world(level: dict, col: int, row: int):
    tw = level["tileWidth"]
    th = level["tileHeight"]
    return (col - row) * (tw / 2.0), (col + row) * (th / 2.0)


def find_spawn(level: dict) -> tuple[int, int]:
    for ent in level.get("entities", []):
        if ent["type"] in ("spawnPoint", "playerStart"):
            return ent["column"], ent["row"]
    return level["_cols"] // 2, level["_rows"] // 2


def source_json_to_scene_res(target_json: str) -> str:
    """Best-effort mapping from a source level JSON path (as referenced in a
    door's `target` property) to where its ported Godot scene would live,
    once ported. The room may not exist yet -- callers must check
    ResourceLoader.exists() at runtime."""
    stem = Path(target_json).stem
    return f"res://scenes/rooms_2d/{stem}.tscn"


def gd_str(s: str) -> str:
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def add_sub_once(sub_resources: list, type_: str, id_: str, body: str) -> str:
    """Append a `[sub_resource]` block by id, skipping if already present
    (mirrors the `if not any(f'id="{x}"' in e for e in ...)` guard used
    all over this file for ext_resources, factored out for the fixed set
    of shared sub-resources -- box meshes/materials -- reused by every
    room, so they're only ever emitted once per generated scene)."""
    if not any(f'id="{id_}"' in s for s in sub_resources):
        sub_resources.append(f'[sub_resource type="{type_}" id="{id_}"]\n{body}')
    return id_


def build_2d_scene(level: dict, room_name: str) -> str:
    ext_resources = []
    sub_resources = []
    nodes = []
    connections = []
    load_steps = 1

    def add_ext(id_, type_, path_):
        nonlocal load_steps
        ext_resources.append(f'[ext_resource type="{type_}" path="{path_}" id="{id_}"]')
        load_steps += 1
        return id_

    tileset_res_suffix = level["tilesetPath"].split("assets/", 1)[-1]
    tileset_res_path = "res://assets/" + tileset_res_suffix
    tileset_ext_id = add_ext("Tileset", "Texture2D", tileset_res_path)

    frame_atlas_ids = {}
    for name, idx in level["tileFrames"].items():
        used = any(
            tile_type(level, ch) == name
            for row in level["tiles"] for ch in row
        )
        if not used:
            continue
        aid = f"AtlasTexture_{name}"
        sub_resources.append(
            f'[sub_resource type="AtlasTexture" id="{aid}"]\n'
            f'atlas = ExtResource("{tileset_ext_id}")\n'
            f'region = Rect2({idx * ISO_FRAME_W}, 0, {ISO_FRAME_W}, {ISO_FRAME_H})'
        )
        load_steps += 1
        frame_atlas_ids[name] = aid

    nodes.append(f'[node name="{room_name}" type="Node2D"]\ny_sort_enabled = true')
    # Softened version of the 3D room's mood lighting: full light_color would
    # crush the pixel art's own colors, so this blends halfway to white.
    mlc = room_mood(room_name)["light_color"]
    tint = tuple((1.0 + c) / 2.0 for c in mlc)
    nodes.append(
        f'[node name="Mood" type="CanvasModulate" parent="."]\n'
        f'color = Color({tint[0]:.4f}, {tint[1]:.4f}, {tint[2]:.4f}, 1)'
    )

    solid_types = set(level.get("solid", []))
    tile_i = 0
    for r, row in enumerate(level["tiles"]):
        for c, ch in enumerate(row):
            t = tile_type(level, ch)
            if t not in frame_atlas_ids:
                continue
            wx, wy = tile_to_world(level, c, r)
            elev = elevation_pixels(level, r, c)
            half_h_step = level["tileHeight"] / 2.0
            steps = int(elev / half_h_step) if half_h_step else 0
            for k in range(steps + 1):
                off_y = -ISO_FRAME_H - (elev - k * half_h_step)
                node_name = f"Tile_r{r}_c{c}_k{k}"
                nodes.append(
                    f'[node name="{node_name}" type="Sprite2D" parent="."]\n'
                    f'position = Vector2({wx}, {wy})\n'
                    f'texture = SubResource("{frame_atlas_ids[t]}")\n'
                    f'centered = false\n'
                    f'offset = Vector2({-ISO_FRAME_W / 2.0}, {off_y})'
                )
                tile_i += 1

            if t in solid_types:
                # A diamond matching the iso projection's own basis
                # (TILE_W/2, TILE_H/2) tessellates edge-to-edge with its
                # neighbors -- a plain rectangle here would leave gaps at
                # the tile corners for the player to slip through.
                hw, hh = level["tileWidth"] / 2.0, level["tileHeight"] / 2.0
                body_name = f"Wall_r{r}_c{c}"
                nodes.append(f'[node name="{body_name}" type="StaticBody2D" parent="."]\nposition = Vector2({wx}, {wy})')
                nodes.append(
                    f'[node name="Collision" type="CollisionPolygon2D" parent="{body_name}"]\n'
                    f'polygon = PackedVector2Array(0, {-hh}, {hw}, 0, 0, {hh}, {-hw}, 0)'
                )

    fallback_type = level.get("fallbackTileType")
    if fallback_type:
        # LevelLoader honors an explicit fallbackTileType (e.g. the outdoor
        # deck's "water") by NOT treating out-of-bounds as solid -- the ship
        # floats in open water, it isn't walled off at the hull. A big flat
        # rect standing in for that water (rather than the undrawn gray
        # background) at least reads as "there's more world beyond the
        # tiles" even though it isn't literally tiled out to worldMaxColumn.
        if fallback_type in level["tileFrames"]:
            r, g, b = frame_average_color(tileset_res_suffix, level["tileFrames"][fallback_type])
        else:
            r, g, b = (0.2, 0.3, 0.4)
        margin = 20
        cols, rows = level["_cols"], level["_rows"]
        fx0, fy0 = tile_to_world(level, -margin, rows + margin)
        fx1, fy1 = tile_to_world(level, cols + margin, -margin)
        nodes.append(
            '[node name="FallbackBackground" type="ColorRect" parent="."]\n'
            f'offset_left = {min(fx0, fx1)}\noffset_top = {min(fy0, fy1)}\n'
            f'offset_right = {max(fx0, fx1)}\noffset_bottom = {max(fy0, fy1)}\n'
            f'color = Color({r:.4f}, {g:.4f}, {b:.4f}, 1)\n'
            'z_index = -10'
        )
    else:
        # "Out of bounds is solid" boundary (see the North/South/East/West
        # StaticBody3D in the 3D room): any walkable cell adjacent to a grid
        # cell that isn't defined at all -- e.g. a doorway floor tile at the
        # map's edge, with nothing beyond it -- gets an invisible diamond
        # plugging that gap, so the player can't wander off the room's
        # defined tiles into the undrawn background. Reuses the exact same
        # diamond shape as a real wall tile, just with no visual.
        hw, hh = level["tileWidth"] / 2.0, level["tileHeight"] / 2.0
        boundary_seen = set()
        for r, row in enumerate(level["tiles"]):
            for c, ch in enumerate(row):
                if tile_type(level, ch) in solid_types:
                    continue
                for nr, nc in ((r - 1, c), (r + 1, c), (r, c - 1), (r, c + 1)):
                    if 0 <= nr < level["_rows"] and 0 <= nc < level["_cols"]:
                        continue
                    if (nr, nc) in boundary_seen:
                        continue
                    boundary_seen.add((nr, nc))
                    bwx, bwy = tile_to_world(level, nc, nr)
                    body_name = f"Boundary_r{nr}_c{nc}"
                    nodes.append(f'[node name="{body_name}" type="StaticBody2D" parent="."]\nposition = Vector2({bwx}, {bwy})')
                    nodes.append(
                        f'[node name="Collision" type="CollisionPolygon2D" parent="{body_name}"]\n'
                        f'polygon = PackedVector2Array(0, {-hh}, {hw}, 0, 0, {hh}, {-hw}, 0)'
                    )

    door_script_id = None
    generic_prop_i = 0
    for ent in level.get("entities", []):
        etype = ent["type"]
        role = ent.get("properties", {}).get("role")
        col, row = ent["column"], ent["row"]
        wx, wy = tile_to_world(level, col, row)
        elev = elevation_pixels(level, row, col)

        art_key = (etype, role) if (etype, role) in ENTITY_ART else (etype, None)
        if etype in ("spawnPoint", "playerStart", "shop"):
            continue
        if art_key not in ENTITY_ART:
            generic_prop_i += 1
            print(f"WARN: no art mapped for entity type={etype!r} role={role!r}, skipping visual", file=sys.stderr)
            continue

        img_path, w, h, frames = ENTITY_ART[art_key]
        res_path = "res://assets/" + img_path.split("assets/", 1)[-1]
        ext_id = f"Tex_{etype}_{role or 'default'}"
        if not any(ext_id in e for e in ext_resources):
            add_ext(ext_id, "Texture2D", res_path)
        # Static entities always show frame 0 -- multi-frame art (e.g. a
        # walk-cycle strip) must still be cropped to one frame, or the whole
        # sheet renders as a squashed filmstrip.
        if frames > 1:
            tex_ref_id = f"AtlasTexture_{etype}_{role or 'default'}"
            if not any(f'id="{tex_ref_id}"' in s for s in sub_resources):
                sub_resources.append(
                    f'[sub_resource type="AtlasTexture" id="{tex_ref_id}"]\n'
                    f'atlas = ExtResource("{ext_id}")\n'
                    f'region = Rect2(0, 0, {w}, {h})'
                )
                load_steps += 1
            tex_expr = f'SubResource("{tex_ref_id}")'
        else:
            tex_expr = f'ExtResource("{ext_id}")'

        node_name = f"Entity_{etype}_{role or ''}_r{row}_c{col}".rstrip("_")
        extra = ""
        if etype == "door":
            if door_script_id is None:
                door_script_id = add_ext("DoorScript", "Script", "res://scripts/Door2D.gd")
            target = source_json_to_scene_res(ent["properties"].get("target", ""))
            extra = f'script = ExtResource("{door_script_id}")\ntarget_scene = {gd_str(target)}\n'
            node_type = "Area2D"
        else:
            node_type = "Node2D"

        nodes.append((
            f'[node name="{node_name}" type="{node_type}" parent="."]\n'
            f'position = Vector2({wx}, {wy})\n'
            + extra
        ).rstrip())
        # z_index=1 (matching Game.cs, which sets ZIndex=1 on every prop/
        # NPC/door/player sprite) puts entities in a stacking band strictly
        # above tiles (left at the default z_index=0): Godot draws z_index
        # bands in order and only y-sorts *within* a band, so an entity can
        # never tie against -- and flicker against -- the ground tile under
        # its own feet, no matter how its Y crosses tile boundaries while
        # moving. Entities still y-sort normally against each other.
        nodes.append(
            f'[node name="Visual" type="Sprite2D" parent="{node_name}"]\n'
            f'z_index = 1\n'
            f'texture = {tex_expr}\n'
            f'centered = false\n'
            f'offset = Vector2({-w / 2.0}, {-h - elev})'
        )
        if etype == "door":
            if not any("RectangleShape2D_door" in s for s in sub_resources):
                sub_resources.insert(0, '[sub_resource type="RectangleShape2D" id="RectangleShape2D_door"]\nsize = Vector2(24, 16)')
                load_steps += 1
            nodes.append(
                f'[node name="Collision" type="CollisionShape2D" parent="{node_name}"]\n'
                f'shape = SubResource("RectangleShape2D_door")'
            )
            connections.append(f'[connection signal="body_entered" from="{node_name}" to="{node_name}" method="_on_body_entered"]')

    spawn_col, spawn_row = find_spawn(level)
    spawn_x, spawn_y = tile_to_world(level, spawn_col, spawn_row)
    add_ext("Tex_player", "Texture2D", "res://" + PLAYER_ART)
    add_ext("PlayerScript", "Script", "res://scripts/Player2D.gd")

    frame_ids = []
    for f in range(PLAYER_FRAMES):
        fid = f"AtlasTexture_player_f{f}"
        sub_resources.append(
            f'[sub_resource type="AtlasTexture" id="{fid}"]\n'
            f'atlas = ExtResource("Tex_player")\n'
            f'region = Rect2({f * PLAYER_W}, 0, {PLAYER_W}, {PLAYER_H})'
        )
        load_steps += 1
        frame_ids.append(fid)
    frame_entries = ", ".join(f'{{\n"duration": 1.0,\n"texture": SubResource("{fid}")\n}}' for fid in frame_ids)
    fps = 1000.0 / PLAYER_FRAME_DURATION_MS
    sub_resources.append(
        '[sub_resource type="SpriteFrames" id="SpriteFrames_player"]\n'
        f'animations = [{{\n"frames": [{frame_entries}],\n"loop": true,\n"name": &"walk",\n"speed": {fps}\n}}]'
    )
    load_steps += 1
    sub_resources.append(
        '[sub_resource type="RectangleShape2D" id="RectangleShape2D_player"]\n'
        'size = Vector2(12, 12)'
    )
    load_steps += 1
    nodes.append(
        f'[node name="Player" type="CharacterBody2D" parent="."]\n'
        f'position = Vector2({spawn_x}, {spawn_y})\n'
        f'script = ExtResource("PlayerScript")'
    )
    nodes.append(
        # z_index=1 (matching Game.cs's PlayerBaseSortOffsetY approach, see
        # the entity Visual nodes above) keeps the player strictly above
        # ground tiles in draw order, regardless of y-sort ties/crossings
        # while walking.
        '[node name="Sprite" type="AnimatedSprite2D" parent="Player"]\n'
        'z_index = 1\n'
        'sprite_frames = SubResource("SpriteFrames_player")\n'
        'animation = &"walk"\n'
        'centered = false\n'
        'offset = Vector2(-8, -32)'
    )
    nodes.append(
        '[node name="Collision" type="CollisionShape2D" parent="Player"]\n'
        'shape = SubResource("RectangleShape2D_player")'
    )
    nodes.append(
        '[node name="Camera2D" type="Camera2D" parent="Player"]\n'
        'zoom = Vector2(3, 3)'
    )

    header = f"[gd_scene load_steps={load_steps} format=3]\n"
    body = "\n\n".join(ext_resources + sub_resources + nodes + connections)
    return header + "\n" + body + "\n"


def build_3d_scene(level: dict, room_name: str) -> str:
    ext_resources = []
    sub_resources = []
    nodes = []
    connections = []
    load_steps = 1

    def add_ext(id_, type_, path_):
        nonlocal load_steps
        ext_resources.append(f'[ext_resource type="{type_}" path="{path_}" id="{id_}"]')
        load_steps += 1
        return id_

    tileset_res_suffix = level["tilesetPath"].split("assets/", 1)[-1]
    tileset_res_path = "res://assets/" + tileset_res_suffix
    tileset_ext_id = add_ext("Tileset", "Texture2D", tileset_res_path)
    solid_types = set(level.get("solid", []))

    frame_mat_ids = {}
    for name, idx in level["tileFrames"].items():
        used = any(tile_type(level, ch) == name for row in level["tiles"] for ch in row)
        if not used:
            continue
        mid = f"StandardMaterial3D_{name}"
        if name in solid_types:
            # The 2D iso tile art is a small diamond on a mostly-transparent
            # 32x16 canvas, meant to be viewed flat from above -- stretched
            # across a tall vertical wall face it tiles into an illegible
            # chevron mess. Flat-color (sampled from the real art) reads as
            # a clean solid wall instead; real wall side-textures are a
            # later refinement (see plan Roadmap).
            r, g, b = frame_average_color(tileset_res_suffix, idx)
            # roughness < the StandardMaterial3D default (1.0, fully matte)
            # so walls catch a soft specular highlight off the
            # DirectionalLight3D instead of reading as flat construction-
            # paper cutouts -- a small, broad-impact realism nudge that
            # costs nothing since it's just material params, not geometry.
            sub_resources.append(
                f'[sub_resource type="StandardMaterial3D" id="{mid}"]\n'
                f'albedo_color = Color({r:.4f}, {g:.4f}, {b:.4f}, 1)\n'
                'roughness = 0.75\n'
                'metallic_specular = 0.35'
            )
        else:
            aid = f"AtlasTexture_{name}"
            sub_resources.append(
                f'[sub_resource type="AtlasTexture" id="{aid}"]\n'
                f'atlas = ExtResource("{tileset_ext_id}")\n'
                f'region = Rect2({idx * ISO_FRAME_W}, 0, {ISO_FRAME_W}, {ISO_FRAME_H})'
            )
            load_steps += 1
            sub_resources.append(
                f'[sub_resource type="StandardMaterial3D" id="{mid}"]\n'
                f'albedo_texture = SubResource("{aid}")\n'
                f'texture_filter = 0'
            )
        load_steps += 1
        frame_mat_ids[name] = mid

    mood = room_mood(room_name)
    ac, ae = mood["ambient_color"], mood["ambient_energy"]
    lc, le = mood["light_color"], mood["light_energy"]
    bg = mood["bg"]
    env_lines = [
        # Flat ambient fill so wall faces angled away from the one
        # DirectionalLight3D aren't fully unlit (near-black) -- a single
        # directional light with no ambient leaves anything not facing it
        # completely dark, which read as broken/undersaturated geometry.
        '[sub_resource type="Environment" id="Environment_room"]',
    ]
    if mood.get("stars"):
        sky_shader_id = "Shader_starfield"
        sub_resources.append(f'[sub_resource type="Shader" id="{sky_shader_id}"]\ncode = {gd_str(STARFIELD_SKY_SHADER)}')
        load_steps += 1
        st, sh = mood["sky_top"], mood["sky_horizon"]
        sky_mat_id = "ShaderMaterial_starfield"
        sub_resources.append(
            f'[sub_resource type="ShaderMaterial" id="{sky_mat_id}"]\n'
            f'shader = SubResource("{sky_shader_id}")\n'
            f'shader_parameter/sky_top = Color({st[0]}, {st[1]}, {st[2]}, 1)\n'
            f'shader_parameter/sky_horizon = Color({sh[0]}, {sh[1]}, {sh[2]}, 1)'
        )
        load_steps += 1
        sky_id = "Sky_starfield"
        sub_resources.append(f'[sub_resource type="Sky" id="{sky_id}"]\nsky_material = SubResource("{sky_mat_id}")')
        load_steps += 1
        env_lines += [
            'background_mode = 2',
            f'sky = SubResource("{sky_id}")',
        ]
    else:
        env_lines += [
            'background_mode = 1',
            f'background_color = Color({bg[0]}, {bg[1]}, {bg[2]}, 1)',
        ]
    env_lines += [
        'ambient_light_source = 2',
        f'ambient_light_color = Color({ac[0]}, {ac[1]}, {ac[2]}, 1)',
        f'ambient_light_energy = {ae}',
    ]
    if "fog_color" in mood:
        fc = mood["fog_color"]
        env_lines += [
            'fog_enabled = true',
            f'fog_light_color = Color({fc[0]}, {fc[1]}, {fc[2]}, 1)',
            f'fog_density = {mood["fog_density"]}',
        ]
    if "glow_intensity" in mood:
        env_lines += [
            'glow_enabled = true',
            f'glow_intensity = {mood["glow_intensity"]}',
            f'glow_bloom = {mood.get("glow_bloom", 0.0)}',
        ]
    sub_resources.append("\n".join(env_lines))
    load_steps += 1
    nodes.append(f'[node name="{room_name}" type="Node3D"]')
    nodes.append(
        '[node name="WorldEnvironment" type="WorldEnvironment" parent="."]\n'
        'environment = SubResource("Environment_room")'
    )
    nodes.append(
        '[node name="DirectionalLight3D" type="DirectionalLight3D" parent="."]\n'
        'position = Vector3(0, 8, 0)\n'
        'rotation_degrees = Vector3(-50, -30, 0)\n'
        f'light_color = Color({lc[0]}, {lc[1]}, {lc[2]}, 1)\n'
        f'light_energy = {le}\n'
        'shadow_enabled = true'
    )

    window_cells = set()
    if room_name in ROOM_WINDOW_WALLS:
        axis, index, span = ROOM_WINDOW_WALLS[room_name]
        glass_id = "StandardMaterial3D_glass"
        sub_resources.append(
            f'[sub_resource type="StandardMaterial3D" id="{glass_id}"]\n'
            'albedo_color = Color(0.55, 0.72, 0.85, 0.35)\n'
            'roughness = 0.05\nmetallic_specular = 0.9\n'
            'transparency = 1\ncull_mode = 2'
        )
        load_steps += 1
        for i in span:
            window_cells.add((index, i) if axis == "row" else (i, index))

    for r, row in enumerate(level["tiles"]):
        for c, ch in enumerate(row):
            t = tile_type(level, ch)
            if t not in frame_mat_ids:
                continue
            elev_units = elevation_pixels(level, r, c) * ELEVATION_UNIT_PER_PIXEL
            is_wall = t in solid_types
            height = WALL_HEIGHT_UNITS if is_wall else FLOOR_THICKNESS_UNITS
            top_y = elev_units + height
            center_y = top_y - height / 2.0
            node_name = f"Cell_r{r}_c{c}"
            cell_mat = glass_id if (is_wall and (r, c) in window_cells) else frame_mat_ids[t]
            nodes.append(
                f'[node name="{node_name}" type="CSGBox3D" parent="."]\n'
                f'position = Vector3({float(c)}, {center_y}, {float(r)})\n'
                f'size = Vector3(1, {height}, 1)\n'
                f'use_collision = true\n'
                f'material = SubResource("{cell_mat}")'
            )

    cols, rows = level["_cols"], level["_rows"]
    fallback_type = level.get("fallbackTileType")
    if fallback_type:
        # See the 2D FallbackBackground: a level with an explicit fallback
        # (the outdoor deck's "water") isn't walled off at its edges -- it
        # floats in open water instead. Real "water" gets an animated ocean
        # (see OCEAN_WATER_SHADER) instead of the flat-stretched iso tile
        # texture, which reads as a static painted patch rather than the
        # open North Atlantic; any other fallback type keeps the older flat
        # plane treatment.
        margin = 20
        plane_w, plane_d = cols + margin * 2, rows + margin * 2
        plane_y = -0.4
        if fallback_type == "water":
            ocean_shader_id = "Shader_ocean"
            sub_resources.append(f'[sub_resource type="Shader" id="{ocean_shader_id}"]\ncode = {gd_str(OCEAN_WATER_SHADER)}')
            load_steps += 1
            ocean_mat_id = "ShaderMaterial_ocean"
            sub_resources.append(f'[sub_resource type="ShaderMaterial" id="{ocean_mat_id}"]\nshader = SubResource("{ocean_shader_id}")')
            load_steps += 1
            ocean_mesh_id = "PlaneMesh_ocean"
            sub_resources.append(
                f'[sub_resource type="PlaneMesh" id="{ocean_mesh_id}"]\n'
                f'size = Vector2({plane_w}, {plane_d})\n'
                'subdivide_width = 60\nsubdivide_depth = 60\n'
                f'material = SubResource("{ocean_mat_id}")'
            )
            load_steps += 1
            nodes.append(
                f'[node name="FallbackBackground" type="MeshInstance3D" parent="."]\n'
                f'position = Vector3({(cols - 1) / 2.0}, {plane_y}, {(rows - 1) / 2.0})\n'
                f'mesh = SubResource("{ocean_mesh_id}")'
            )
        else:
            fmat = frame_mat_ids.get(fallback_type)
            nodes.append(
                f'[node name="FallbackBackground" type="CSGBox3D" parent="."]\n'
                f'position = Vector3({(cols - 1) / 2.0}, -0.5, {(rows - 1) / 2.0})\n'
                f'size = Vector3({plane_w}, 0.2, {plane_d})'
                + (f'\nmaterial = SubResource("{fmat}")' if fmat else "")
            )
    else:
        # Invisible boundary walls around the whole tile grid's bounding
        # box. Room.cs treats anything outside the defined tiles array as
        # solid (see IsSolid: "out of bounds ... and no virtual-world
        # fallback"); this replicates that so walking through an unwalled
        # gap (e.g. a floor cell at the grid edge with no neighbor, like an
        # unported door) can't send the player off the built geometry into
        # an endless fall.
        boundary_height = WALL_HEIGHT_UNITS + 1.0
        bx, by, bz = (cols - 1) / 2.0, boundary_height / 2.0, (rows - 1) / 2.0
        boundary_id = 0
        for name, size, pos in [
            ("North", (cols, boundary_height, 0.2), (bx, by, -0.5)),
            ("South", (cols, boundary_height, 0.2), (bx, by, rows - 0.5)),
            ("West", (0.2, boundary_height, rows), (-0.5, by, bz)),
            ("East", (0.2, boundary_height, rows), (cols - 0.5, by, bz)),
        ]:
            boundary_id += 1
            sid = f"BoxShape3D_boundary{boundary_id}"
            sub_resources.append(f'[sub_resource type="BoxShape3D" id="{sid}"]\nsize = Vector3{size}')
            load_steps += 1
            nodes.append(
                f'[node name="Boundary{name}" type="StaticBody3D" parent="."]\n'
                f'position = Vector3{pos}'
            )
            nodes.append(
                f'[node name="Shape" type="CollisionShape3D" parent="Boundary{name}"]\n'
                f'shape = SubResource("{sid}")'
            )

    door_script_id = None
    for ent in level.get("entities", []):
        etype = ent["type"]
        role = ent.get("properties", {}).get("role")
        col, row = ent["column"], ent["row"]
        ground_y = floor_top_units(level, row, col)

        if etype in ("spawnPoint", "playerStart", "shop"):
            continue

        if etype in ENTITY_MODELS_3D:
            model_path, rot = ENTITY_MODELS_3D[etype]
            model_id = f"Model_{etype}"
            if not any(f'id="{model_id}"' in e for e in ext_resources):
                add_ext(model_id, "PackedScene", model_path)
            node_name = f"Entity_{etype}_r{row}_c{col}"
            nodes.append(
                f'[node name="{node_name}" parent="." instance=ExtResource("{model_id}")]\n'
                f'position = Vector3({float(col)}, {ground_y}, {float(row)})\n'
                f'rotation_degrees = Vector3({rot[0]}, {rot[1]}, {rot[2]})'
            )
            continue

        art_key = (etype, role) if (etype, role) in ENTITY_ART else (etype, None)
        if art_key not in ENTITY_ART:
            print(f"WARN: no art mapped for entity type={etype!r} role={role!r}, skipping visual", file=sys.stderr)
            continue

        img_path, w, h, frames = ENTITY_ART[art_key]
        res_path = "res://assets/" + img_path.split("assets/", 1)[-1]
        ext_id = f"Tex_{etype}_{role or 'default'}"
        if not any(ext_id in e for e in ext_resources):
            add_ext(ext_id, "Texture2D", res_path)

        node_name = f"Entity_{etype}_{role or ''}_r{row}_c{col}".rstrip("_")
        node_type = "Area3D" if etype == "door" else "Node3D"
        extra = ""
        if etype == "door":
            if door_script_id is None:
                door_script_id = add_ext("DoorScript", "Script", "res://scripts/Door3D.gd")
            target = source_json_to_scene_res(ent["properties"].get("target", "")).replace("rooms_2d", "rooms_3d")
            extra = f'script = ExtResource("{door_script_id}")\ntarget_scene = {gd_str(target)}\n'

        nodes.append(
            (
                f'[node name="{node_name}" type="{node_type}" parent="."]\n'
                f'position = Vector3({float(col)}, {ground_y}, {float(row)})\n'
                + extra
            ).rstrip()
        )
        # Sprite3D natively handles frame-sheet slicing (hframes/frame) and
        # feet-anchoring (centered=false + pixel offset), so entities don't
        # need the manual QuadMesh/UV setup the player used to need either.
        nodes.append(
            f'[node name="Sprite" type="Sprite3D" parent="{node_name}"]\n'
            f'position = Vector3(0, {h * PIXEL_SIZE_3D / 2.0}, 0)\n'
            f'texture = ExtResource("{ext_id}")\n'
            f'pixel_size = {PIXEL_SIZE_3D}\n'
            f'billboard = 2\n'
            f'shaded = false\n'
            f'texture_filter = 0\n'
            f'hframes = {frames}\n'
            f'frame = 0'
        )
        if etype == "door":
            nodes.append(
                f'[node name="Collision" type="CollisionShape3D" parent="{node_name}"]\n'
                f'position = Vector3(0, 1, 0)\n'
                f'shape = SubResource("BoxShape3D_door")'
            )
            connections.append(f'[connection signal="body_entered" from="{node_name}" to="{node_name}" method="_on_body_entered"]')

    if door_script_id is not None:
        sub_resources.insert(0, '[sub_resource type="BoxShape3D" id="BoxShape3D_door"]\nsize = Vector3(1, 2, 1)')
        load_steps += 1

    spawn_col, spawn_row = find_spawn(level)
    spawn_ground_y = floor_top_units(level, spawn_row, spawn_col)
    add_ext("Tex_player", "Texture2D", "res://" + PLAYER_ART)
    add_ext("PlayerScript", "Script", "res://scripts/Player3D.gd")
    # Capsule half-height above the CharacterBody3D's own origin, so with
    # Player.position.y set to the floor surface (feet level), the capsule's
    # bottom -- not its center -- touches the ground.
    sub_resources.append(
        '[sub_resource type="CapsuleShape3D" id="CapsuleShape3D_player"]\n'
        'radius = 0.3\n'
        'height = 1.0'
    )
    load_steps += 1
    nodes.append(
        f'[node name="Player" type="CharacterBody3D" parent="."]\n'
        f'position = Vector3({float(spawn_col)}, {spawn_ground_y}, {float(spawn_row)})\n'
        f'script = ExtResource("PlayerScript")'
    )
    nodes.append(
        '[node name="Collision" type="CollisionShape3D" parent="Player"]\n'
        'position = Vector3(0, 0.5, 0)\n'
        'shape = SubResource("CapsuleShape3D_player")'
    )
    nodes.append(
        f'[node name="Sprite" type="Sprite3D" parent="Player"]\n'
        f'position = Vector3(0, {PLAYER_H * PIXEL_SIZE_3D / 2.0}, 0)\n'
        f'texture = ExtResource("Tex_player")\n'
        f'pixel_size = {PIXEL_SIZE_3D}\n'
        f'billboard = 2\n'
        f'shaded = false\n'
        f'texture_filter = 0\n'
        f'hframes = {PLAYER_FRAMES}\n'
        f'frame = 0'
    )
    nodes.append(
        # First-person by default: eye height near the top of the capsule
        # (capsule height 1.0, feet at the Player node's own origin), a
        # normal perspective projection looking down -Z (Player3D.gd's
        # mouse-look then yaws the whole Player node and pitches just this
        # camera, and swaps this same node's local position between
        # CAMERA_FIRST_PERSON/CAMERA_THIRD_PERSON on the F5 view toggle)
        # -- not the old isometric orthogonal follow-cam.
        '[node name="Camera3D" type="Camera3D" parent="Player"]\n'
        'position = Vector3(0, 0.85, 0)\n'
        'fov = 75'
    )
    # Minecraft-style first-person arm: a plain forearm+hand box pair (no
    # Blender model -- this is screen-space set dressing, not world
    # geometry), parented to the camera so it inherits its pitch and sits
    # in the same lower-right-of-view spot every FPS game puts the
    # player's own arm. Player3D.gd bobs its local position while walking
    # and hides it in third-person view.
    arm_mesh_id = add_sub_once(sub_resources, "BoxMesh", "BoxMesh_forearm", 'size = Vector3(0.12, 0.12, 0.5)')
    hand_mesh_id = add_sub_once(sub_resources, "BoxMesh", "BoxMesh_hand", 'size = Vector3(0.14, 0.1, 0.16)')
    skin_mat_id = add_sub_once(
        sub_resources, "StandardMaterial3D", "StandardMaterial3D_skin",
        'albedo_color = Color(0.85, 0.68, 0.54, 1)\nroughness = 0.7'
    )
    load_steps += 3
    nodes.append(
        # NodePath in `parent=` is relative to the *scene root*, not the
        # immediately preceding node -- Camera3D itself lives at
        # "Player/Camera3D" from root, so its children need that full
        # path, not just "Camera3D" (got this wrong on the first pass:
        # Godot silently drops the node with a "vanished" warning instead
        # of a hard parse error, so this only surfaced by grepping the
        # scene-run output for "WARNING"/"vanished", not just "error").
        '[node name="Arm" type="Node3D" parent="Player/Camera3D"]\n'
        'position = Vector3(0.32, -0.32, -0.45)\n'
        'rotation_degrees = Vector3(15, -20, 10)'
    )
    nodes.append(
        f'[node name="Forearm" type="MeshInstance3D" parent="Player/Camera3D/Arm"]\n'
        f'mesh = SubResource("{arm_mesh_id}")\n'
        f'material_override = SubResource("{skin_mat_id}")\n'
        'position = Vector3(0, 0, -0.15)'
    )
    nodes.append(
        f'[node name="Hand" type="MeshInstance3D" parent="Player/Camera3D/Arm"]\n'
        f'mesh = SubResource("{hand_mesh_id}")\n'
        f'material_override = SubResource("{skin_mat_id}")\n'
        'position = Vector3(0, 0, -0.42)'
    )

    if room_name in EXTERIOR_MODELS:
        model_id = add_ext("ExteriorModel", "PackedScene", EXTERIOR_MODELS[room_name])
        nodes.append(f'[node name="Exterior" parent="." instance=ExtResource("{model_id}")]')

    header = f"[gd_scene load_steps={load_steps} format=3]\n"
    body = "\n\n".join(ext_resources + sub_resources + nodes + connections)
    return header + "\n" + body + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("level_json", type=Path)
    ap.add_argument("--name", default=None)
    args = ap.parse_args()

    level_path = args.level_json
    if not level_path.is_absolute():
        level_path = (Path.cwd() / level_path).resolve()
    level = load_level(level_path)

    if level.get("perspective") != "isometric":
        print(f"ERROR: only isometric levels are supported by this importer, got {level.get('perspective')!r}", file=sys.stderr)
        sys.exit(1)

    room_name = args.name or level_path.stem

    scene_2d = build_2d_scene(level, room_name)
    scene_3d = build_3d_scene(level, room_name)

    out_2d = PROJECT_ROOT / "scenes" / "rooms_2d" / f"{room_name}.tscn"
    out_3d = PROJECT_ROOT / "scenes" / "rooms_3d" / f"{room_name}.tscn"
    out_2d.parent.mkdir(parents=True, exist_ok=True)
    out_3d.parent.mkdir(parents=True, exist_ok=True)
    out_2d.write_text(scene_2d)
    out_3d.write_text(scene_3d)
    print(f"wrote {out_2d}")
    print(f"wrote {out_3d}")


if __name__ == "__main__":
    main()
