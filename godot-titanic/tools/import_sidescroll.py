#!/usr/bin/env python3
"""
Import a Majorsilence.Games.Learning sidescroll (platformer) level JSON into
a Godot 2D scene. Sibling of import_level.py, which only handles isometric
levels -- sidescroll uses a completely different math model (simple
orthogonal grid, real gravity/jump physics, one-way platforms, ladders), not
the diamond projection, so it gets its own script rather than a branch
bolted onto the isometric one.

Replicates the C# engine's exact conventions:
  - FlatTilemap.Render: worldX = col*tileWidth, worldY = row*tileHeight,
    top-left anchored, no elevation/y-sort (tiles draw as one flat layer)
  - Physics.PlatformerBody: gravity=1500, jump=-480, speed=120 (Game.cs's
    BasePlayerSpeed), maxFall=700, climbSpeed=110
  - Room.KindAt: column out of range -> Solid; row out of range -> Empty
    (open top/bottom -- doors are expected to catch the player first)
  - Room.EntityTile: an authored entity tile is shifted up to the first
    non-solid/non-oneway tile, since level data anchors entities on the
    surface they rest on, not the open tile above it (avoids spawning
    inside a floor)

Usage:
    python3 tools/import_sidescroll.py <path-to-level.json> [--name grand-stair-escape]
"""
import argparse
import json
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]

ENTITY_ART = {
    ("tix", None): ("assets/artwork/titanic-demo/tix-coin.png", 16, 16, 1),
    ("door", None): ("assets/artwork/titanic-demo/doorway.png", 24, 36, 1),
}
PLAYER_ART = "assets/artwork/isometric-demo/character.png"
PLAYER_W, PLAYER_H, PLAYER_FRAMES = 16, 32, 4
PLAYER_FRAME_DURATION_MS = 150

LAYER_SOLID = 1
LAYER_ONEWAY = 2
LAYER_PLAYER = 8


def load_level(path: Path) -> dict:
    return json.loads(path.read_text())


def tile_type(level: dict, ch: str) -> str:
    return level["legend"][ch]


def tile_kind(level: dict, r: int, c: int) -> str:
    rows = len(level["tiles"])
    cols = len(level["tiles"][0])
    if c < 0 or c >= cols:
        return "solid"
    if r < 0 or r >= rows:
        return "empty"
    t = tile_type(level, level["tiles"][r][c])
    if t in level["solid"]:
        return "solid"
    if t in level.get("platforms", []):
        return "oneway"
    if t in level.get("climbable", []):
        return "ladder"
    return "empty"


def entity_tile(level: dict, col: int, row: int) -> tuple[int, int]:
    open_row = row
    while open_row >= 0 and tile_kind(level, open_row, col) in ("solid", "oneway"):
        open_row -= 1
    return (col, row) if open_row < 0 else (col, open_row)


def tile_world(level: dict, col: int, row: int) -> tuple[float, float]:
    return col * level["tileWidth"], row * level["tileHeight"]


def find_spawn(level: dict) -> tuple[int, int]:
    for ent in level.get("entities", []):
        if ent["type"] in ("spawnPoint", "playerStart"):
            return entity_tile(level, ent["column"], ent["row"])
    return 0, 0


def source_json_to_scene_res(target_json: str) -> str:
    return f"res://scenes/rooms_2d/{Path(target_json).stem}.tscn"


def gd_str(s: str) -> str:
    return '"' + s.replace("\\", "\\\\").replace('"', '\\"') + '"'


