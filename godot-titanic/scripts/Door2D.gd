extends Area2D

@export var target_scene := ""


func _on_body_entered(body: Node) -> void:
	if not body.is_in_group("player"):
		return
	if target_scene == "" or not ResourceLoader.exists(target_scene):
		print("Door: ", target_scene, " not ported yet")
		return
	# Freeing the current scene (and its physics/collision nodes, including
	# this Area2D's own body) while still inside the body_entered physics
	# callback isn't allowed -- defer it to run right after the step ends.
	get_tree().call_deferred("change_scene_to_file", target_scene)
