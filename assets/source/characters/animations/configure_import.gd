extends SceneTree
func _initialize() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string("res://assets/source/characters/animations/manifest.json"))
	var config := ConfigFile.new()
	assert(config.load("res://assets/characters/anims/character_anims.glb.import")==OK)
	var resources: Dictionary = config.get_value("params","_subresources",{})
	var animations: Dictionary = resources.get("animations",{})
	for name in manifest.clips:
		var settings: Dictionary = animations.get(name,{})
		settings["settings/loop_mode"] = 1 if manifest.clips[name].loop else 0
		animations[name] = settings
	resources["animations"] = animations
	config.set_value("params","_subresources",resources)
	assert(config.save("res://assets/characters/anims/character_anims.glb.import")==OK)
	quit()