def build_scene(level: dict, room_name: str) -> str:
    ext_resources, sub_resources, nodes, connections = [], [], [], []
    load_steps = 1

    def add_ext(id_, type_, path_):
        nonlocal load_steps
        ext_resources.append(f'[ext_resource type="{type_}" path="{path_}" id="{id_}"]')
        load_steps += 1
        return id_

    def add_sub(text):
        nonlocal load_steps
        sub_resources.append(text)
        load_steps += 1

    tw, th = level["tileWidth"], level["tileHeight"]
    tileset_res_path = "res://assets/" + level["tilesetPath"].split("assets/", 1)[-1]
    tileset_ext_id = add_ext("Tileset", "Texture2D", tileset_res_path)
    ladder_script_id = add_ext("LadderScript", "Script", "res://scripts/Ladder2D.gd")

    frame_atlas_ids = {}
    for name, idx in level["tileFrames"].items():
        if idx < 0:
            continue
        used = any(tile_type(level, ch) == name for row in level["tiles"] for ch in row)
        if not used:
            continue
        aid = f"AtlasTexture_{name}"
        add_sub(
            f'[sub_resource type="AtlasTexture" id="{aid}"]\n'
            f'atlas = ExtResource("{tileset_ext_id}")\n'
            f'region = Rect2({idx * tw}, 0, {tw}, {th})'
        )
        frame_atlas_ids[name] = aid

    nodes.append(f'[node name="{room_name}" type="Node2D"]')

    rows, cols = len(level["tiles"]), len(level["tiles"][0])
    solid_shape_added = oneway_shape_added = False
    for r in range(rows):
        for c in range(cols):
            ch = level["tiles"][r][c]
            t = tile_type(level, ch)
            wx, wy = tile_world(level, c, r)

            if t in frame_atlas_ids:
                nodes.append(
                    f'[node name="Tile_r{r}_c{c}" type="Sprite2D" parent="."]\n'
                    f'position = Vector2({wx}, {wy})\n'
                    f'texture = SubResource("{frame_atlas_ids[t]}")\n'
                    f'centered = false'
                )

            kind = tile_kind(level, r, c)
            cx, cy = wx + tw / 2.0, wy + th / 2.0
            if kind == "solid":
                if not solid_shape_added:
                    add_sub(f'[sub_resource type="RectangleShape2D" id="RectangleShape2D_solid"]\nsize = Vector2({tw}, {th})')
                    solid_shape_added = True
                body = f"Solid_r{r}_c{c}"
                nodes.append(
                    f'[node name="{body}" type="StaticBody2D" parent="."]\n'
                    f'position = Vector2({cx}, {cy})\ncollision_layer = {LAYER_SOLID}\ncollision_mask = 0'
                )
                nodes.append(f'[node name="Collision" type="CollisionShape2D" parent="{body}"]\nshape = SubResource("RectangleShape2D_solid")')
            elif kind in ("oneway", "ladder"):
                if not oneway_shape_added:
                    add_sub(f'[sub_resource type="RectangleShape2D" id="RectangleShape2D_oneway"]\nsize = Vector2({tw}, {th})')
                    oneway_shape_added = True
                body = f"OneWay_r{r}_c{c}"
                nodes.append(
                    f'[node name="{body}" type="StaticBody2D" parent="."]\n'
                    f'position = Vector2({cx}, {cy})\ncollision_layer = {LAYER_ONEWAY}\ncollision_mask = 0'
                )
                nodes.append(
                    f'[node name="Collision" type="CollisionShape2D" parent="{body}"]\n'
                    f'shape = SubResource("RectangleShape2D_oneway")\none_way_collision = true'
                )
                if kind == "ladder":
                    lb = f"Ladder_r{r}_c{c}"
                    nodes.append(
                        f'[node name="{lb}" type="Area2D" parent="."]\n'
                        f'position = Vector2({cx}, {cy})\ncollision_layer = 0\ncollision_mask = {LAYER_PLAYER}\n'
                        f'script = ExtResource("{ladder_script_id}")'
                    )
                    nodes.append(f'[node name="Collision" type="CollisionShape2D" parent="{lb}"]\nshape = SubResource("RectangleShape2D_oneway")')
                    connections.append(f'[connection signal="body_entered" from="{lb}" to="{lb}" method="_on_body_entered"]')
                    connections.append(f'[connection signal="body_exited" from="{lb}" to="{lb}" method="_on_body_exited"]')

    # Left/right boundary only -- Room.KindAt returns Solid past the column
    # range but Empty past the row range (the top/bottom are meant to be
    # open; doors are expected to catch the player before that matters).
    boundary_h = rows * th
    add_sub(f'[sub_resource type="RectangleShape2D" id="RectangleShape2D_boundary"]\nsize = Vector2(8, {boundary_h})')
    for name, x in [("Left", -4.0), ("Right", cols * tw + 4.0)]:
        nodes.append(
            f'[node name="Boundary{name}" type="StaticBody2D" parent="."]\n'
            f'position = Vector2({x}, {boundary_h / 2.0})\ncollision_layer = {LAYER_SOLID}\ncollision_mask = 0'
        )
        nodes.append(f'[node name="Shape" type="CollisionShape2D" parent="Boundary{name}"]\nshape = SubResource("RectangleShape2D_boundary")')

    door_script_id = None
    for ent in level.get("entities", []):
        etype = ent["type"]
        if etype in ("spawnPoint", "playerStart"):
            continue
        art_key = (etype, None)
        if art_key not in ENTITY_ART:
            print(f"WARN: no art mapped for entity type={etype!r}, skipping visual", file=sys.stderr)
            continue

        col, row = entity_tile(level, ent["column"], ent["row"])
        wx, wy = tile_world(level, col, row)
        img_path, w, h, frames = ENTITY_ART[art_key]
        res_path = "res://assets/" + img_path.split("assets/", 1)[-1]
        ext_id = f"Tex_{etype}"
        if not any(f'id="{ext_id}"' in e for e in ext_resources):
            add_ext(ext_id, "Texture2D", res_path)

        anchor_x, anchor_y = wx + tw / 2.0, wy + th
        node_name = f"Entity_{etype}_r{row}_c{col}"
        wrapper_type = "Area2D" if etype == "door" else "Node2D"
        extra = ""
        if etype == "door":
            if door_script_id is None:
                door_script_id = add_ext("DoorScript", "Script", "res://scripts/Door2D.gd")
            target = source_json_to_scene_res(ent["properties"].get("target", ""))
            extra = f'script = ExtResource("{door_script_id}")\ntarget_scene = {gd_str(target)}\n'

        nodes.append((
            f'[node name="{node_name}" type="{wrapper_type}" parent="."]\n'
            f'position = Vector2({anchor_x}, {anchor_y})\n' + extra
        ).rstrip())
        nodes.append(
            f'[node name="Visual" type="Sprite2D" parent="{node_name}"]\n'
            f'z_index = 1\ntexture = ExtResource("{ext_id}")\ncentered = false\noffset = Vector2({-w / 2.0}, {-h})'
        )
        if etype == "door":
            if not any('id="RectangleShape2D_door"' in s for s in sub_resources):
                add_sub('[sub_resource type="RectangleShape2D" id="RectangleShape2D_door"]\nsize = Vector2(24, 32)')
            nodes.append(f'[node name="Collision" type="CollisionShape2D" parent="{node_name}"]\nposition = Vector2(0, -16)\nshape = SubResource("RectangleShape2D_door")')
            connections.append(f'[connection signal="body_entered" from="{node_name}" to="{node_name}" method="_on_body_entered"]')

    spawn_col, spawn_row = find_spawn(level)
    sx, sy = tile_world(level, spawn_col, spawn_row)
    sx += tw / 2.0
    sy += th

    add_ext("Tex_player", "Texture2D", "res://" + PLAYER_ART)
    add_ext("PlayerScript", "Script", "res://scripts/Player2DSide.gd")
    frame_ids = []
    for f in range(PLAYER_FRAMES):
        fid = f"AtlasTexture_player_f{f}"
        add_sub(f'[sub_resource type="AtlasTexture" id="{fid}"]\natlas = ExtResource("Tex_player")\nregion = Rect2({f * PLAYER_W}, 0, {PLAYER_W}, {PLAYER_H})')
        frame_ids.append(fid)
    frame_entries = ", ".join(f'{{\n"duration": 1.0,\n"texture": SubResource("{fid}")\n}}' for fid in frame_ids)
    fps = 1000.0 / PLAYER_FRAME_DURATION_MS
    add_sub(
        '[sub_resource type="SpriteFrames" id="SpriteFrames_player"]\n'
        f'animations = [{{\n"frames": [{frame_entries}],\n"loop": true,\n"name": &"walk",\n"speed": {fps}\n}}]'
    )
    add_sub('[sub_resource type="RectangleShape2D" id="RectangleShape2D_player"]\nsize = Vector2(12, 28)')

    nodes.append(
        f'[node name="Player" type="CharacterBody2D" parent="."]\n'
        f'position = Vector2({sx}, {sy})\ncollision_layer = {LAYER_PLAYER}\ncollision_mask = {LAYER_SOLID | LAYER_ONEWAY}\n'
        f'script = ExtResource("PlayerScript")'
    )
    nodes.append(
        '[node name="Sprite" type="AnimatedSprite2D" parent="Player"]\n'
        'z_index = 1\nsprite_frames = SubResource("SpriteFrames_player")\nanimation = &"walk"\n'
        'centered = false\noffset = Vector2(-8, -32)'
    )
    nodes.append('[node name="Collision" type="CollisionShape2D" parent="Player"]\nposition = Vector2(0, -14)\nshape = SubResource("RectangleShape2D_player")')
    nodes.append('[node name="Camera2D" type="Camera2D" parent="Player"]\nzoom = Vector2(3, 3)')

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

    if level.get("perspective") != "sidescroll":
        print(f"ERROR: only sidescroll levels are supported by this importer, got {level.get('perspective')!r}", file=sys.stderr)
        sys.exit(1)

    room_name = args.name or level_path.stem
    scene = build_scene(level, room_name)
    out = PROJECT_ROOT / "scenes" / "rooms_2d" / f"{room_name}.tscn"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(scene)
    print(f"wrote {out}")


if __name__ == "__main__":
    main()
