"""
Builds a stylized, recognizable Grand Staircase (split double flight up to
an upper gallery, a clock on the back wall, a domed skylight, wood-paneled
walls) as a standalone 3D room model for godot-titanic's grand-stair-escape
3D scene. Run headless:

    blender --background --python build_grand_staircase.py

Coordinate convention: everywhere here, positions are (x, depth, height) in
"Godot" terms; P() is the one place that converts to Blender's axes and
negates depth, matching Blender's glTF exporter sending Blender Y to
glTF's *negative* Z (verified empirically -- see build_hull.py's docstring).
Never construct a bpy location/vertex tuple without going through P().

Floor/wall/step meshes are named with a "-col" suffix so Godot's glTF
importer auto-generates trimesh collision for them on import -- everything
else (clock, dome, balustrade) is visual only, no collision.
"""
import bpy
import bmesh
import math

OUT = "/home/peter/source/repos/Majorsilence.Games.Learning/godot-titanic/assets/models/grand_staircase.glb"

ROOM_W = 8.0
ROOM_D = 10.0
STEP_H = 0.22
N_STEPS_LOWER = 8
LANDING_Y = N_STEPS_LOWER * STEP_H
N_STEPS_UPPER = 7
GALLERY_Y = LANDING_Y + N_STEPS_UPPER * STEP_H
WALL_H = GALLERY_Y + 3.0


def P(x, depth, height):
    return (x, -depth, height)


bpy.ops.wm.read_factory_settings(use_empty=True)


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def make_material(name, color, emission=0.0, alpha=1.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, alpha)
    bsdf.inputs["Roughness"].default_value = 0.45
    if alpha < 1.0:
        mat.blend_method = "BLEND"
    if emission > 0.0:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    return mat


MAT_WOOD = MAT_GOLD = MAT_CARPET = MAT_GLASS = MAT_CLOCKFACE = MAT_CLOCKHAND = None


