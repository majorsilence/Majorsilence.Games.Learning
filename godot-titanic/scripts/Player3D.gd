extends CharacterBody3D

const SPEED := 4.0
const GRAVITY := 9.8
const MOUSE_SENSITIVITY := 0.0025
const MAX_PITCH := deg_to_rad(80.0)
# Mirrors Game.cs's player Animation(frames: [0,1,2,3], frameDurationMs: 150).
const FRAME_COUNT := 4
const FRAME_DURATION := 0.15

const CAMERA_FIRST_PERSON := Vector3(0, 0.85, 0)
const CAMERA_THIRD_PERSON := Vector3(0, 1.6, 3.2)
const ARM_REST := Vector3(0.32, -0.32, -0.45)

enum ViewMode { FIRST_PERSON, THIRD_PERSON }

var _anim_time := 0.0
var _pitch := 0.0
var _view_mode := ViewMode.FIRST_PERSON

@onready var _sprite: Sprite3D = $Sprite
@onready var _camera: Camera3D = $Camera3D
@onready var _arm: Node3D = $Camera3D/Arm


func _ready() -> void:
	add_to_group("player")
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	_apply_view_mode()


func _physics_process(delta: float) -> void:
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0

	# Movement is relative to which way the body (== camera yaw) is
	# currently facing, not fixed world axes -- turning comes from
	# mouse-look now, so "forward" has to follow it.
	var input_dir := _movement_input()
	var forward := -global_transform.basis.z
	var right := global_transform.basis.x
	var move := forward * -input_dir.y + right * input_dir.x
	move.y = 0.0
	if move.length() > 0.0:
		move = move.normalized() * SPEED
	velocity.x = move.x
	velocity.z = move.z
	move_and_slide()

	if Vector2(velocity.x, velocity.z).length() > 0.1:
		_anim_time += delta
		_sprite.frame = int(_anim_time / FRAME_DURATION) % FRAME_COUNT
	else:
		_anim_time = 0.0
		_sprite.frame = 0

	# Minecraft-style arm swing: a small bob tied to the same walk-cycle
	# timer the sprite's own frame animation already uses (_anim_time),
	# so the arm and the legs read as one stride instead of two unrelated
	# animations. Idle (_anim_time reset to 0 above) collapses back to
	# ARM_REST exactly, no separate idle-state handling needed.
	_arm.position = ARM_REST + Vector3(0.0, sin(_anim_time * 10.0) * 0.015, -abs(sin(_anim_time * 5.0)) * 0.02)


func _movement_input() -> Vector2:
	# ui_left/right/up/down are arrow-keys-only in Godot's built-in
	# InputMap -- WASD is added here directly (raw physical-key checks)
	# rather than by editing project.godot's input map, so movement keeps
	# working regardless of the project's input settings.
	var v := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	if Input.is_physical_key_pressed(KEY_A):
		v.x -= 1.0
	if Input.is_physical_key_pressed(KEY_D):
		v.x += 1.0
	if Input.is_physical_key_pressed(KEY_W):
		v.y -= 1.0
	if Input.is_physical_key_pressed(KEY_S):
		v.y += 1.0
	return v.limit_length(1.0)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		rotate_y(-event.relative.x * MOUSE_SENSITIVITY)
		_pitch = clamp(_pitch - event.relative.y * MOUSE_SENSITIVITY, -MAX_PITCH, MAX_PITCH)
		_camera.rotation.x = _pitch

	if event is InputEventKey and event.pressed and not event.echo and event.physical_keycode == KEY_F5:
		_view_mode = ViewMode.THIRD_PERSON if _view_mode == ViewMode.FIRST_PERSON else ViewMode.FIRST_PERSON
		_apply_view_mode()

	if event.is_action_pressed("ui_cancel"):
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
		get_tree().change_scene_to_file("res://Main.tscn")


func _apply_view_mode() -> void:
	# Minecraft-style F5 toggle: first person shows the arm (hides the
	# player's own billboard, which would otherwise just fill the screen
	# facing the camera); third person is the reverse, camera pulled back
	# over the shoulder instead of sitting at eye level.
	var first := _view_mode == ViewMode.FIRST_PERSON
	_camera.position = CAMERA_FIRST_PERSON if first else CAMERA_THIRD_PERSON
	_sprite.visible = not first
	_arm.visible = first
