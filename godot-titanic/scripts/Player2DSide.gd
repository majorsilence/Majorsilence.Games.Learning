extends CharacterBody2D

# Mirrors Majorsilence.Games.Core.Physics.PlatformerBody's constants exactly.
const SPEED := 120.0
const GRAVITY := 1500.0
const JUMP_VELOCITY := -480.0
const MAX_FALL_SPEED := 700.0
const CLIMB_SPEED := 110.0
const ONE_WAY_LAYER := 2

var _ladder_count := 0

@onready var _sprite: AnimatedSprite2D = $Sprite


func _ready() -> void:
	add_to_group("player")


func _physics_process(delta: float) -> void:
	var input_dir := _horizontal_input()
	var vertical := _vertical_input()
	var on_ladder := _ladder_count > 0

	if on_ladder and vertical != 0.0:
		velocity.y = vertical * CLIMB_SPEED
	elif not is_on_floor():
		velocity.y = min(velocity.y + GRAVITY * delta, MAX_FALL_SPEED)
	elif velocity.y > 0.0:
		velocity.y = 0.0

	velocity.x = input_dir * SPEED

	if Input.is_action_just_pressed("ui_accept"):
		if on_ladder:
			_ladder_count = 0
			velocity.y = JUMP_VELOCITY
		elif is_on_floor() and vertical > 0.0:
			_drop_through()
		elif is_on_floor():
			velocity.y = JUMP_VELOCITY

	if input_dir != 0.0:
		_sprite.flip_h = input_dir < 0.0
		if not _sprite.is_playing():
			_sprite.play("walk")
	else:
		_sprite.stop()

	move_and_slide()


func _drop_through() -> void:
	# Classic Down+Jump: briefly stop colliding with one-way platforms so the
	# body falls through the one it's standing on (RequestDropThrough).
	set_collision_mask_value(ONE_WAY_LAYER, false)
	await get_tree().create_timer(0.25).timeout
	set_collision_mask_value(ONE_WAY_LAYER, true)


func _horizontal_input() -> float:
	# ui_left/right are arrow-keys-only in Godot's built-in InputMap --
	# WASD is added here directly (raw physical-key checks).
	var v := Input.get_axis("ui_left", "ui_right")
	if Input.is_physical_key_pressed(KEY_A):
		v -= 1.0
	if Input.is_physical_key_pressed(KEY_D):
		v += 1.0
	return clamp(v, -1.0, 1.0)


func _vertical_input() -> float:
	var v := Input.get_axis("ui_up", "ui_down")
	if Input.is_physical_key_pressed(KEY_W):
		v -= 1.0
	if Input.is_physical_key_pressed(KEY_S):
		v += 1.0
	return clamp(v, -1.0, 1.0)


func _ladder_enter() -> void:
	_ladder_count += 1


func _ladder_exit() -> void:
	_ladder_count = max(0, _ladder_count - 1)


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		get_tree().change_scene_to_file("res://Main.tscn")