def box(name, center, size, mat, collide=False):
    """center, size are (x, depth, height)."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=P(*center))
    obj = bpy.context.active_object
    obj.name = name + ("-col" if collide else "")
    obj.scale = (size[0] / 2.0, size[1] / 2.0, size[2] / 2.0)
    obj.data.materials.append(mat)
    return obj


def build_flight(name, x_center, h0, d0, h1, d1, n_steps, width, mat):
    """A straight run of n_steps boxed steps rising from (h0,d0) to (h1,d1)
    (height, depth)."""
    dd = (d1 - d0) / n_steps
    dh = (h1 - h0) / n_steps
    for i in range(n_steps):
        step_h = h0 + dh * (i + 1)
        step_d = d0 + dd * i + dd / 2.0
        box(f"{name}_Step{i}", (x_center, step_d, step_h - STEP_H / 2.0), (width, abs(dd) + 0.02, STEP_H), mat, collide=True)


def build_ramp_collider(name, x_center, h0, d0, h1, d1, width):
    """Sloped box (render-hidden, collision on) under a flight so trimesh
    collision gives a smooth walkable ramp alongside the visible steps."""
    mesh = bpy.data.meshes.new(name + "_mesh")
    obj = bpy.data.objects.new(name + "-col", mesh)
    bpy.context.collection.objects.link(obj)
    hw = width / 2.0
    verts = [
        P(x_center - hw, d0, h0 - 0.05), P(x_center + hw, d0, h0 - 0.05),
        P(x_center + hw, d1, h1 - 0.05), P(x_center - hw, d1, h1 - 0.05),
        P(x_center - hw, d0, h0 - 0.4), P(x_center + hw, d0, h0 - 0.4),
        P(x_center + hw, d1, h1 - 0.4), P(x_center - hw, d1, h1 - 0.4),
    ]
    faces = [(0, 1, 2, 3), (4, 5, 1, 0), (7, 6, 5, 4), (3, 2, 6, 7), (1, 5, 6, 2), (0, 3, 7, 4)]
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj.hide_render = True
    return obj


def build_railing(name, x, h0, d0, h1, d1, height=0.9):
    n = 6
    for i in range(n + 1):
        t = i / n
        ph = h0 + (h1 - h0) * t
        pd = d0 + (d1 - d0) * t
        box(f"{name}_Post{i}", (x, pd, ph + height / 2.0), (0.06, 0.06, height), MAT_GOLD)

    mesh = bpy.data.meshes.new(name + "_rail_mesh")
    obj = bpy.data.objects.new(name + "_Rail", mesh)
    bpy.context.collection.objects.link(obj)
    hw = 0.05
    verts = [
        P(x - hw, d0, h0 + height), P(x + hw, d0, h0 + height),
        P(x + hw, d1, h1 + height), P(x - hw, d1, h1 + height),
        P(x - hw, d0, h0 + height - 0.08), P(x + hw, d0, h0 + height - 0.08),
        P(x + hw, d1, h1 + height - 0.08), P(x - hw, d1, h1 + height - 0.08),
    ]
    faces = [(0, 1, 2, 3), (4, 5, 1, 0), (7, 6, 5, 4), (3, 2, 6, 7), (1, 5, 6, 2), (0, 3, 7, 4)]
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj.data.materials.append(MAT_GOLD)


def build_clock(x, depth, height):
    bpy.ops.mesh.primitive_cylinder_add(radius=0.9, depth=0.12, location=P(x, depth, height))
    face = bpy.context.active_object
    face.rotation_euler = (math.radians(90), 0, 0)
    face.name = "ClockFace"
    face.data.materials.append(MAT_CLOCKFACE)

    bpy.ops.mesh.primitive_cylinder_add(radius=0.98, depth=0.06, location=P(x, depth - 0.07, height))
    rim = bpy.context.active_object
    rim.rotation_euler = (math.radians(90), 0, 0)
    rim.name = "ClockRim"
    rim.data.materials.append(MAT_GOLD)

    box("ClockHandHour", (x - 0.15, depth - 0.1, height + 0.1), (0.4, 0.05, 0.06), MAT_CLOCKHAND)
    box("ClockHandMinute", (x, depth - 0.1, height + 0.35), (0.06, 0.05, 0.7), MAT_CLOCKHAND)


def build_dome(x, depth, height, radius):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=P(x, depth, height), segments=24, ring_count=12)
    dome = bpy.context.active_object
    dome.name = "Dome"
    bm = bmesh.new()
    bm.from_mesh(dome.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < 0.0], context="VERTS")
    bm.to_mesh(dome.data)
    bm.free()
    dome.data.materials.append(MAT_GLASS)
    return dome


clear_scene()
MAT_WOOD = make_material("Wood", (0.32, 0.19, 0.1))
MAT_GOLD = make_material("Gold", (0.55, 0.42, 0.18))
MAT_CARPET = make_material("Carpet", (0.45, 0.06, 0.08))
MAT_GLASS = make_material("Glass", (0.75, 0.85, 0.95), emission=0.6, alpha=0.35)
MAT_CLOCKFACE = make_material("ClockFace", (0.92, 0.88, 0.75))
MAT_CLOCKHAND = make_material("ClockHand", (0.05, 0.05, 0.05))

# Entry floor (depth 0..2.2), rises via center flight to mid-landing
# (depth 2.4..4.4), splits into two flights climbing left/right to the
# upper gallery (depth 5.4..7.6), which runs the rest of the room depth.
box("EntryFloor", (ROOM_W / 2.0, 1.0, -0.1), (ROOM_W, 2.2, 0.2), MAT_CARPET, collide=True)
build_ramp_collider("LowerFlightRamp", ROOM_W / 2.0, 0.0, 2.2, LANDING_Y, 4.4, 3.2)
build_flight("LowerFlight", ROOM_W / 2.0, 0.0, 2.4, LANDING_Y, 4.4, N_STEPS_LOWER, 3.2, MAT_WOOD)
box("MidLanding", (ROOM_W / 2.0, 4.9, LANDING_Y - 0.1), (4.2, 1.0, 0.2), MAT_CARPET, collide=True)

split_d0, split_d1 = 5.4, 7.6
left_x, right_x = ROOM_W * 0.27, ROOM_W * 0.73
build_ramp_collider("LeftFlightRamp", left_x, LANDING_Y, split_d0, GALLERY_Y, split_d1, 2.2)
build_flight("LeftFlight", left_x, LANDING_Y, split_d0, GALLERY_Y, split_d1, N_STEPS_UPPER, 2.2, MAT_WOOD)
build_ramp_collider("RightFlightRamp", right_x, LANDING_Y, split_d0, GALLERY_Y, split_d1, 2.2)
build_flight("RightFlight", right_x, LANDING_Y, split_d0, GALLERY_Y, split_d1, N_STEPS_UPPER, 2.2, MAT_WOOD)

box("Gallery", (ROOM_W / 2.0, 8.8, GALLERY_Y - 0.1), (ROOM_W, 2.4, 0.2), MAT_CARPET, collide=True)

box("WallBack", (ROOM_W / 2.0, ROOM_D, WALL_H / 2.0), (ROOM_W, 0.3, WALL_H), MAT_WOOD, collide=True)
box("WallFront", (ROOM_W / 2.0, -0.2, WALL_H / 2.0), (ROOM_W, 0.3, WALL_H), MAT_WOOD, collide=True)
box("WallLeft", (-0.2, ROOM_D / 2.0, WALL_H / 2.0), (0.3, ROOM_D, WALL_H), MAT_WOOD, collide=True)
box("WallRight", (ROOM_W + 0.2, ROOM_D / 2.0, WALL_H / 2.0), (0.3, ROOM_D, WALL_H), MAT_WOOD, collide=True)

build_railing("LowerRailL", ROOM_W / 2.0 - 1.7, 0.0, 2.4, LANDING_Y, 4.4)
build_railing("LowerRailR", ROOM_W / 2.0 + 1.7, 0.0, 2.4, LANDING_Y, 4.4)
build_railing("LeftFlightRailL", left_x - 1.2, LANDING_Y, split_d0, GALLERY_Y, split_d1)
build_railing("LeftFlightRailR", left_x + 1.2, LANDING_Y, split_d0, GALLERY_Y, split_d1)
build_railing("RightFlightRailL", right_x - 1.2, LANDING_Y, split_d0, GALLERY_Y, split_d1)
build_railing("RightFlightRailR", right_x + 1.2, LANDING_Y, split_d0, GALLERY_Y, split_d1)

build_clock(ROOM_W / 2.0, ROOM_D - 0.3, GALLERY_Y + 1.4)
build_dome(ROOM_W / 2.0, ROOM_D / 2.0, WALL_H - 0.2, min(ROOM_W, ROOM_D) * 0.45)

bpy.ops.object.light_add(type="POINT", location=P(ROOM_W / 2.0, ROOM_D - 1.5, GALLERY_Y + 1.6))
light = bpy.context.active_object
light.data.energy = 400
light.data.color = (1.0, 0.85, 0.6)

bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB")
print(f"WROTE {OUT}")
