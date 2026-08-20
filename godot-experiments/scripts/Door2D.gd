extends Area2D

@export var target_scene := "res://scenes/Room3D.tscn"


func _on_body_entered(body: Node) -> void:
	if body.is_in_group("player"):
		get_tree().change_scene_to_file(target_scene)
