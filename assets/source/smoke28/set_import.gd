extends SceneTree
func _initialize():
 var file="res://assets/characters/anims/character_anims.glb.import"
 var config=ConfigFile.new()
 assert(config.load(file)==OK)
 var sub=config.get_value("params","_subresources",{})
 sub["animations"]={"idle":{"settings/loop_mode":1},"walk":{"settings/loop_mode":1}}
 config.set_value("params","_subresources",sub)
 assert(config.save(file)==OK)
 quit()
