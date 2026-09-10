extends SceneTree
## Repeatable spatial audit using Fable's PlayerController and injected joypad
## events. This does NOT certify physical gamepad UX or #23 encounter flow.
var level: Node3D
var player: CharacterBody3D
var failed := 0

func _initialize() -> void:
	call_deferred("run")

func check(ok: bool, label: String) -> void:
	print(("PASS " if ok else "FAIL ") + label)
	if not ok: failed += 1

func stick(direction: Vector2, strength: float) -> void:
	for axis in [0,1]:
		var event := InputEventJoypadMotion.new()
		event.device = 0
		event.axis = axis
		event.axis_value = direction[axis] * strength
		Input.parse_input_event(event)

func settle() -> void:
	stick(Vector2.ZERO,0)
	for i in 20: await physics_frame

func interact_with(door: Node, label: String) -> void:
	var triggered := {"value":false}
	door.connect("TransitionRequested",func(_scene,_spawn,_player): triggered.value = true)
	var button := InputEventJoypadButton.new()
	button.device = 0
	button.button_index = JOY_BUTTON_A
	button.pressed = true
	Input.parse_input_event(button)
	for i in 3: await physics_frame
	button.pressed = false
	Input.parse_input_event(button)
	check(triggered.value,label+" interaction raises TransitionRequested")

func travel(label: String, start: Vector3, points: Array, strength: float) -> void:
	await settle()
	player.position = start
	player.velocity = Vector3.ZERO
	await settle()
	var frames := 0
	var ok := true
	for target: Vector3 in points:
		var reached := false
		for i in 3600:
			var delta := Vector2(target.x-player.position.x,target.z-player.position.z)
			if delta.length() < 0.15:
				reached = true
				break
			stick(delta.normalized(),strength)
			await physics_frame
			frames += 1
			if player.position.y < -0.2:
				break
		if not reached:
			ok = false
			break
	await settle()
	check(ok, "%s: %.2f simulated seconds; endpoint %s" % [label,frames/60.0,player.position])

func flood() -> Dictionary:
	# 1 m sampling plus swept capsule edges: no teleporting through thin walls.
	var space := level.get_world_3d().direct_space_state
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.35
	capsule.height = 1.7
	var shape := PhysicsShapeQueryParameters3D.new()
	shape.shape = capsule
	shape.collision_mask = 1
	var valid := {}
	for x in range(7,114):
		for z in range(7,106):
			var p := Vector3(x,0.91,z)
			var ground := space.intersect_ray(PhysicsRayQueryParameters3D.create(p,Vector3(x,-0.1,z),1))
			if ground.is_empty() or ground.position.y > 0.05: continue
			shape.transform = Transform3D(Basis.IDENTITY,p)
			if space.intersect_shape(shape,1).is_empty(): valid[Vector2i(x,z)] = true
	var found := {Vector2i(58,98):true}
	var queue := [Vector2i(58,98)]
	var index := 0
	while index < queue.size():
		var p: Vector2i = queue[index]
		index += 1
		for step in [Vector2i.LEFT,Vector2i.RIGHT,Vector2i.UP,Vector2i.DOWN]:
			var q: Vector2i = p+step
			if found.has(q) or not valid.has(q): continue
			shape.transform = Transform3D(Basis.IDENTITY,Vector3(p.x,.91,p.y))
			shape.motion = Vector3(step.x,0,step.y)
			var sweep := space.cast_motion(shape)
			shape.motion = Vector3.ZERO
			if sweep[0] < 0.999: continue
			found[q] = true
			queue.append(q)
	return found

