"""
Builds a batch of small entity-replacement props (funnel, mast, iceberg,
ship's wheel, boiler) to swap in for their flat billboard Sprite3D in 3D
rooms, same idea as build_lifeboat.py. Run headless:

    blender --background --python build_props.py

Same (x, depth, height) -> P() axis convention as build_hull.py /
build_grand_staircase.py / build_lifeboat.py (Blender's exporter sends
Blender Y to glTF's -Z; routing every raw vertex/location tuple through one
P() keeps every model's "up" and "depth" consistent). Each model is reset
to a clean scene and exported to its own .glb before the next is built.
"""
import math
import random

import bpy
import bmesh

OUT_DIR = "/home/peter/source/repos/Majorsilence.Games.Learning/godot-titanic/assets/models"


def P(x, depth, height):
    return (x, -depth, height)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def make_material(name, color, roughness=0.6, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return mat


def export(name):
    path = f"{OUT_DIR}/{name}.glb"
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB")
    print(f"WROTE {path}")


# ---------------------------------------------------------------- funnel --
def build_funnel():
    reset_scene()
    radius, height = 0.42, 2.6
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=height, location=P(0, 0, height / 2.0))
    body = bpy.context.active_object
    body.name = "FunnelBody"
    body.data.materials.append(make_material("FunnelBuff", (0.72, 0.55, 0.32)))

    bpy.ops.mesh.primitive_cylinder_add(radius=radius + 0.03, depth=0.32, location=P(0, 0, height + 0.1))
    cap = bpy.context.active_object
    cap.name = "FunnelCap"
    cap.data.materials.append(make_material("FunnelBlack", (0.03, 0.03, 0.035)))

    export("funnel")


# ------------------------------------------------------------------ mast --
def build_mast():
    reset_scene()
    height = 2.0
    bpy.ops.mesh.primitive_cone_add(radius1=0.08, radius2=0.025, depth=height, location=P(0, 0, height / 2.0))
    pole = bpy.context.active_object
    pole.name = "MastPole"
    pole.data.materials.append(make_material("MastDark", (0.12, 0.11, 0.1)))

    nest_h = height * 0.72
    bpy.ops.mesh.primitive_torus_add(major_radius=0.16, minor_radius=0.03, location=P(0, 0, nest_h))
    nest = bpy.context.active_object
    nest.name = "CrowsNestRing"
    nest.data.materials.append(make_material("NestWood", (0.35, 0.24, 0.14)))

    bpy.ops.mesh.primitive_cylinder_add(radius=0.14, depth=0.05, location=P(0, 0, nest_h - 0.05))
    floor = bpy.context.active_object
    floor.name = "CrowsNestFloor"
    floor.data.materials.append(make_material("NestFloor", (0.3, 0.2, 0.12)))

    export("mast")


# --------------------------------------------------------------- iceberg --
def build_iceberg():
    reset_scene()
    random.seed(7)
    bpy.ops.mesh.primitive_ico_sphere_add(radius=0.7, subdivisions=2, location=P(0, 0, 0))
    berg = bpy.context.active_object
    berg.name = "Iceberg"

    bm = bmesh.new()
    bm.from_mesh(berg.data)
    for v in bm.verts:
        jitter = 1.0 + random.uniform(-0.18, 0.18)
        v.co.x *= jitter
        v.co.y *= jitter
        # Squash into a low, jagged floe rather than a round ball, and lift
        # so its lowest point sits at height 0 (waterline/ground anchor).
        v.co.z = v.co.z * 0.55 * (1.0 + random.uniform(-0.15, 0.15))
    min_z = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= min_z
    bm.normal_update()
    bm.to_mesh(berg.data)
    bm.free()
    berg.data.materials.append(make_material("IceMat", (0.75, 0.86, 0.9), roughness=0.25))

    export("iceberg")


