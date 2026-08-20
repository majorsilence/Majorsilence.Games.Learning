extends Control

# Rebuilt as rooms get ported -- keeps Main.tscn from needing hand-edited
# buttons every time an importer adds a room.
const ROOMS := [
	"crows-nest", "titanic", "bridge", "engine-room", "first-class-quarters",
	"a-deck-corridor", "pursers-office", "second-class-quarters",
	"third-class-berths", "boat-deck-split", "grand-stair-escape",
]
# grand-stair-escape's 3D scene is a bespoke hand-built model (Blender), not
# generated from tile data like the others -- its 2D scene still comes from
# import_sidescroll.py, same as turbine-room below.
# Sidescroll (platformer) rooms otherwise only have a 2D scene --
# import_sidescroll.py doesn't generate a 3D version.
const SIDESCROLL_ROOMS := ["turbine-room"]


func _ready() -> void:
	for room in ROOMS:
		var row := HBoxContainer.new()

		var label := Label.new()
		label.text = room
		label.custom_minimum_size = Vector2(200, 0)
		row.add_child(label)

		var btn2d := Button.new()
		btn2d.text = "2D"
		btn2d.pressed.connect(_go.bind("res://scenes/rooms_2d/%s.tscn" % room))
		row.add_child(btn2d)

		var btn3d := Button.new()
		btn3d.text = "3D"
		btn3d.pressed.connect(_go.bind("res://scenes/rooms_3d/%s.tscn" % room))
		row.add_child(btn3d)

		$Center/VBox.add_child(row)

	var sep := Label.new()
	sep.text = "Sidescroll (2D only)"
	$Center/VBox.add_child(sep)

	for room in SIDESCROLL_ROOMS:
		var row := HBoxContainer.new()

		var label := Label.new()
		label.text = room
		label.custom_minimum_size = Vector2(200, 0)
		row.add_child(label)

		var btn := Button.new()
		btn.text = "Play"
		btn.pressed.connect(_go.bind("res://scenes/rooms_2d/%s.tscn" % room))
		row.add_child(btn)

		$Center/VBox.add_child(row)


func _go(scene_path: String) -> void:
	get_tree().change_scene_to_file(scene_path)
