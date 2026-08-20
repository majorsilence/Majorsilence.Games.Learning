"""
Builds a small, simple lifeboat (pointed at both ends, shallow open hull,
white/cream) to replace the flat billboard sprite for "lifeboat" entities
in 3D rooms. Run headless:

    blender --background --python build_lifeboat.py

Same (x, depth, height) -> P() axis convention as build_hull.py /
build_grand_staircase.py -- see build_hull.py's docstring for why.
"""
import bpy
import bmesh

OUT = "/home/peter/source/repos/Majorsilence.Games.Learning/godot-titanic/assets/models/lifeboat.glb"

LENGTH = 1.6   # along the boat's own long axis (mapped to model X after a
                # 90-degree yaw so it lies broadside, matching how the
                # existing 2D-derived lifeboat entities sit on deck)
HALF_LEN = LENGTH / 2.0
BEAM = 0.5
HALF_BEAM = BEAM / 2.0
DEPTH = 0.22


def P(x, depth, height):
    return (x, -depth, height)


bpy.ops.wm.read_factory_settings(use_empty=True)
for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)


def make_material(name, color, roughness=0.6):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


MAT_HULL = make_material("LifeboatHull", (0.85, 0.83, 0.76))

# Cross-section (along "depth" = the boat's beam axis here) at a given
# position along its length: rounded-bottom open hull, half-points mirrored
# left/right, y=0 at gunwale (top edge).
XS = [-1.0, -0.6, 0.0, 0.6, 1.0]
YS = [0.0, -0.7 * DEPTH, -DEPTH, -0.7 * DEPTH, 0.0]


def taper(t):
    """1.0 at midship, tapering to ~0 at the pointed bow/stern (t in -1..1)."""
    return max(0.05, 1.0 - abs(t) ** 1.4)


def section(x_pos, t):
    hb = HALF_BEAM * taper(t)
    return [P(x_pos, hb * xf, hy) for xf, hy in zip(XS, YS)]


mesh = bpy.data.meshes.new("LifeboatMesh")
obj = bpy.data.objects.new("Lifeboat", mesh)
bpy.context.collection.objects.link(obj)

bm = bmesh.new()
n = 14
rings = []
for i in range(n + 1):
    t = -1.0 + 2.0 * i / n
    x_pos = t * HALF_LEN
    rings.append([bm.verts.new(pt) for pt in section(x_pos, t)])

for i in range(len(rings) - 1):
    a, b = rings[i], rings[i + 1]
    for j in range(len(a) - 1):
        try:
            bm.faces.new((a[j], a[j + 1], b[j + 1], b[j]))
        except ValueError:
            pass
# Gunwale rim (top edge run) so the hull isn't open along the top edges.
for i in range(len(rings) - 1):
    a, b = rings[i], rings[i + 1]
    try:
        bm.faces.new((a[0], b[0], b[-1], a[-1]))
    except ValueError:
        pass

bm.normal_update()
bm.to_mesh(mesh)
bm.free()
mesh.materials.append(MAT_HULL)

bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB")
print(f"WROTE {OUT}")
