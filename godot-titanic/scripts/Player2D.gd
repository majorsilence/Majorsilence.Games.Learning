extends CharacterBody2D

const SPEED := 100.0
const TILE_W := 32.0
const TILE_H := 16.0

# Movement follows the isometric grid's column/row axes (same basis as
# IsometricGrid.TileToWorld), not raw screen axes, so arrow keys feel right
# against the diamond projection.
var _col_dir := Vector2(TILE_W / 2.0, TILE_H / 2.0).normalized()
var _row_dir := Vector2(-TILE_W / 2.0, TILE_H / 2.0).normalized()

@onready var _sprite: AnimatedSprite2D = $Sprite


func _ready() -> void:
	add_to_group("player")


func _physics_process(_delta: float) -> void:
	var input_vector := _movement_input()
	velocity = (_col_dir * input_vector.x + _row_dir * input_vector.y) * SPEED
	move_and_slide()

	if velocity.length() > 0.1:
		if not _sprite.is_playing():
			_sprite.play("walk")
	else:
		_sprite.stop()


func _movement_input() -> Vector2:
	# ui_left/right/up/down are arrow-keys-only in Godot's built-in
	# InputMap -- WASD is added here directly (raw physical-key checks)
	# rather than by editing project.godot's input map.
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
	if event.is_action_pressed("ui_cancel"):
		get_tree().change_scene_to_file("res://Main.tscn")
