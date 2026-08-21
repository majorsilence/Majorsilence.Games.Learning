"""
Builds a stylized, recognizable Titanic hull (tapered black hull, 4 funnels,
2 masts) sized to underlie/surround the existing flat 80x20 deck grid in
godot-titanic's 3D "titanic" scene (X: 0..19 = columns, Z: 0..79 = rows,
deck surface at Y=0). Run headless:

    blender --background --python build_hull.py

Coordinate convention: everywhere in this script, positions are expressed
as (x, depth, height) in "Godot/deck" terms -- depth 0 = bow end (matches
the deck's row 0), increasing toward the stern (matches increasing row),
height 0 = deck surface. All of it goes through P() before touching bpy,
which does the one conversion that actually matters: Blender's glTF
exporter converts Z-up to Y-up by sending Blender's Y axis to glTF's
*negative* Z (verified by inspecting a test export's JSON) -- so P negates
depth going into Blender's Y slot, and everything comes out the right way
around and the right way up in Godot once imported.
"""
import bpy
import bmesh
import math
import mathutils

OUT = "/home/peter/source/repos/Majorsilence.Games.Learning/godot-titanic/assets/models/titanic_hull.glb"

DECK_W = 19.0
DECK_LEN = 79.0
CENTER_X = DECK_W / 2.0
BOW_Z = -3.0
STERN_Z = DECK_LEN + 3.0
BEAM = DECK_W + 3.0
HALF_BEAM = BEAM / 2.0
HULL_DEPTH = 6.0


def P(x, depth, height):
    return (x, -depth, height)


