@tool
extends EditorPlugin
## Battle City editor tools (architecture.md §7.1, §7.3):
## - Project > Tools > "Battle City: Run level checklist" runs tools/level_check.gd
##   on the scene being edited and prints the report to Output.
## - Project > Tools > "Battle City: KitSnap on/off" snaps selected kit_* nodes to
##   the 1 m grid and 90° yaw, prop_* nodes to the 0.25 m grid, while you move them.
## - Gizmos for EncounterSite, Duelist, PlayerSpawn and Door.

const CHECK_ITEM := "Battle City: Run level checklist"
const SNAP_ITEM := "Battle City: KitSnap on/off"
const LevelChecker := preload("res://addons/battle_city/level_checker.gd")
const MarkerGizmos := preload("res://addons/battle_city/marker_gizmos.gd")

var _gizmos := MarkerGizmos.new()
var _snap_enabled := true
var _dialog: AcceptDialog


func _enter_tree() -> void:
	add_node_3d_gizmo_plugin(_gizmos)
	add_tool_menu_item(CHECK_ITEM, _run_checklist)
	add_tool_menu_item(SNAP_ITEM, _toggle_snap)
	_dialog = AcceptDialog.new()
	_dialog.title = "Level checklist"
	_dialog.min_size = Vector2i(720, 360)
	EditorInterface.get_base_control().add_child(_dialog)


func _exit_tree() -> void:
	remove_node_3d_gizmo_plugin(_gizmos)
	remove_tool_menu_item(CHECK_ITEM)
	remove_tool_menu_item(SNAP_ITEM)
	if is_instance_valid(_dialog):
		_dialog.queue_free()


func _process(_delta: float) -> void:
	if not _snap_enabled:
		return
	for node in EditorInterface.get_selection().get_selected_nodes():
		if node is Node3D:
			_snap(node)


func _snap(node: Node3D) -> void:
	var step := 0.0
	if node.name.begins_with("kit_"):
		step = 1.0
	elif node.name.begins_with("prop_"):
		step = 0.25
	else:
		return
	var p := node.position
	var snapped_p := Vector3(snappedf(p.x, step), snappedf(p.y, step), snappedf(p.z, step))
	if not snapped_p.is_equal_approx(p):
		node.position = snapped_p
	if step == 1.0:
		var yaw := snappedf(node.rotation_degrees.y, 90.0)
		if not is_equal_approx(yaw, node.rotation_degrees.y):
			node.rotation_degrees.y = yaw


func _toggle_snap() -> void:
	_snap_enabled = not _snap_enabled
	print("KitSnap %s" % ("on" if _snap_enabled else "off"))


func _run_checklist() -> void:
	var root := EditorInterface.get_edited_scene_root()
	if root == null:
		push_warning("Level checklist: open a level scene first")
		return
	var checker := LevelChecker.new()
	var results: Array = await checker.run(root)
	var lines := PackedStringArray()
	for r in results:
		var line := "%s %s" % [r.level, r.text]
		lines.append(line)
		if r.level == "FAIL":
			push_error("level_check: " + r.text)
		else:
			print("level_check " + line)
	var summary := checker.summary(results)
	print(summary)
	_dialog.dialog_text = summary + "\n\n" + "\n".join(lines)
	_dialog.popup_centered()
