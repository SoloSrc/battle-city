@tool
extends RefCounted
## The level checklist (architecture.md §7.3). Pure checks on a scene root that
## is inside a tree (physics queries need a World3D). Returns
## [{level: "PASS"|"FAIL"|"WARN"|"INFO", text: String}]. Used by the editor
## plugin menu and by tools/level_check.gd on the command line.

const DUELISTS_PATH := "res://data/duelists.json"
const TRIANGLE_BUDGET := 400_000
const DRAW_CALL_BUDGET := 600
const KNOWN_FLAGS := ["tutorial_done", "ending_seen"]
const NAV_TOLERANCE := 0.5
const SITE_DUELIST_RANGE := 10.0

var _results: Array = []


func run(root: Node) -> Array:
	_results = []
	if not root.is_inside_tree():
		_fail("scene root is not inside a tree; physics and navigation checks need one")
		return _results
	# let physics settle before the shape queries; navmesh checks read the baked mesh directly
	await root.get_tree().physics_frame
	await root.get_tree().physics_frame
	_check_structure(root)
	_check_spawns(root)
	_check_sites(root)
	_check_duelist_ids(root)
	_check_camera_coverage(root)
	_check_kit_pieces(root)
	_check_doors(root)
	_check_gates(root)
	_check_budget(root)
	return _results


func summary(results: Array) -> String:
	var counts := {"PASS": 0, "FAIL": 0, "WARN": 0, "INFO": 0}
	for r in results:
		counts[r.level] += 1
	return "level_check summary: %d pass, %d fail, %d warn" % [counts.PASS, counts.FAIL, counts.WARN]


func has_failures(results: Array) -> bool:
	for r in results:
		if r.level == "FAIL":
			return true
	return false


# --- checks -----------------------------------------------------------------

func _check_structure(root: Node) -> void:
	var nav := _children_of_type(root, "NavigationRegion3D")
	var env := _children_of_type(root, "WorldEnvironment")
	var sun := _children_of_type(root, "DirectionalLight3D")
	_check(nav.size() == 1, "exactly one NavigationRegion3D under the root (found %d)" % nav.size())
	_check(env.size() == 1, "exactly one WorldEnvironment under the root (found %d)" % env.size())
	_check(sun.size() == 1, "exactly one DirectionalLight3D under the root (found %d)" % sun.size())
	if _nodes_with_script(root, "LevelBounds").is_empty():
		_warn("no LevelBounds volume; the player has no safety net if the kit has gaps")


func _check_spawns(root: Node) -> void:
	var spawns := _nodes_with_script(root, "PlayerSpawn")
	var ids := {}
	for s in spawns:
		var id: String = s.get("Id")
		ids[id] = ids.get(id, 0) + 1
	if _is_interior(root):
		_check(not spawns.is_empty() and ids.get("arrival", 0) <= 1, "interior has at least one PlayerSpawn and at most one 'arrival' (found %d spawns)" % spawns.size())
	else:
		_check(ids.get("arrival", 0) == 1, "exactly one PlayerSpawn with id 'arrival' (found %d)" % ids.get("arrival", 0))
	for id in ids:
		if ids[id] > 1:
			_fail("PlayerSpawn id '%s' is used %d times" % [id, ids[id]])
	for door in _nodes_with_script(root, "Door"):
		var ret: String = door.get("ReturnSpawn")
		if ret == "":
			_warn("Door '%s' has no ReturnSpawn; the target's exit cannot come back here" % door.name)
		else:
			_check(ids.has(ret), "Door '%s' return spawn '%s' exists in this scene" % [door.name, ret])