bpy.ops.wm.read_factory_settings(use_empty=True)


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def make_material(name, color, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.6
    if emission > 0.0:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    return mat


def half_width_at(depth):
    bow_taper_end = 14.0
    stern_taper_start = DECK_LEN - 10.0
    if depth <= BOW_Z:
        return 0.0
    if depth < bow_taper_end:
        t = (depth - BOW_Z) / (bow_taper_end - BOW_Z)
        return HALF_BEAM * (t ** 1.6)
    if depth <= stern_taper_start:
        return HALF_BEAM
    if depth < STERN_Z:
        t = (depth - stern_taper_start) / (STERN_Z - stern_taper_start)
        return HALF_BEAM * (1.0 - t ** 1.3) + 0.6 * (1.0 - t)
    return 0.0


def hull_draft_at(depth):
    if depth <= BOW_Z + 1.0 or depth >= STERN_Z - 1.0:
        return 0.4
    fade = min(1.0, (depth - BOW_Z) / 6.0, (STERN_Z - depth) / 6.0)
    return HULL_DEPTH * fade


def cross_section(depth):
    """7 points (x, depth, height), left-to-right across the beam, rounded
    V hull profile from deck edge (height 0) down to the keel."""
    hw = half_width_at(depth)
    d = hull_draft_at(depth)
    if hw <= 0.001:
        return [P(CENTER_X, depth, 0.0)] * 7
    xs = [-1.0, -0.8, -0.4, 0.0, 0.4, 0.8, 1.0]
    hs = [0.0, -0.35 * d, -0.8 * d, -d, -0.8 * d, -0.35 * d, 0.0]
    return [P(CENTER_X + hw * x, depth, h) for x, h in zip(xs, hs)]


def build_hull():
    mesh = bpy.data.meshes.new("HullMesh")
    obj = bpy.data.objects.new("Hull", mesh)
    bpy.context.collection.objects.link(obj)

    bm = bmesh.new()
    rings = []
    depths = [BOW_Z]
    d = BOW_Z
    while d < STERN_Z:
        d += 2.5
        depths.append(min(d, STERN_Z))
    if depths[-1] != STERN_Z:
        depths.append(STERN_Z)

    for depth in depths:
        rings.append([bm.verts.new(pt) for pt in cross_section(depth)])

    bm.verts.ensure_lookup_table()
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for j in range(len(a) - 1):
            try:
                bm.faces.new((a[j], a[j + 1], b[j + 1], b[j]))
            except ValueError:
                pass
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        try:
            bm.faces.new((a[0], b[0], b[-1], a[-1]))
        except ValueError:
            pass

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(make_material("HullBlack", (0.03, 0.03, 0.035)))
    return obj


def build_funnel(depth, index):
    # Real White Star Line livery (see reference photos): buff/cream body
    # for most of the height, a black cap only at the very top -- this had
    # the two swapped (mostly black with a thin buff band), the opposite
    # of every reference image.
    bpy.ops.mesh.primitive_cylinder_add(radius=2.0, depth=8.0, location=P(CENTER_X, depth, 4.0))
    body = bpy.context.active_object
    body.name = f"Funnel{index}_Body"
    body.data.materials.append(make_material(f"FunnelBuff{index}", (0.78, 0.6, 0.35)))

    bpy.ops.mesh.primitive_cylinder_add(radius=2.05, depth=1.4, location=P(CENTER_X, depth, 7.7))
    cap = bpy.context.active_object
    cap.name = f"Funnel{index}_Cap"
    cap.data.materials.append(make_material(f"FunnelBlack{index}", (0.04, 0.04, 0.045)))
    return [body, cap]


def build_mast(depth, height, index):
    bpy.ops.mesh.primitive_cone_add(radius1=0.35, radius2=0.12, depth=height, location=P(CENTER_X, depth, height / 2.0))
    mast = bpy.context.active_object
    mast.name = f"Mast{index}"
    mast.data.materials.append(make_material(f"MastMat{index}", (0.15, 0.14, 0.13)))
    return mast


def build_sheer_line():
    """A thin gold/buff stripe hugging both sides of the hull right at the
    deck edge -- the line separating black hull from white superstructure
    in every reference photo, and one of the two or three things that
    single-handedly reads as "Titanic" at a glance. Follows the hull's own
    tapering contour (same half_width_at()) rather than running the full
    beam width, so it narrows into the bow/stern with the hull instead of
    poking through it."""
    mesh = bpy.data.meshes.new("SheerLineMesh")
    obj = bpy.data.objects.new("SheerLine", mesh)
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    y_top, y_bot = -0.05, -0.3
    depths = [BOW_Z]
    d = BOW_Z
    while d < STERN_Z:
        d += 2.5
        depths.append(min(d, STERN_Z))
    if depths[-1] != STERN_Z:
        depths.append(STERN_Z)

    for side in (-1, 1):
        ring = []
        for depth in depths:
            hw = half_width_at(depth)
            x = CENTER_X + side * hw
            ring.append((bm.verts.new(P(x, depth, y_top)), bm.verts.new(P(x, depth, y_bot))))
        for i in range(len(ring) - 1):
            t0, b0 = ring[i]
            t1, b1 = ring[i + 1]
            try:
                bm.faces.new((t0, t1, b1, b0) if side > 0 else (b0, b1, t1, t0))
            except ValueError:
                pass

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(make_material("SheerLineGold", (0.75, 0.6, 0.3)))
    return obj


def build_portholes():
    """Rows of small emissive discs along both sides of the black hull --
    the glowing-windows-at-night look every reference photo leans on.
    Skipped near the bow/stern where the hull tapers to nothing (hw <= 1)
    so discs don't poke out past the hull's own surface. Built into one
    shared mesh (bmesh.ops.create_circle per instance, one shared
    material) instead of one bpy object each -- a few dozen individual
    objects/materials would still export fine, but there's no reason to
    pay that draw-call cost for flat little discs."""
    mesh = bpy.data.meshes.new("PortholesMesh")
    obj = bpy.data.objects.new("Portholes", mesh)
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    count = 0
    d = BOW_Z + 6.0
    while d < STERN_Z - 6.0:
        hw = half_width_at(d)
        if hw > 1.0:
            for row_h in (-0.6, -1.35):
                for side in (-1, 1):
                    x = CENTER_X + side * (hw + 0.02)
                    loc = mathutils.Vector(P(x, d, row_h))
                    rot = mathutils.Matrix.Rotation(math.radians(90), 4, "Y")
                    mat = mathutils.Matrix.Translation(loc) @ rot
                    bmesh.ops.create_circle(bm, cap_ends=True, segments=10, radius=0.12, matrix=mat)
                    count += 1
        d += 2.2

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(make_material("PortholeGlow", (1.0, 0.85, 0.55), emission=3.0))
    print(f"Built {count} portholes")
    return obj


def build_superstructure():
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=P(CENTER_X, DECK_LEN * 0.5, 1.5))
    obj = bpy.context.active_object
    obj.name = "Superstructure"
    obj.scale = (DECK_W * 0.7 / 2.0, DECK_LEN * 0.55 / 2.0, 3.0)
    obj.data.materials.append(make_material("SuperstructureWhite", (0.82, 0.8, 0.76)))
    return obj


clear_scene()
build_hull()
build_sheer_line()
build_portholes()
for i, fd in enumerate([DECK_LEN * 0.32, DECK_LEN * 0.44, DECK_LEN * 0.56, DECK_LEN * 0.68]):
    build_funnel(fd, i)
build_mast(DECK_LEN * 0.12, 11.0, 0)
build_mast(DECK_LEN * 0.88, 9.0, 1)
build_superstructure()

bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB")
print(f"WROTE {OUT}")
