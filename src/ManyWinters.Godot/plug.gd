extends "res://addons/gd-plug/plug.gd"

# Editor-only addons, restored with:  godot --headless --path src/ManyWinters.Godot -s plug.gd install
# See docs/development.md ("Editor plugins").
func _plugging():
	plug("beckettlab/beckett-godot-mcp", {"tag": "v1.15.0"})
