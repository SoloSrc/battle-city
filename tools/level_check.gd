extends SceneTree
## Command-line entry for the level checklist (architecture.md §7.3):
##
##   godot --headless --path . -s tools/level_check.gd -- res://levels/district/District.tscn
##
## Prints one line per check, a summary line, and exits 1 when anything fails.
## The editor menu (Project > Tools > Battle City: Run level checklist) runs the
## same checks on the scene being edited.

const LevelChecker := preload("res://addons/battle_city/level_checker.gd")


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		push_error("usage: godot --headless --path . -s tools/level_check.gd -- <scene.tscn>")
		quit(2)
		return
	var path: String = args[0]
	if not ResourceLoader.exists(path):
		push_error("level_check: scene not found: " + path)
		quit(2)
		return
	var packed: PackedScene = load(path)
	var scene := packed.instantiate()
	_run(scene, path)


func _run(scene: Node, path: String) -> void:
	await process_frame
	root.add_child(scene)
	await process_frame
	var checker := LevelChecker.new()
	var results: Array = await checker.run(scene)
	print("level_check %s" % path)
	for r in results:
		if r.level == "FAIL":
			printerr("level_check FAIL: " + r.text)
		else:
			print("level_check %s: %s" % [r.level, r.text])
	print(checker.summary(results))
	quit(1 if checker.has_failures(results) else 0)
