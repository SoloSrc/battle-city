extends SceneTree
func _initialize() -> void:
	call_deferred("run")
func run() -> void:
	DirAccess.make_dir_recursive_absolute("res://docs/art/previews/district")
	for shot in ["overview","nico","market","edge","park","arcade","room","shop"]:
		var viewport := SubViewport.new()
		viewport.size = Vector2i(1600,1000)
		viewport.own_world_3d = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		var path := "res://levels/review/DistrictComposition.tscn"
		if shot == "room": path = "res://levels/district/interiors/StartingRoom.tscn"
		if shot == "shop": path = "res://levels/district/interiors/CardShop.tscn"
		var level: Node3D = load(path).instantiate()
		viewport.add_child(level)
		var camera: Camera3D
		if shot == "overview":
			level.get_node("CameraRig").queue_free()
			camera = Camera3D.new()
			level.add_child(camera)
			camera.projection = Camera3D.PROJECTION_ORTHOGONAL
			camera.size = 138
			camera.far = 500
			camera.position = Vector3(115,155,170)
			camera.look_at(Vector3(61,0,58))
		elif shot in ["room","shop"]:
			var rig: Node3D = load("res://scenes/world/CameraRig.tscn").instantiate()
			var focus := Node3D.new()
			level.add_child(focus)
			focus.position = Vector3(4,0,3) if shot == "room" else Vector3(5,0,4)
			rig.set("Target",focus)
			level.add_child(rig)
			rig.call("Snap")
			camera = rig.get_node("Pivot/Camera3D")
		else:
			var player: Node3D = level.get_node("Player")
			player.position = {"nico":Vector3(58,0,95),"market":Vector3(22,0,75),"edge":Vector3(36,0,61),"park":Vector3(94,0,74),"arcade":Vector3(77,0,29)}[shot]
			level.get_node("CameraRig").call("Snap")
			camera = level.get_node("CameraRig/Pivot/Camera3D")
		camera.make_current()
		for i in 15: await process_frame
		await RenderingServer.frame_post_draw
		print("VIEW %s: %d visible draw calls, %d primitives" % [shot,viewport.get_render_info(Viewport.RENDER_INFO_TYPE_VISIBLE,Viewport.RENDER_INFO_DRAW_CALLS_IN_FRAME),viewport.get_render_info(Viewport.RENDER_INFO_TYPE_VISIBLE,Viewport.RENDER_INFO_PRIMITIVES_IN_FRAME)])
		var target := "res://docs/art/previews/district/%s.png" % shot
		var error := viewport.get_texture().get_image().save_png(target)
		print("RENDER %s save=%s" % [shot,error])
		viewport.free()
	quit()
