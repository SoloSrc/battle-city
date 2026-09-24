extends SceneTree

func _initialize():
    call_deferred("verify")

func inspect(model: Node3D) -> Dictionary:
    var meshes = model.find_children("*", "MeshInstance3D", true, false)
    var bounds = AABB()
    var first = true
    var triangles = 0
    var materials = 0
    var body_hash = 0
    for child in meshes:
        var box = child.global_transform * child.get_aabb()
        bounds = box if first else bounds.merge(box)
        first = false
        for surface in child.mesh.get_surface_count():
            var indices = child.mesh.surface_get_array_index_len(surface)
            triangles += indices / 3 if indices else child.mesh.surface_get_array_len(surface) / 3
            if child.mesh.surface_get_material(surface) != null:
                materials += 1
        if str(child.name).begins_with("Body_A_complete"):
            body_hash = hash(child.mesh.surface_get_arrays(0))
    return {"meshes":meshes.size(),"triangles":triangles,"material_surfaces":materials,
        "body_hash":body_hash,"position":[bounds.position.x,bounds.position.y,bounds.position.z],
        "size":[bounds.size.x,bounds.size.y,bounds.size.z]}

func body_arrays(model: Node3D) -> Array:
    for child in model.find_children("*", "MeshInstance3D", true, false):
        if str(child.name).begins_with("Body_A_complete"):
            return child.mesh.surface_get_arrays(0)
    return []

func verify():
    var report = {}
    var models = {}
    for name in ["character_a_base", "character_a_body_only"]:
        var packed = load("res://assets/characters/benchmark/" + name + ".glb") as PackedScene
        if packed == null:
            push_error("Could not import " + name)
            quit(1)
            return
        var instance = packed.instantiate()
        root.add_child(instance)
        models[name] = instance
        var result = inspect(instance)
        report[name] = result
        instance.visible = false
        if abs(result.size[1] - 1.7) > 0.002 or abs(result.position[1]) > 0.002 or result.body_hash == 0:
            push_error("Height, ground contact or complete-body mesh check failed: " + name)
            quit(1)
            return
    report["same_body_with_outfit_hidden"] = body_arrays(models.character_a_base) == body_arrays(models.character_a_body_only)
    if not report.same_body_with_outfit_hidden:
        push_error("Body mesh changed when outfit was removed")
        quit(1)
        return
    print("CHARACTER99_IMPORT ", JSON.stringify(report))
    var args = OS.get_cmdline_user_args()
    var chosen = "character_a_body_only" if args.has("--body") else "character_a_base"
    models[chosen].visible = true
    var capture = args.find("--capture")
    if capture >= 0 and capture + 1 < args.size():
        var camera = Camera3D.new()
        root.add_child(camera)
        camera.projection = Camera3D.PROJECTION_ORTHOGONAL
        camera.size = 1.86
        camera.position = Vector3(0, 0.85, -5)
        camera.look_at(Vector3(0, 0.85, 0))
        camera.current = true
        var light = DirectionalLight3D.new()
        root.add_child(light)
        light.rotation_degrees = Vector3(-35, -150, 0)
        light.light_energy = 1.1
        var environment = WorldEnvironment.new()
        environment.environment = Environment.new()
        environment.environment.background_mode = Environment.BG_COLOR
        environment.environment.background_color = Color(0.36,0.39,0.43)
        environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
        environment.environment.ambient_light_color = Color.WHITE
        environment.environment.ambient_light_energy = 0.6
        root.add_child(environment)
        root.size = Vector2i(800,1000)
        root.msaa_3d = Viewport.MSAA_4X
        for frame in range(12):
            await process_frame
        await RenderingServer.frame_post_draw
        root.get_texture().get_image().save_png(args[capture + 1])
        print("CHARACTER99_CAPTURE ", args[capture + 1])
    quit(0)
