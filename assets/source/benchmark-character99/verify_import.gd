extends SceneTree
func _initialize():
    call_deferred("verify")

func verify():
    var packed = load("res://assets/characters/benchmark/character_a_base.glb") as PackedScene
    if packed == null:
        push_error("Could not import benchmark GLB")
        quit(1)
        return
    var model = packed.instantiate()
    root.add_child(model)
    var meshes = model.find_children("*", "MeshInstance3D", true, false)
    var bounds = AABB()
    var first = true
    var triangles = 0
    var materials = 0
    for child in meshes:
        var box = child.global_transform * child.get_aabb()
        bounds = box if first else bounds.merge(box)
        first = false
        for surface in child.mesh.get_surface_count():
            var indices = child.mesh.surface_get_array_index_len(surface)
            triangles += indices / 3 if indices else child.mesh.surface_get_array_len(surface) / 3
            if child.mesh.surface_get_material(surface) != null:
                materials += 1
    print("CHARACTER99_IMPORT ", JSON.stringify({"meshes":meshes.size(),"triangles":triangles,"material_surfaces":materials,"position":[bounds.position.x,bounds.position.y,bounds.position.z],"size":[bounds.size.x,bounds.size.y,bounds.size.z]}))
    var args = OS.get_cmdline_user_args()
    if args.size() == 2 and args[0] == "--capture":
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
        root.get_texture().get_image().save_png(args[1])
        print("CHARACTER99_CAPTURE ", args[1])
    if abs(bounds.size.y - 1.7) > 0.002 or abs(bounds.position.y) > 0.002 or meshes.is_empty():
        quit(1)
    else:
        quit(0)
