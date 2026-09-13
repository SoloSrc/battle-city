extends SceneTree
## Artist-side integration audit of the real Game flow. Injected input is not
## a physical-controller UX test. Duel outcomes use Fable's placeholder box.
var game: Node
var failures := 0
var passes := 0
var message_age := 0
var capture := false
var captured := {}

func _initialize() -> void:
	capture = "--capture" in OS.get_cmdline_user_args()
	call_deferred("run")

func check(ok: bool, text: String) -> void:
	print(("RuntimeArt PASS: " if ok else "RuntimeArt FAIL: ")+text)
	if ok: passes += 1
	else: failures += 1

func current_level() -> Node:
	return root.get_node_or_null("World/Level")

func player() -> Node3D:
	return root.get_node_or_null("World/Player")

func loaded(name: String) -> bool:
	var level := current_level()
	return level != null and level.scene_file_path.ends_with(name+".tscn")

func axis(direction: Vector2) -> void:
	for i in [0,1]:
		var event := InputEventJoypadMotion.new()
		event.device = 0
		event.axis = i
		event.axis_value = direction[i]
		Input.parse_input_event(event)

func button(pressed: bool) -> void:
	var event := InputEventJoypadButton.new()
	event.device = 0
	event.button_index = JOY_BUTTON_A
	event.pressed = pressed
	Input.parse_input_event(event)

func photo(name: String) -> void:
	if not capture or captured.has(name): return
	captured[name] = true
	RenderingServer.force_draw(false)
	var path := "res://docs/art/previews/district-runtime/%s.png"%name
	DirAccess.make_dir_recursive_absolute("res://docs/art/previews/district-runtime")
	check(root.get_texture().get_image().save_png(path)==OK,"captured "+name)

func tick(accept_messages := true) -> void:
	button(false)
	var panel := game.get_node("Messages/Panel") as Control
	if panel.visible:
		message_age += 1
		if capture and message_age == 15:
			for label in panel.find_children("*","Label",true,false):
				if label.text.begins_with("Placeholder duel against"):
					for id in ["d1","d2","d3"]:
						if "("+id+")" in label.text: await photo(id+"-placeholder-duel")
		if accept_messages and message_age > 30:
			button(true)
			message_age = 0
	else: message_age = 0
	await physics_frame

func wait_for(condition: Callable, label: String, limit := 2400) -> bool:
	axis(Vector2.ZERO)
	for i in limit:
		if condition.call():
			check(true,label)
			for j in 45: await tick()
			return true
		await tick()
	check(false,label+" timed out")
	return false

func go(points: Array, label: String) -> bool:
	for destination: Vector3 in points:
		var reached := false
		for i in 3000:
			var p := player()
			if p == null: break
			var delta := Vector2(destination.x-p.position.x,destination.z-p.position.z)
			if delta.length()<0.22:
				reached = true
				break
			axis(delta.normalized())
			await tick()
		if not reached:
			check(false,label+" blocked at "+str(player().position))
			axis(Vector2.ZERO)
			return false
	axis(Vector2.ZERO)
	for j in 12: await tick()
	check(true,label)
	return true

func interact() -> void:
	button(true)
	await physics_frame
	button(false)

func win(id: String) -> bool:
	axis(Vector2.ZERO)
	# Save a runtime dialogue/placeholder view before accepting its outcome.
	for i in 1800:
		if game.get_node("Messages/Panel").visible:
			await photo(id+"-encounter")
		if game.call("HasFlag","defeated:"+id):
			check(true,id+" won through the actual encounter flow")
			for j in 120: await tick()
			return true
		await tick()
	check(false,id+" encounter timed out")
	return false

func run() -> void:
	await process_frame
	game = root.get_node("Game")
	print("RuntimeArt physical joypads: ",Input.get_connected_joypads())
	game.call("NewGame")
	if not await wait_for(func(): return loaded("StartRoom"),"New Game uses integrated starting room"): return finish()
	await photo("starting-room")
	if not await go([Vector3(4,0,5)],"room exit approach"): return finish()
	await interact()
	if not await wait_for(func(): return loaded("District"),"door loads integrated District"): return finish()
	if not await win("d1"): return finish()
	var level := current_level()
	check(level.get_node("Navigation/Edge/ParkGate/Blocker").collision_layer==0,"Nico victory opens Park collision")
	check(level.get_node("Navigation/Edge/ArcadeGate/Blocker").collision_layer==1,"Arcade remains blocked before Mara victory")
	check(not level.get_node("Navigation/Arcade/ArcadeOwner/Marker").call("CanInteract",player()),"Arcade Owner remains unchallengeable")
	# Avoid the now-moved Nico at the reserved stand point.
	if not await go([Vector3(56,0,98),Vector3(72,0,98),Vector3(72,0,88),Vector3(82,0,88),Vector3(94,0,78)],"walk through unlocked Park gate"): return finish()
	await photo("park-approach")
	if not await win("d2"): return finish()
	level = current_level()
	check(level.get_node("Navigation/Edge/ArcadeGate/Blocker").collision_layer==0,"Mara victory opens Arcade collision")
	check(level.get_node("Navigation/Arcade/ArcadeOwner/Marker").call("CanInteract",player()),"Arcade Owner unlocks after Mara")
	if not await go([Vector3(94,0,78),Vector3(98,0,78),Vector3(98,0,46),Vector3(94,0,42),Vector3(94,0,34),Vector3(79,0,34),Vector3(78,0,32)],"walk through unlocked Arcade gate"): return finish()
	if not await win("d3"): return finish()
	await photo("arcade-victory")
	if not await go([Vector3(80,0,34),Vector3(94,0,34),Vector3(94,0,42),Vector3(98,0,46),Vector3(98,0,88),Vector3(72,0,88),Vector3(72,0,94),Vector3(38,0,94),Vector3(38,0,88),Vector3(22,0,88),Vector3(22,0,75)],"return through both gates to shop"): return finish()
	await photo("shop-exterior")
	await interact()
	if not await wait_for(func(): return loaded("ShopInterior"),"shop exterior door loads integrated interior"): return finish()
	await photo("shop-interior")
	if not await go([Vector3(5,0,3.5)],"counter approach"): return finish()
	await interact()
	await physics_frame
	check(game.get_node("Messages/Panel").visible,"shop counter invokes runtime placeholder UI")
	await photo("shop-counter")
	for i in 80: await tick()
	if not await go([Vector3(5,0,7)],"shop exit approach"): return finish()
	await interact()
	if not await wait_for(func(): return loaded("District"),"shop exit returns to District"): return finish()
	check(player().position.distance_to(Vector3(22,0,76))<0.5,"shop return uses shop_door spawn")
	check(game.call("HasFlag","defeated:d1") and game.call("HasFlag","defeated:d2") and game.call("HasFlag","defeated:d3"),"all three flags survive interior transitions")
	check(current_level().get_node("Navigation/Edge/ParkGate/Blocker").collision_layer==0 and current_level().get_node("Navigation/Edge/ArcadeGate/Blocker").collision_layer==0,"both gates remain open after return")
	finish()

func finish() -> void:
	axis(Vector2.ZERO)
	button(false)
	print("RuntimeArt summary: %d pass, %d fail"%[passes,failures])
	quit(1 if failures else 0)
