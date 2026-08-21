extends Node3D

## Confirms the player actually leaves the ground on a single space-bar
## press and comes back down (not just that the script parses). Tracks
## peak height reached, since PhysicsProbe.gd's final-position check would
## miss a jump that's already landed again by the time the frame budget
## runs out.

@export var scene_path: String = "res://scenes/rooms_3d/crows-nest.tscn"
@export var test_frames: int = 120
@export var min_peak_rise: float = 0.15

var _player: CharacterBody3D
var _start_y := 0.0
var _peak_rise := 0.0
var _frames := 0


func _ready() -> void:
	var scene: PackedScene = load(scene_path)
	var room := scene.instantiate()
	add_child(room)
	_player = room.get_node("Player")
	_start_y = _player.global_position.y
	var press := InputEventKey.new()
	press.physical_keycode = KEY_SPACE
	press.pressed = true
	Input.parse_input_event(press)


func _physics_process(_delta: float) -> void:
	_frames += 1
	_peak_rise = max(_peak_rise, _player.global_position.y - _start_y)
	if _frames < test_frames:
		return
	var final_rise := _player.global_position.y - _start_y
	if _peak_rise < min_peak_rise:
		print("PROBE FAIL: peak rise ", _peak_rise, " below min_peak_rise=", min_peak_rise, " (never left the ground)")
	elif abs(final_rise) > 0.05:
		print("PROBE FAIL: didn't land back near start (final_rise=", final_rise, "), peak_rise=", _peak_rise)
	else:
		print("PROBE PASS: jumped (peak_rise=", _peak_rise, ") and landed (final_rise=", final_rise, ")")
	get_tree().quit()