func _check_sites(root: Node) -> void:
	var sites := _nodes_with_script(root, "EncounterSite")
	var duelists := _nodes_with_script(root, "Duelist")
	var space: PhysicsDirectSpaceState3D = null
	var world := (root as Node3D).get_world_3d() if root is Node3D else root.get_viewport().find_world_3d()
	if world != null:
		space = PhysicsServer3D.space_get_direct_state(world.space)
	var nav_ready := _navmesh_ready(root)
	for site in sites:
		var site_id: String = site.get("Id")
		var duelist_id: String = site.get("DuelistId")
		var centre: Vector3 = site.global_position
		var matched := false
		for d in duelists:
			if d.get("DuelistId") == duelist_id and d.global_position.distance_to(centre) <= SITE_DUELIST_RANGE:
				matched = true
		_check(matched, "EncounterSite '%s' has Duelist '%s' within %.0f m" % [site_id, duelist_id, SITE_DUELIST_RANGE])
		if nav_ready:
			var region := _find_nav_region(root)
			for label in ["A", "B"]:
				var p: Vector3 = site.get("StandPoint" + label)
				var closest := _closest_point_on_navmesh(region, p)
				# the baked surface floats up to a cell above the collider, so compare on the ground plane
				var off := Vector2(closest.x - p.x, closest.z - p.z).length()
				_check(off <= NAV_TOLERANCE and absf(closest.y - p.y) <= 1.0, "site '%s' stand point %s is on the navmesh (%.2f m off)" % [site_id, label, off])
		if space != null:
			var size: Vector3 = site.get("ClearanceSize")
			var box := BoxShape3D.new()
			box.size = Vector3(size.x, maxf(size.y - 0.3, 0.1), size.z)
			var xf: Transform3D = site.get("ClearanceTransform")
			xf.origin = site.global_position + Vector3.UP * (0.15 + box.size.y * 0.5)
			var q := PhysicsShapeQueryParameters3D.new()
			q.shape = box
			q.transform = xf
			q.collision_mask = 1
			q.collide_with_areas = false
			var hits := space.intersect_shape(q, 8)
			var names := PackedStringArray()
			for h in hits:
				names.append(str(h.collider.name))
			_check(hits.is_empty(), "site '%s' clearance box (%.0f × %.0f m) is free of world collision%s" % [site_id, size.x, size.z, "" if hits.is_empty() else ": " + ", ".join(names)])
	for i in range(sites.size()):
		for j in range(i + 1, sites.size()):
			if _site_aabb(sites[i]).intersects(_site_aabb(sites[j])):
				_fail("EncounterSite '%s' and '%s' reservations overlap" % [sites[i].get("Id"), sites[j].get("Id")])
	if sites.size() > 1:
		_pass("%d encounter reservations checked for overlap" % sites.size())
	if not nav_ready and not sites.is_empty():
		_warn("navmesh not baked; stand-point checks skipped")


func _check_duelist_ids(root: Node) -> void:
	var duelists := _nodes_with_script(root, "Duelist")
	if duelists.is_empty():
		return
	if not FileAccess.file_exists(DUELISTS_PATH):
		_warn("%s not present yet (issue #26); duelist ids not verified" % DUELISTS_PATH)
		return
	var text := FileAccess.get_file_as_string(DUELISTS_PATH)
	var data = JSON.parse_string(text)
	var known := {}
	if data is Array:
		for entry in data:
			if entry is Dictionary and entry.has("id"):
				known[entry.id] = true
	elif data is Dictionary:
		var list = data.get("duelists", data)
		if list is Array:
			for entry in list:
				if entry is Dictionary and entry.has("id"):
					known[entry.id] = true
		else:
			for key in list:
				known[key] = true
	for d in duelists:
		var id: String = d.get("DuelistId")
		_check(known.has(id), "Duelist id '%s' exists in duelists.json" % id)


