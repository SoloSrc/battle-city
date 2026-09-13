extends SceneTree
func _initialize() -> void: call_deferred("run")
func run() -> void:
    root.size=Vector2i(1200,800)
    var game=root.get_node("Game")
    game.call("Transition","res://levels/district/District.tscn","arrival")
    for i in 100:await process_frame
    while game.get("IsTransitioning"):await process_frame
    game.call("LockInput","artist_capture")
    game.get("Encounters").process_mode=Node.PROCESS_MODE_DISABLED
    var player:Node3D=game.get("Player")
    var target="res://docs/art/previews/walkthrough-fixes"
    DirAccess.make_dir_recursive_absolute(target)
    var suffix="after" if "--after" in OS.get_cmdline_user_args() else "before"
    for sample in [["park-wall",Vector3(73,0,79)],["park-gate",Vector3(73,0,90)],["arcade",Vector3(70,0,22)]]:
        player.position=sample[1]
        for node in root.find_children("*","Node3D",true,false):
            if node.has_method("Snap") and node.has_node("Pivot/Camera3D"):node.call("Snap")
        for i in 15:await process_frame
        RenderingServer.force_draw(false)
        print("WALKTHROUGH capture ",sample[0]," ",suffix," save=",root.get_texture().get_image().save_png(target+"/"+sample[0]+"-"+suffix+".png"))
    quit()
