extends CharacterBody3D

const SPEED := 4.0
const GRAVITY := 9.8


func _ready() -> void:
	add_to_group("player")
	$Camera3D.look_at(global_position, Vector3.UP)


func _physics_process(delta: float) -> void:
	if not is_on_floor():
		velocity.y -= GRAVITY * delta
	else:
		velocity.y = 0.0

	var input_dir := Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
	velocity.x = input_dir.x * SPEED
	velocity.z = input_dir.y * SPEED
	move_and_slide()


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		get_tree().change_scene_to_file("res://Main.tscn")