func run() -> void:
	level = load("res://levels/review/DistrictComposition.tscn").instantiate()
	root.add_child(level)
	player = level.get_node("Player")
	await settle()
	print("Physical joypads reported: ", Input.get_connected_joypads())
	var nico: Node = level.get_node("Navigation/Plaza/Nico/Marker")
	check(nico.call("IsInCone",Vector3(58,0,98)),"arrival is inside Nico's configured cone")
	check(not nico.call("IsInCone",Vector3(58,0,100)),"Nico cone rejects points beyond 8 m")
	var connected := flood()
	check(connected.has(Vector2i(22,76)),"locked state still reaches shop")
	check(not connected.has(Vector2i(94,68)) and not connected.has(Vector2i(70,26)),"closed gates prevent all sampled Park/Arcade bypasses")
	level.get_node("Navigation/Edge/ParkGate").call("Open",true)
	await settle()
	connected = flood()
	check(connected.has(Vector2i(94,68)) and not connected.has(Vector2i(70,26)),"d1 gate opening grants Park only")
	level.get_node("Navigation/Edge/ArcadeGate").call("Open",true)
	await settle()
	connected = flood()
	check(connected.has(Vector2i(70,26)),"both gates opening grants Arcade")
	var map := level.get_world_3d().navigation_map
	for destination in [Vector3(22,0,76),Vector3(94,0,68),Vector3(70,0,26)]:
		var path := NavigationServer3D.map_get_path(map,Vector3(58,0,98),destination,true)
		check(path.size()>1 and path[-1].distance_to(destination)<=0.51,"baked navigation connects arrival to %s"%destination)
	for strength in [0.5,1.0]:
		var mode := "walk" if strength == 0.5 else "run"
		await travel("arrival to Nico approach "+mode,Vector3(58,0,98),[Vector3(58,0,93)],strength)
		await travel("plaza to shop "+mode,Vector3(58,0,98),[Vector3(38,0,88),Vector3(22,0,88),Vector3(22,0,75)],strength)
		await travel("Nico to Mara approach "+mode,Vector3(60,0,90),[Vector3(72,0,88),Vector3(82,0,88),Vector3(94,0,88),Vector3(94,0,73)],strength)
		await travel("Mara to Arcade approach "+mode,Vector3(94,0,73),[Vector3(98,0,74),Vector3(98,0,46),Vector3(94,0,42),Vector3(94,0,34),Vector3(78,0,34),Vector3(77,0,29)],strength)
		await travel("Arcade return to shop "+mode,Vector3(77,0,29),[Vector3(78,0,34),Vector3(94,0,34),Vector3(94,0,42),Vector3(98,0,46),Vector3(98,0,88),Vector3(72,0,88),Vector3(38,0,88),Vector3(22,0,88),Vector3(22,0,75)],strength)
	await travel("exterior room door",Vector3(58,0,101),[Vector3(58,0,102.5)],0.5)
	await interact_with(level.get_node("Navigation/Plaza/RoomDoor"),"exterior room door")
	await travel("exterior shop door",Vector3(22,0,78),[Vector3(22,0,75)],0.5)
	await interact_with(level.get_node("Navigation/Market/CardShopDoor"),"exterior shop door")
	player = null
	level.queue_free()
	await process_frame
	await process_frame
	for name in ["StartingRoom","CardShop"]:
		level = load("res://levels/district/interiors/%s.tscn" % name).instantiate()
		player = load("res://scenes/characters/Player.tscn").instantiate()
		player.position = level.get_node("Arrival").position
		level.add_child(player)
		root.add_child(level)
		await settle()
		var start := player.position
		await travel(name+" exit approach",start,[start+Vector3(0,0,1)],0.5)
		await interact_with(level.get_node("Exit"),name+" exit")
		if name == "CardShop":
			await travel("shop counter approach",Vector3(5,0,6),[Vector3(5,0,3.5)],0.5)
			var result := {"hit":false}
			level.get_node("ShopCounter").connect("ShopRequested",func(_id,_player): result.hit=true)
			Input.action_press("interact")
			for i in 3: await physics_frame
			Input.action_release("interact")
			check(result.hit,"shop counter raises ShopRequested through player interaction")
		player = null
		level.queue_free()
		await process_frame
		await process_frame
	print("COMPOSITION AUDIT failures=%d" % failed)
	quit(1 if failed else 0)