# ----------------------------------------------------------- ship wheel --
def build_wheel():
    reset_scene()
    hub_h = 0.55  # pedestal height up to the wheel's hub
    radius = 0.32

    bpy.ops.mesh.primitive_cylinder_add(radius=0.08, depth=hub_h, location=P(0, 0, hub_h / 2.0))
    pedestal = bpy.context.active_object
    pedestal.name = "WheelPedestal"
    pedestal.data.materials.append(make_material("Brass", (0.55, 0.42, 0.18), roughness=0.35, metallic=0.6))

    # Rim: a torus, standing vertical (facing along the depth/-Y axis) so
    # it reads as a wheel a player stands in front of and turns, not a
    # dinner plate lying flat on the deck.
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=0.03, location=P(0, 0, hub_h))
    rim = bpy.context.active_object
    rim.rotation_euler = (math.radians(90), 0, 0)
    rim.name = "WheelRim"
    rim.data.materials.append(make_material("WheelWood", (0.32, 0.2, 0.11)))

    spoke_mat = make_material("WheelSpoke", (0.28, 0.17, 0.09))
    n_spokes = 8
    for i in range(n_spokes):
        ang = 2 * math.pi * i / n_spokes
        mx, mz = radius * 0.5 * math.cos(ang), radius * 0.5 * math.sin(ang)
        bpy.ops.mesh.primitive_cylinder_add(radius=0.015, depth=radius, location=P(mx, 0, hub_h + mz))
        spoke = bpy.context.active_object
        spoke.rotation_euler = (math.radians(90), 0, ang + math.radians(90))
        spoke.name = f"Spoke{i}"
        spoke.data.materials.append(spoke_mat)
        if i % 2 == 0:
            bpy.ops.mesh.primitive_cylinder_add(radius=0.025, depth=0.09, location=P(mx * 1.65, 0, hub_h + mz * 1.65))
            handle = bpy.context.active_object
            handle.rotation_euler = (0, math.radians(90), 0)
            handle.name = f"Handle{i}"
            handle.data.materials.append(spoke_mat)

    bpy.ops.mesh.primitive_cylinder_add(radius=0.06, depth=0.12, location=P(0, -0.06, hub_h))
    hub = bpy.context.active_object
    hub.rotation_euler = (math.radians(90), 0, 0)
    hub.name = "WheelHub"
    hub.data.materials.append(make_material("Brass2", (0.55, 0.42, 0.18), roughness=0.35, metallic=0.6))

    export("wheel")


# ----------------------------------------------------------------- boiler --
def build_boiler():
    reset_scene()
    radius, height = 0.55, 1.15
    bpy.ops.mesh.primitive_cylinder_add(radius=radius, depth=height, location=P(0, 0, height / 2.0))
    drum = bpy.context.active_object
    drum.rotation_euler = (math.radians(90), 0, 0)
    drum.name = "BoilerDrum"
    drum.data.materials.append(make_material("BoilerIron", (0.22, 0.21, 0.2), roughness=0.55, metallic=0.3))

    # A couple of raised rivet-seam rings around the drum.
    band_mat = make_material("BoilerBand", (0.15, 0.14, 0.13), roughness=0.6, metallic=0.3)
    for frac in (0.3, 0.7):
        bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=0.02, location=P(0, height * frac - height / 2.0, height / 2.0))
        band = bpy.context.active_object
        band.rotation_euler = (math.radians(90), 0, 0)
        band.name = f"Band{int(frac * 100)}"
        band.data.materials.append(band_mat)

    # Furnace door: a dark disc set into the front face (the -depth side).
    bpy.ops.mesh.primitive_cylinder_add(radius=radius * 0.45, depth=0.06, location=P(0, -radius - 0.02, height * 0.32))
    door = bpy.context.active_object
    door.rotation_euler = (math.radians(90), 0, 0)
    door.name = "FurnaceDoor"
    door.data.materials.append(make_material("FurnaceDoor", (0.05, 0.04, 0.03), roughness=0.4))

    export("boiler")


build_funnel()
build_mast()
build_iceberg()
build_wheel()
build_boiler()
print("ALL PROPS DONE")
