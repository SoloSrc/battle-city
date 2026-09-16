extends SceneTree
## Bounds in metres, tested across the animation rather than at one instant.
func _initialize() -> void: call_deferred("run")
func run() -> void:
    var world := Node3D.new()
    root.add_child(world)
    var max_summon := 0.0
    var max_hit_depth := 0.0
    for name in ["SummonFlash", "HitPulse"]:
        var effect=load("res://vfx/"+name+".tscn").instantiate()
        effect.autoplay=false
        world.add_child(effect)
        effect.set_process(false)
        effect.configure(Transform3D.IDENTITY,Transform3D.IDENTITY)
        effect.play()
        for sample in 100:
            if sample>0: effect._process(effect.duration/100.0)
            for piece in effect.pieces:
                var box: AABB=piece.mesh.get_aabb()
                for corner in 8:
                    var point: Vector3=piece.transform*box.get_endpoint(corner)
                    if name=="SummonFlash":
                        max_summon=maxf(max_summon,maxf(absf(point.x),absf(point.z)))
                    elif piece!=effect.pieces[0]:
                        max_hit_depth=maxf(max_hit_depth,absf(point.z))
        effect.stop()
    print("DUEL64 geometry: summon half-width=",max_summon," m; hit-ray half-depth=",max_hit_depth," m")
    var ok := max_summon<=0.105 and max_hit_depth<=0.013
    print("DUEL64 geometry ","PASS" if ok else "FAIL")
    world.queue_free()
    await process_frame
    quit(0 if ok else 1)
