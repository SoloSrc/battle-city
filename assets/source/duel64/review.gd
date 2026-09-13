extends SceneTree
const NAMES=["CardMaterialise","CardSelected","SummonFlash","AttackTrail","HitPulse","Dissolve"]
var failures := 0
func _initialize() -> void:call_deferred("run")
func check(ok: bool,message: String) -> void:
    print("DUEL64 ","PASS: " if ok else "FAIL: ",message)
    if not ok:failures+=1
func make_card(world:Node3D) -> MeshInstance3D:
    var card=MeshInstance3D.new();var mesh=QuadMesh.new();mesh.size=Vector2(.5,.73);card.mesh=mesh
    var mat=load("res://shaders/materials/hologram_player.tres").duplicate()
    mat.set_shader_parameter("face_texture",ImageTexture.create_from_image(Image.load_from_file(ProjectSettings.globalize_path("res://docs/art/previews/duel64/rogue-doll-face.png"))))
    mat.set_shader_parameter("hover_amplitude",0.0)
    card.material_override=mat;world.add_child(card)
    return card
func run() -> void:
    # Exercise lifecycle and card binding with actual shared hologram instances.
    for name in NAMES:
        var world=Node3D.new();root.add_child(world)
        var card=make_card(world)
        var effect=load("res://vfx/"+name+".tscn").instantiate()
        effect.autoplay=false;world.add_child(effect);effect.bind_card(card)
        effect.configure(Transform3D.IDENTITY,Transform3D(Basis.IDENTITY,Vector3(1.0,0,0)))
        effect.play();effect.set_process(false)
        if name=="CardSelected":
            check(card.get_instance_shader_parameter("selected")==1.0,name+" selects bound card")
            effect.stop()
            check(card.get_instance_shader_parameter("selected")==0.0,name+" stop clears selection")
        else:
            effect._process(effect.duration*.5)
            if name in ["CardMaterialise","Dissolve"]:
                var key="reveal" if name=="CardMaterialise" else "dissolve"
                check(absf(card.get_instance_shader_parameter(key)-.5)<.001,name+" midpoint")
            else:
                check(effect.pieces.size()>0,name+" builds visible geometry")
                if name=="AttackTrail":check(effect.pieces[1].global_position.distance_to(Vector3(.5,0,0))<.001,"AttackTrail travels toward the configured target")
            effect._process(effect.duration)
        check(effect.is_queued_for_deletion(),name+" cleans up")
        world.queue_free();await process_frame
    var sounds=JSON.parse_string(FileAccess.get_file_as_string("res://assets/source/duel64/sfx-manifest.json"))
    for entry in sounds:
        var sound=load("res://"+entry.path)
        check(sound is AudioStream and absf(sound.get_length()-entry.seconds)<.02,"sound "+entry.cue+" imports with expected duration")
    if "--render" not in OS.get_cmdline_user_args():
        print("DUEL64 summary: ",failures," failures");quit(1 if failures else 0);return
    DirAccess.make_dir_recursive_absolute("/private/tmp/duel64-frames")
    for frame in 24:
        var views=[]
        for name in NAMES:
            var view=SubViewport.new();view.size=Vector2i(420,320);view.own_world_3d=true
            view.render_target_update_mode=SubViewport.UPDATE_ALWAYS;root.add_child(view)
            var world=Node3D.new();view.add_child(world)
            var env=Environment.new();env.background_mode=Environment.BG_COLOR;env.background_color=Color(.08,.10,.14)
            var we=WorldEnvironment.new();we.environment=env;world.add_child(we)
            var camera=Camera3D.new();world.add_child(camera);camera.projection=Camera3D.PROJECTION_ORTHOGONAL;camera.size=1.6
            camera.position=Vector3(0,.7,2);camera.look_at(Vector3.ZERO);camera.make_current()
            var card=make_card(world);card.visible=name in ["CardMaterialise","CardSelected","Dissolve"]
            var effect=load("res://vfx/"+name+".tscn").instantiate();effect.autoplay=false;world.add_child(effect)
            effect.bind_card(card);effect.set_process(false)
            var start=Vector3(-.6,0,0) if name=="AttackTrail" else Vector3.ZERO
            effect.configure(Transform3D(Basis.IDENTITY,start),Transform3D(Basis.IDENTITY,Vector3(.6,0,0)))
            effect.play()
            var t=fmod(frame/12.0,maxf(effect.duration,.6)+.25)
            effect._process(minf(t,effect.duration))
            var label=Label.new();label.text=name;label.position=Vector2(12,10);label.add_theme_font_size_override("font_size",20);view.add_child(label)
            views.append(view)
        await process_frame
        RenderingServer.force_draw(false)
        var sheet=Image.create(1260,640,false,Image.FORMAT_RGBA8)
        for i in views.size():
            var tile=views[i].get_texture().get_image()
            tile.convert(Image.FORMAT_RGBA8)
            sheet.blit_rect(tile,Rect2i(0,0,420,320),Vector2i((i%3)*420,(i/3)*320))
        check(sheet.save_png("/private/tmp/duel64-frames/%03d.png"%frame)==OK,"render frame "+str(frame))
        for view in views:view.queue_free()
        await process_frame
    print("DUEL64 summary: ",failures," failures; rendered with ",RenderingServer.get_current_rendering_method())
    quit(1 if failures else 0)