func _check_camera_coverage(root: Node) -> void:
	var bounds := _nodes_with_script(root, "CameraBounds")
	_check(not bounds.is_empty(), "at least one CameraBounds volume (found %d)" % bounds.size())
	if bounds.is_empty():
		return
	var region := _find_nav_region(root)
	if region == null or region.navigation_mesh == null or region.navigation_mesh.get_polygon_count() == 0:
		_warn("navmesh not baked; CameraBounds coverage not verified")
		return
	var verts := region.navigation_mesh.get_vertices()
	var outside := 0
	var sample := Vector3.ZERO
	for v in verts:
		var p: Vector3 = region.global_transform * v
		var covered := false
		for b in bounds:
			if b.call("Contains", p + Vector3.UP * 0.9):
				covered = true
				break
		if not covered:
			outside += 1
			sample = p
	_check(outside == 0, "every navmesh vertex is inside a CameraBounds (%d of %d outside%s)" % [outside, verts.size(), "" if outside == 0 else ", e.g. %s" % sample])


func _check_kit_pieces(root: Node) -> void:
	var pieces := 0
	var name_re := RegEx.new()
	name_re.compile("^(kit|prop)_[a-z0-9_]+$")
	for node in _all_nodes(root):
		if not (node is Node3D):
			continue
		var n: String = node.name
		if not (n.to_lower().begins_with("kit_") or n.to_lower().begins_with("prop_")):
			continue
		pieces += 1
		if not name_re.search(n):
			_fail("'%s' does not follow the §6.3 naming (lowercase, digits, underscores)" % n)
		if not node.scale.is_equal_approx(Vector3.ONE):
			_fail("'%s' has an unapplied scale %s" % [n, node.scale])
	_pass("%d kit pieces and props checked for naming and scale" % pieces)


func _check_doors(root: Node) -> void:
	for door in _nodes_with_script(root, "Door"):
		var target: String = door.get("TargetScene")
		var spawn: String = door.get("TargetSpawn")
		if target == "":
			_fail("Door '%s' has no TargetScene" % door.name)
			continue
		if not ResourceLoader.exists(target):
			_fail("Door '%s' target '%s' does not exist" % [door.name, target])
			continue
		var packed: PackedScene = load(target)
		var inst := packed.instantiate()
		var found := false
		for s in _nodes_with_script(inst, "PlayerSpawn"):
			if s.get("Id") == spawn:
				found = true
		inst.free()
		_check(found, "Door '%s' target '%s' has spawn '%s'" % [door.name, target.get_file(), spawn])


func _check_gates(root: Node) -> void:
	var re := RegEx.new()
	re.compile("^defeated:[a-z][a-z0-9_]*$")
	for gate in _nodes_with_script(root, "ProgressionGate"):
		var flag: String = gate.get("RequiredFlag")
		_check(flag in KNOWN_FLAGS or re.search(flag) != null, "ProgressionGate '%s' flag '%s' is a known flag" % [gate.name, flag])
	for d in _nodes_with_script(root, "Duelist"):
		var required: String = d.get("RequiredFlag")
		if required != "":
			_check(required in KNOWN_FLAGS or re.search(required) != null, "Duelist '%s' required flag '%s' is a known flag" % [d.get("DuelistId"), required])


func _check_budget(root: Node) -> void:
	var tris := 0
	var surfaces := 0
	for node in _all_nodes(root):
		if node is MeshInstance3D and node.mesh != null:
			var mesh: Mesh = node.mesh
			for i in range(mesh.get_surface_count()):
				surfaces += 1
				var arrays := mesh.surface_get_arrays(i)
				if arrays.size() > Mesh.ARRAY_INDEX and arrays[Mesh.ARRAY_INDEX] != null:
					tris += arrays[Mesh.ARRAY_INDEX].size() / 3
				elif arrays.size() > Mesh.ARRAY_VERTEX and arrays[Mesh.ARRAY_VERTEX] != null:
					tris += arrays[Mesh.ARRAY_VERTEX].size() / 3
	_info("whole level: %d triangles, %d surfaces (budget per view: %d tris, %d draw calls)" % [tris, surfaces, TRIANGLE_BUDGET, DRAW_CALL_BUDGET])
	if tris > TRIANGLE_BUDGET or surfaces > DRAW_CALL_BUDGET:
		_warn("whole-level totals exceed the per-view budget; profile from each camera position before dressing further")


