extends Node3D

## Automated headless physics smoke test: loads a 3D room scene, holds a
## direction key for a fixed number of physics frames, then checks the
## player's final position against a pass/fail predicate. Exists because
## the normal verification recipe (--quit-after N on a scene, grep for
## errors) never actually simulates movement, so a real collision bug
## (walking through a wall, getting stuck on stairs) can't be caught by
## it -- these tests actually drive the CharacterBody3D via a synthetic
## held key (Input.parse_input_event), the same one Player3D.gd reads via
## Input.is_physical_key_pressed.
##
## Configure via the exported fields on the instancing .tscn, then run:
##   godot --headless --path . tests/<Name>.tscn
## It prints "PROBE PASS: ..." or "PROBE FAIL: ..." and quits itself.

@export var scene_path: String = ""
@export var hold_key: Key = KEY_W
@export var test_frames: int = 300
@export_multiline var pass_condition: String = ""  # GDScript expression, `pos` is the player's final global_position

var _player: CharacterBody3D
var _frames := 0


func _ready() -> void:
	var scene: PackedScene = load(scene_path)
	var room := scene.instantiate()
	add_child(room)
	_player = room.get_node("Player")
	var press := InputEventKey.new()
	press.physical_keycode = hold_key
	press.pressed = true
	Input.parse_input_event(press)


func _physics_process(_delta: float) -> void:
	_frames += 1
	if _frames < test_frames:
		return
	var pos := _player.global_position
	var expr := Expression.new()
	var err := expr.parse(pass_condition, ["pos"])
	if err != OK:
		print("PROBE FAIL: bad pass_condition expression: ", pass_condition)
		get_tree().quit(1)
		return
	var result = expr.execute([pos], self)
	if expr.has_execute_failed():
		print("PROBE FAIL: pass_condition failed to execute")
		get_tree().quit(1)
		return
	if result:
		print("PROBE PASS: pos=", pos)
	else:
		print("PROBE FAIL: pos=", pos, " did not satisfy: ", pass_condition)
	get_tree().quit()
