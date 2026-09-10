extends SceneTree
## Artist-side batch bake. No script is attached to composed levels.
func _initialize() -> void:
	call_deferred("bake_all")

func bake_all() -> void:
	for entry in [
		["res://levels/review/DistrictComposition.tscn", "DistrictReview"],
		["res://levels/district/interiors/StartingRoom.tscn", "StartingRoom"],
		["res://levels/district/interiors/CardShop.tscn", "CardShop"]]:
		var level: Node3D = load(entry[0]).instantiate()
		root.add_child(level)
		await process_frame
		var region: NavigationRegion3D = level.get_node("Navigation")
		# Gates must be traversable after their flag opens them. Exclude ONLY their
		# blockers from the bake, restoring collisions immediately afterwards.
		var gates: Array[StaticBody3D] = []
		for gate in get_nodes_in_group("gate"):
			var blocker: StaticBody3D = gate.get_node("Blocker")
			gates.append(blocker)
			blocker.collision_layer = 0
		var nav := region.navigation_mesh
		nav.cell_size = 0.25
		nav.cell_height = 0.25
		# Conservative rounded dimensions on the project's 0.25 m voxel grid.
		nav.agent_radius = 0.5
		nav.agent_height = 1.75
		nav.agent_max_climb = 0.25
		nav.geometry_parsed_geometry_type = NavigationMesh.PARSED_GEOMETRY_STATIC_COLLIDERS
		nav.geometry_collision_mask = 1
		# Include full-height obstacles: vertically clipping walls at street height
		# creates false walkable tops. Remove disconnected elevated polygons below.
		nav.filter_baking_aabb = AABB(Vector3(-2,-1,-2),Vector3(124,12,124))
		region.bake_navigation_mesh(false)
		var vertices := nav.get_vertices()
		var polygons: Array[PackedInt32Array] = []
		for i in nav.get_polygon_count():
			var polygon := nav.get_polygon(i)
			var at_ground := true
			for index in polygon:
				if vertices[index].y > 0.5: at_ground = false
			if at_ground: polygons.append(polygon)
		nav.clear_polygons()
		var remap := {}
		var used := PackedVector3Array()
		for polygon in polygons:
			for j in polygon.size():
				var old := polygon[j]
				if not remap.has(old):
					remap[old] = used.size()
					used.append(vertices[old])
				polygon[j] = remap[old]
			nav.add_polygon(polygon)
		nav.set_vertices(used)
		for blocker in gates: blocker.collision_layer = 1
		var target: String = "res://levels/district/navigation/%s.tres" % entry[1]
		var error := ResourceSaver.save(nav, target)
		print("BAKE %s: %d polygons; save=%d" % [entry[1],nav.get_polygon_count(),error])
		if error != OK or nav.get_polygon_count() == 0:
			quit(1)
			return
		level.free()
	quit()
