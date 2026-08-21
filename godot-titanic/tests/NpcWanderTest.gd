extends Node3D

## Confirms NPCs actually move over time (not just that the script parses
## without error) and stay on their short leash instead of drifting away
## unbounded. Prints PROBE PASS/FAIL like PhysicsProbe.gd, but drives no
## input -- there's nothing to hold, NpcIdle.gd moves on its own.

@export var scene_path: String = "res://scenes/rooms_3d/crows-nest.tscn"
@export var npc_node_path: String = "Entity_npc_watcher_r2_c3"
@export var test_frames: int = 400
@export var leash_radius: float = 1.0

var _npc: Node3D
var _home := Vector3.ZERO
var _moved := false
var _frames := 0


func _ready() -> void:
	var scene: PackedScene = load(scene_path)
	var room := scene.instantiate()
	add_child(room)
	_npc = room.get_node(npc_node_path)
	_home = _npc.position


func _physics_process(_delta: float) -> void:
	_frames += 1
	if _npc.position.distance_to(_home) > 0.05:
		_moved = true
	if _frames < test_frames:
		return
	var drift := Vector2(_npc.position.x - _home.x, _npc.position.z - _home.z).length()
	if not _moved:
		print("PROBE FAIL: NPC never moved from spawn (", _home, ")")
	elif drift > leash_radius:
		print("PROBE FAIL: NPC drifted ", drift, " units from spawn, past leash_radius=", leash_radius)
	else:
		print("PROBE PASS: NPC moved (drift=", drift, ") and stayed on its leash")
	get_tree().quit()
