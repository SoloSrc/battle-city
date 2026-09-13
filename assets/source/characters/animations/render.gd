extends SceneTree
## Independent Godot view per clip; assembles rendered tiles, no source image edits.
func _initialize() -> void: call_deferred("run")
func run() -> void:
    var spec = JSON.parse_string(FileAccess.get_file_as_string("res://assets/source/characters/animations/manifest.json"))
    var entries := []
    for clip in spec.clips:
        var view := SubViewport.new()
        view.size=Vector2i(400,300);view.own_world_3d=true
        view.render_target_update_mode=SubViewport.UPDATE_ALWAYS
        root.add_child(view)
        var world := Node3D.new();view.add_child(world)
        var env := Environment.new();env.background_mode=Environment.BG_COLOR
        env.background_color=Color(.76,.79,.81);env.ambient_light_source=Environment.AMBIENT_SOURCE_COLOR
        env.ambient_light_color=Color.WHITE;env.ambient_light_energy=.35
        var we := WorldEnvironment.new();we.environment=env;world.add_child(we)
        var sun := DirectionalLight3D.new();sun.rotation_degrees=Vector3(-50,-30,0)
        sun.light_energy=.55;sun.shadow_enabled=true;world.add_child(sun)
        var plane := PlaneMesh.new();plane.size=Vector2(10,10)
        var mat := StandardMaterial3D.new();mat.albedo_color=Color(.62,.65,.66);plane.material=mat
        var floor_node := MeshInstance3D.new();floor_node.mesh=plane;floor_node.position.y=-.02;world.add_child(floor_node)
        var camera := Camera3D.new();world.add_child(camera)
        camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=2.9
        camera.position=Vector3(2.4,2.0,-4);camera.look_at(Vector3(0,.95,0));camera.make_current()
        var actor: Node3D=load("res://scenes/characters/Character.tscn").instantiate()
        world.add_child(actor);actor.get_node("AnimationTree").active=false
        var player: AnimationPlayer=actor.get_node("Animations")
        player.callback_mode_process=AnimationMixer.ANIMATION_CALLBACK_MODE_PROCESS_MANUAL
        player.play(clip)
        entries.append([view,player,clip])
        var label := Label.new();label.text=clip;label.position=Vector2(12,8)
        label.add_theme_font_size_override("font_size",20);label.add_theme_color_override("font_color",Color(.05,.06,.07));view.add_child(label)
    for i in 3: await process_frame
    DirAccess.make_dir_recursive_absolute("/private/tmp/character-motion-frames")
    for frame in 60:
        for entry in entries:
            var duration: float=spec.clips[entry[2]].seconds
            var period := duration if spec.clips[entry[2]].loop else duration+.5
            entry[1].seek(min(fmod(frame/15.0,period),duration),true)
        await process_frame
        RenderingServer.force_draw(false)
        var sheet := Image.create(1600,1200,false,Image.FORMAT_RGBA8)
        sheet.fill(Color(.76,.79,.81))
        for i in entries.size():
            sheet.blit_rect(entries[i][0].get_texture().get_image(),Rect2i(0,0,400,300),Vector2i((i%4)*400,(i/4)*300))
        if sheet.save_png("/private/tmp/character-motion-frames/%03d.png"%frame)!=OK: quit(1);return
    print("AnimationSet rendered 60 frames at 15 fps")
    quit()
