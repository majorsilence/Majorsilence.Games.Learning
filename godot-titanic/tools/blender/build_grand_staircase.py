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
import mathutils

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


def make_material(name, color, emission=0.0, alpha=1.0, metallic=0.0, roughness=0.45):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, alpha)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if alpha < 1.0:
        mat.blend_method = "BLEND"
    if emission > 0.0:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    return mat


MAT_WOOD = MAT_GOLD = MAT_CARPET = MAT_GLASS = MAT_CLOCKFACE = MAT_CLOCKHAND = None


def box(name, center, size, mat, collide=False):
    """center, size are (x, depth, height) -- the box's real, full edge
    lengths, not half-extents."""
    # primitive_cube_add(size=1.0) creates a cube with edge length 1.0 (not
    # a half-extent of 1.0), so scale must equal the desired edge length
    # directly. This was previously `size[i] / 2.0`, which silently built
    # every box() piece in this room -- EntryFloor, MidLanding, Gallery,
    # all four walls -- at HALF its intended size (confirmed empirically:
    # EntryFloor's baked collision AABB measured 4.0x0.1x1.1 against an
    # intended 8.0x0.2x2.2). Found while investigating "can't walk up the
    # stairs" -- undersized walls/floors likely also explain any
    # walk-through-geometry gaps in this room specifically.
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=P(*center))
    obj = bpy.context.active_object
    obj.name = name + ("-col" if collide else "")
    obj.scale = (size[0], size[1], size[2])
    # Bake the scale into the mesh's actual vertex data instead of leaving
    # it as an object-level transform. Confirmed by an automated physics
    # probe (godot-titanic/tests/) that Godot's physics does NOT reliably
    # apply a non-uniform *inherited* scale (e.g. EntryFloor's (4.0, 0.1,
    # 1.1)) to a glTF-imported ConcavePolygonShape3D -- the collision shape
    # silently stayed a 1x1x1 unit cube regardless of the visual size,
    # which is why the player fell straight through every collide=True box
    # in this file (floors, landings, walls) instead of standing on it.
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    return obj


def build_flight(name, x_center, h0, d0, h1, d1, n_steps, width, mat):
    """A straight run of n_steps boxed steps rising from (h0,d0) to (h1,d1)
    (height, depth). Visual only (collide=False) -- a CharacterBody3D has
    no built-in stair-auto-step, so a real 0.22-unit box collider per step
    would read as a wall the player bumps into instead of climbs. The
    actual walking surface is build_ramp_collider() below, a single smooth
    invisible slope covering the same run -- leaving these steps' own
    collision on (as a first pass did) fights that ramp instead of just
    looking good on top of it."""
    dd = (d1 - d0) / n_steps
    dh = (h1 - h0) / n_steps
    for i in range(n_steps):
        step_h = h0 + dh * (i + 1)
        step_d = d0 + dd * i + dd / 2.0
        box(f"{name}_Step{i}", (x_center, step_d, step_h - STEP_H / 2.0), (width, abs(dd) + 0.02, STEP_H), mat, collide=False)


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
    _recalc_outward_normals(mesh)
    obj.hide_render = True
    return obj


