extends Control


func _on_room2d_pressed() -> void:
	get_tree().change_scene_to_file("res://scenes/Room2D.tscn")


func _on_room3d_pressed() -> void:
	get_tree().change_scene_to_file("res://scenes/Room3D.tscn")
