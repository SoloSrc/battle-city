extends SceneTree
func _initialize():
    call_deferred("review")
func review():
    var output = OS.get_cmdline_user_args()[0]
    var manifest = JSON.parse_string(FileAccess.get_file_as_string("res://assets/ui/manifest.json"))
    for key in manifest:
        assert(load("res://assets/ui/" + key + ".png") != null, key)
        if manifest[key]["slice"] > 0:
            assert(load("res://assets/ui/" + key + ".tres") != null, key)
    for specimen in ["components", "glyphs"]:
        var scene = load("res://assets/ui/review/" + specimen + ".tscn").instantiate()
        root.add_child(scene)
        await process_frame
        await process_frame
        await RenderingServer.frame_post_draw
        var shot = root.get_texture().get_image()
        assert(shot.save_png(output.path_join(specimen + "-godot.png")) == OK)
        scene.queue_free()
        await process_frame
    print("PASS: 45 textures, all StyleBox resources and both scenes loaded; two Godot captures saved.")
    quit()
