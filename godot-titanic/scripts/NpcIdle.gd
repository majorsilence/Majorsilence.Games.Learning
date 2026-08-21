extends Node3D

## A completely motionless figure reads as a mannequin, not a person.
## Small idle sway + a slow head turn (cheap -- no animation data, just a
## couple of sine waves), plus a slow wander within a short leash of its
## own spawn point so a room doesn't feel like a diorama. Each NPC gets
## its own random phase/timing so a room full of them doesn't move in
## lockstep. No collision avoidance -- these are plain Node3D, not
## CharacterBody3D -- so the wander radius is kept small (well under a
## tile) rather than trying to path around walls it has no way to sense.

@export var wander_radius: float = 0.5
@export var wander_speed: float = 0.35
@export var wander_interval_min: float = 3.0
@export var wander_interval_max: float = 6.5

@onready var _head: Node3D = $Head
var _phase := randf() * TAU
var _base_y := 0.0
var _home_xz := Vector2.ZERO
var _target_xz := Vector2.ZERO
var _wander_timer := 0.0
var _facing := 0.0


func _ready() -> void:
	# Bob/wander relative to wherever the importer placed this NPC
	# (ground_y for its tile, and its own spawn XZ) -- overwriting
	# position outright would erase that and drop the NPC to (near) world
	# origin instead of its own floor/tile.
	_base_y = position.y
	_home_xz = Vector2(position.x, position.z)
	_target_xz = _home_xz
	_wander_timer = randf_range(wander_interval_min, wander_interval_max)
	_facing = rotation.y


func _physics_process(delta: float) -> void:
	var t := Time.get_ticks_msec() / 1000.0 + _phase

	_wander_timer -= delta
	if _wander_timer <= 0.0:
		var angle := randf() * TAU
		var r := randf() * wander_radius
		_target_xz = _home_xz + Vector2(cos(angle), sin(angle)) * r
		_wander_timer = randf_range(wander_interval_min, wander_interval_max)

	var current_xz := Vector2(position.x, position.z)
	var to_target := _target_xz - current_xz
	var dist := to_target.length()
	if dist > 0.02:
		var dir := to_target.normalized()
		current_xz += dir * min(dist, wander_speed * delta)
		position.x = current_xz.x
		position.z = current_xz.y
		_facing = atan2(dir.x, dir.y)

	rotation.y = _facing + sin(t * 0.35) * 0.1
	position.y = _base_y + sin(t * 1.3) * 0.01
	if _head:
		_head.rotation.y = sin(t * 0.6) * 0.18