def _recalc_outward_normals(mesh):
    """P(x, depth, height) -> (x, -depth, height) is a mirror (single-axis
    negation), not a rotation -- any face winding hand-authored in the
    natural (x, depth, height) order comes out inward-facing after that
    flip. Confirmed by hand for the ramp collider's top face (cross
    product of its first two edges pointed -Z, into the ground, not up)
    and this was why CharacterBody3D physics wouldn't climb it -- a
    downward-facing "floor" isn't floor. bmesh's own outward-normal
    solver is used instead of trusting/re-deriving hand-picked winding for
    every face by hand, and fixes the same latent bug in build_railing's
    rail mesh below."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()


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
    _recalc_outward_normals(mesh)
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


def build_cherub_statue(x, depth, base_height):
    """The single most recognizable detail in the real Grand Staircase:
    a small gilt cherub standing on the newel post at the foot of the
    stairs, one arm raised holding a lit torch. Built from the same
    capsule/sphere primitives as the game's own humanoids (see
    build_humanoid() in import_level.py) rather than anything more
    elaborate -- it's a small background prop, not something the player
    stands next to and inspects closely."""
    pedestal_h = 0.35
    box(f"CherubPedestal", (x, depth, base_height + pedestal_h / 2.0), (0.3, 0.3, pedestal_h), MAT_GOLD, collide=False)
    body_y = base_height + pedestal_h

    bpy.ops.mesh.primitive_cylinder_add(radius=0.09, depth=0.35, location=P(x, depth, body_y + 0.175))
    torso = bpy.context.active_object
    torso.name = "CherubTorso"
    torso.data.materials.append(MAT_CHERUB)

    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.09, location=P(x, depth, body_y + 0.44))
    head = bpy.context.active_object
    head.name = "CherubHead"
    head.data.materials.append(MAT_CHERUB)

    # Raised arm: angled up and out from the shoulder toward a torch held
    # above head height.
    arm_base = mathutils.Vector(P(x, depth, body_y + 0.32))
    arm_tip = mathutils.Vector(P(x + 0.05, depth - 0.05, body_y + 0.72))
    arm_mid = (arm_base + arm_tip) / 2.0
    bpy.ops.mesh.primitive_cylinder_add(radius=0.03, depth=(arm_tip - arm_base).length, location=arm_mid)
    arm = bpy.context.active_object
    arm.name = "CherubArm"
    arm.data.materials.append(MAT_CHERUB)
    # Point the cylinder's own Z axis (its length axis) from base to tip.
    arm.rotation_mode = "QUATERNION"
    arm.rotation_quaternion = (arm_tip - arm_base).to_track_quat("Z", "Y")

    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.06, location=P(x + 0.06, depth - 0.06, body_y + 0.78))
    flame = bpy.context.active_object
    flame.name = "CherubFlame"
    flame.data.materials.append(MAT_FLAME)


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


def build_dome_lattice(x, depth, height, radius):
    """The ornate wrought-iron radial-spoke frame over the glass dome, from
    the reference photos -- real geometry (thin oriented cylinder segments
    following the hemisphere + a couple of concentric torus rings) rather
    than a patterned texture, since Blender's node-based procedural
    textures (a Checker node, say) don't survive glTF export -- only baked
    image textures do, and baking one just for this is a lot of pipeline
    for a decorative overlay when actual geometry gets the same look for
    free with the tools already in use everywhere else in this file."""
    n_ribs = 16
    n_seg = 5
    rib_r = 0.025
    for i in range(n_ribs):
        theta = (2 * math.pi * i) / n_ribs
        pts = []
        for j in range(n_seg + 1):
            phi = (math.pi / 2) * (j / n_seg)  # 0 at apex .. pi/2 at rim
            r = radius * math.sin(phi)
            h = radius * math.cos(phi)
            px = x + r * math.cos(theta)
            pd = depth + r * math.sin(theta)
            ph = height + h
            pts.append(mathutils.Vector(P(px, pd, ph)))
        for j in range(len(pts) - 1):
            a, b = pts[j], pts[j + 1]
            mid = (a + b) / 2.0
            seg_len = (b - a).length
            bpy.ops.mesh.primitive_cylinder_add(radius=rib_r, depth=seg_len, location=mid)
            seg = bpy.context.active_object
            seg.name = f"DomeRib{i}_{j}"
            seg.data.materials.append(MAT_IRON)
            seg.rotation_mode = "QUATERNION"
            seg.rotation_quaternion = (b - a).to_track_quat("Z", "Y")

    for phi_deg in (30, 60):
        phi = math.radians(phi_deg)
        r = radius * math.sin(phi)
        h = height + radius * math.cos(phi)
        bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=rib_r, location=P(x, depth, h))
        ring = bpy.context.active_object
        ring.name = f"DomeRing{phi_deg}"
        ring.data.materials.append(MAT_GOLD)


clear_scene()
# Warmed toward the real replica's polished reddish mahogany (reference
# photos of the actual Grand Staircase set/replica), and MAT_GOLD given
# real metallic response instead of a flat brass-colored diffuse -- the
# wrought-iron/gilt balustrade and the cherub statue below both read as
# "gold" largely because they're actually reflective, not just yellow.
MAT_WOOD = make_material("Wood", (0.42, 0.2, 0.12), roughness=0.35)
MAT_GOLD = make_material("Gold", (0.62, 0.47, 0.2), metallic=0.85, roughness=0.3)
MAT_CARPET = make_material("Carpet", (0.45, 0.06, 0.08))
MAT_GLASS = make_material("Glass", (0.75, 0.85, 0.95), emission=0.6, alpha=0.35)
MAT_CLOCKFACE = make_material("ClockFace", (0.92, 0.88, 0.75))
MAT_CLOCKHAND = make_material("ClockHand", (0.05, 0.05, 0.05))
MAT_CHERUB = make_material("Cherub", (0.7, 0.55, 0.25), metallic=0.85, roughness=0.25)
MAT_FLAME = make_material("Flame", (1.0, 0.75, 0.35), emission=4.0)
MAT_IRON = make_material("Iron", (0.08, 0.08, 0.09), metallic=0.7, roughness=0.4)

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
build_dome_lattice(ROOM_W / 2.0, ROOM_D / 2.0, WALL_H - 0.2, min(ROOM_W, ROOM_D) * 0.45)
build_cherub_statue(ROOM_W / 2.0 - 1.7, 2.35, 0.0)

bpy.ops.object.light_add(type="POINT", location=P(ROOM_W / 2.0, ROOM_D - 1.5, GALLERY_Y + 1.6))
light = bpy.context.active_object
light.data.energy = 400
light.data.color = (1.0, 0.85, 0.6)

bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB")
print(f"WROTE {OUT}")
