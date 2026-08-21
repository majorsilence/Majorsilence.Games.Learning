extends Node3D

@export var scene_path: String = "res://scenes/rooms_3d/grand-stair-escape.tscn"


func _ready() -> void:
	var scene: PackedScene = load(scene_path)
	var room := scene.instantiate()
	add_child(room)
	_dump(room, 0)
	get_tree().quit()


func _dump(n: Node, depth: int) -> void:
	var extra := ""
	if n is Node3D:
		var n3 := n as Node3D
		extra = " pos=" + str(n3.global_position) + " scale=" + str(n3.scale) + " gscale=" + str(n3.global_transform.basis.get_scale())
	if n is MeshInstance3D:
		var mi := n as MeshInstance3D
		if mi.material_override:
			extra += " override_mat=" + mi.material_override.resource_path + "/" + mi.material_override.get_class()
		elif mi.mesh and mi.mesh.get_surface_count() > 0:
			var m = mi.get_active_material(0)
			extra += " active_mat=" + (m.get_class() if m else "null")
	if n is CollisionShape3D:
		var shape = (n as CollisionShape3D).shape
		if shape:
			extra += " shape=" + shape.get_class()
			if shape is BoxShape3D:
				extra += " size=" + str((shape as BoxShape3D).size)
			if shape is ConcavePolygonShape3D:
				var faces = (shape as ConcavePolygonShape3D).get_faces()
				var minv = Vector3(INF, INF, INF)
				var maxv = Vector3(-INF, -INF, -INF)
				for v in faces:
					minv = minv.min(v)
					maxv = maxv.max(v)
				extra += " local_aabb=[" + str(minv) + " .. " + str(maxv) + "]"
	print("  ".repeat(depth), n.name, " (", n.get_class(), ")", extra)
	for c in n.get_children():
		_dump(c, depth + 1)