# --- helpers -----------------------------------------------------------------

func _site_aabb(site: Node3D) -> AABB:
	var size: Vector3 = site.get("ClearanceSize")
	var xf: Transform3D = site.get("ClearanceTransform")
	var local := AABB(-size * 0.5, size)
	return xf * local


## Closest point to p on the region's baked mesh, computed from the polygons
## themselves. The NavigationServer map does not reliably take a mesh baked
## after the region entered the tree in script mode, so the checklist never
## asks it.
func _closest_point_on_navmesh(region: NavigationRegion3D, p: Vector3) -> Vector3:
	var mesh := region.navigation_mesh
	var verts := mesh.get_vertices()
	var xf := region.global_transform
	var best := Vector3.ZERO
	var best_d := INF
	for i in range(mesh.get_polygon_count()):
		var poly := mesh.get_polygon(i)
		for k in range(1, poly.size() - 1):
			var a: Vector3 = xf * verts[poly[0]]
			var b: Vector3 = xf * verts[poly[k]]
			var c: Vector3 = xf * verts[poly[k + 1]]
			var q := _closest_point_on_triangle(p, a, b, c)
			var d := q.distance_squared_to(p)
			if d < best_d:
				best_d = d
				best = q
	return best


func _closest_point_on_triangle(p: Vector3, a: Vector3, b: Vector3, c: Vector3) -> Vector3:
	var n := (b - a).cross(c - a)
	if n.length_squared() < 1e-12:
		return Geometry3D.get_closest_point_to_segment(p, a, b)
	n = n.normalized()
	var q := p - n * (p - a).dot(n)
	if Geometry3D.ray_intersects_triangle(q + n, -n, a, b, c) != null:
		return q
	var e1 := Geometry3D.get_closest_point_to_segment(p, a, b)
	var e2 := Geometry3D.get_closest_point_to_segment(p, b, c)
	var e3 := Geometry3D.get_closest_point_to_segment(p, c, a)
	var best := e1
	for e in [e2, e3]:
		if e.distance_squared_to(p) < best.distance_squared_to(p):
			best = e
	return best


## Interiors (architecture.md §7.4) live under levels/district/interiors/ and have one spawn per door, no 'arrival'.
func _is_interior(root: Node) -> bool:
	return str(root.scene_file_path).contains("/interiors/")


func _navmesh_ready(root: Node) -> bool:
	var region := _find_nav_region(root)
	return region != null and region.navigation_mesh != null and region.navigation_mesh.get_polygon_count() > 0


func _find_nav_region(root: Node) -> NavigationRegion3D:
	for node in _all_nodes(root):
		if node is NavigationRegion3D:
			return node
	return null


func _children_of_type(root: Node, type_name: String) -> Array:
	var out := []
	for child in root.get_children():
		if child.is_class(type_name):
			out.append(child)
	return out


func _nodes_with_script(root: Node, class_file: String) -> Array:
	var out := []
	var suffix := "/World/%s.cs" % class_file
	for node in _all_nodes(root):
		var script = node.get_script()
		if script != null and str(script.resource_path).ends_with(suffix):
			out.append(node)
	return out


func _all_nodes(root: Node) -> Array:
	var out := [root]
	var i := 0
	while i < out.size():
		for child in out[i].get_children():
			out.append(child)
		i += 1
	return out


func _check(ok: bool, text: String) -> void:
	if ok:
		_pass(text)
	else:
		_fail(text)


func _pass(text: String) -> void:
	_results.append({"level": "PASS", "text": text})


func _fail(text: String) -> void:
	_results.append({"level": "FAIL", "text": text})


func _warn(text: String) -> void:
	_results.append({"level": "WARN", "text": text})


func _info(text: String) -> void:
	_results.append({"level": "INFO", "text": text})
