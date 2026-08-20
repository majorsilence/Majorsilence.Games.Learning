extends Area2D


func _on_body_entered(body: Node) -> void:
	if body.is_in_group("player") and body.has_method("_ladder_enter"):
		body._ladder_enter()


func _on_body_exited(body: Node) -> void:
	if body.is_in_group("player") and body.has_method("_ladder_exit"):
		body._ladder_exit()
