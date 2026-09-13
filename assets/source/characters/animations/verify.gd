extends SceneTree
var failed := 0
func _initialize() -> void: call_deferred("run")
func check(ok: bool, label: String) -> void:
	print(("AnimationSet PASS: " if ok else "AnimationSet FAIL: ")+label)
	if not ok: failed+=1
func run() -> void:
	var manifest = JSON.parse_string(FileAccess.get_file_as_string("res://assets/source/characters/animations/manifest.json"))
	var actor: Node3D = load("res://scenes/characters/Character.tscn").instantiate()
	root.add_child(actor)
	await process_frame
	actor.get_node("AnimationTree").active = false
	var player: AnimationPlayer = actor.get_node("Animations")
	var skeleton: Skeleton3D = actor.find_children("*","Skeleton3D",true,false)[0]
	var events = JSON.parse_string(FileAccess.get_file_as_string("res://data/rig/animation_events.json"))
	for name in manifest.clips:
		if not player.has_animation(name):
			check(false,name+" exists on runtime Character")
			continue
		var clip := player.get_animation(name)
		check(abs(clip.length-manifest.clips[name].seconds)<.002,name+" duration")
		check((clip.loop_mode==Animation.LOOP_LINEAR)==manifest.clips[name].loop,name+" loop mode")
		var animated := false
		var root_fixed := true
		for track in clip.get_track_count():
			var path := str(clip.track_get_path(track))
			if path.ends_with(":Root"):
				for key in clip.track_get_key_count(track):
					var value = clip.track_get_key_value(track,key)
					if value is Vector3 and clip.track_get_type(track)==Animation.TYPE_POSITION_3D:
						root_fixed = root_fixed and value.length()<.0001
					elif value is Quaternion:
						root_fixed = root_fixed and value.angle_to(clip.track_get_key_value(track,0))<.001
					elif value is Vector3:
						root_fixed = root_fixed and value.distance_to(clip.track_get_key_value(track,0))<.0001
			elif clip.track_get_type(track)==Animation.TYPE_ROTATION_3D:
				for key in clip.track_get_key_count(track):
					if not clip.track_get_key_value(track,key).is_equal_approx(clip.track_get_key_value(track,0)): animated=true
		check(root_fixed and animated,name+" has authored bone motion with fixed Root")
		if events.has(name):
			for event in events[name]:
				var found := false
				for track in clip.get_track_count():
					if clip.track_get_type(track)!=Animation.TYPE_METHOD: continue
					for key in clip.track_get_key_count(track):
						var call_data: Dictionary = clip.track_get_key_value(track,key)
						if event in call_data.args and abs(clip.track_get_key_time(track,key)-events[name][event])<.002: found=true
				check(found,name+" injects "+event+" at contracted time")
		if manifest.clips[name].loop:
			var seamless := true
			for track in clip.get_track_count():
				if clip.track_get_type(track) not in [Animation.TYPE_POSITION_3D,Animation.TYPE_ROTATION_3D]: continue
				var first = clip.track_get_key_value(track,0)
				var last = clip.track_get_key_value(track,clip.track_get_key_count(track)-1)
				if first is Quaternion:
					seamless = seamless and first.angle_to(last)<.001
				else: seamless = seamless and first.distance_to(last)<.0001
			check(seamless,name+" closes its loop")
		player.play(name)
		for fraction in [0.0,.25,.5,.75,1.0]:
			player.seek(clip.length*fraction,true)
			for bone in skeleton.get_bone_count():
				if not skeleton.get_bone_global_pose(bone).is_finite(): check(false,name+" finite pose")
	print("AnimationSet summary: %d failures"%failed)
	quit(1 if failed else 0)
