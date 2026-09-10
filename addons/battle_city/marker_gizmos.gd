@tool
extends EditorNode3DGizmoPlugin
## Editor gizmos for the marker scenes (architecture.md §7.1): the EncounterSite
## footprint, axis and stand points; the Duelist detection cone; the PlayerSpawn
## facing arrow; the Door interact side.

const SCRIPTS := {
	"EncounterSite": "site",
	"Duelist": "cone",
	"PlayerSpawn": "spawn",
	"Door": "door",
}


func _init() -> void:
	create_material("site", Color(0.3, 0.9, 0.4))
	create_material("cone", Color(1.0, 0.6, 0.2))
	create_material("spawn", Color(0.3, 0.8, 1.0))
	create_material("door", Color(0.5, 0.6, 1.0))


func _get_gizmo_name() -> String:
	return "Battle City markers"


func _has_gizmo(node: Node3D) -> bool:
	return _kind(node) != ""


func _kind(node: Node3D) -> String:
	var script := node.get_script()
	if script == null:
		return ""
	var path: String = script.resource_path
	for name in SCRIPTS:
		if path.ends_with("/World/%s.cs" % name):
			return name
	return ""


func _redraw(gizmo: EditorNode3DGizmo) -> void:
	gizmo.clear()
	var node := gizmo.get_node_3d()
	var kind := _kind(node)
	var lines := PackedVector3Array()
	match kind:
		"EncounterSite":
			_encounter_site(node, lines)
		"Duelist":
			_cone(node, lines)
		"PlayerSpawn":
			_arrow(lines, Vector3(0, 0.1, 0), Vector3(0, 0, -1), 1.0)
			_circle(lines, Vector3(0, 0.05, 0), 0.35)
		"Door":
			_arrow(lines, Vector3(0, 0.1, 0), Vector3(0, 0, 1), 1.0)
	if lines.size() > 0:
		gizmo.add_lines(lines, get_material(SCRIPTS[kind], gizmo), false)
		gizmo.add_collision_segments(lines)


func _encounter_site(node: Node3D, lines: PackedVector3Array) -> void:
	var size: Vector3 = node.get("ClearanceSize")
	var axis: Vector3 = node.get("Axis")
	var stand: float = node.get("StandDistance")
	var hx := size.x * 0.5
	var hz := size.z * 0.5
	var y := 0.05
	_rect(lines, Vector3(-hx, y, -hz), Vector3(hx, y, hz))
	_rect(lines, Vector3(-hx, size.y, -hz), Vector3(hx, size.y, hz))
	for corner in [Vector3(-hx, 0, -hz), Vector3(hx, 0, -hz), Vector3(hx, 0, hz), Vector3(-hx, 0, hz)]:
		lines.append(corner + Vector3(0, y, 0))
		lines.append(corner + Vector3(0, size.y, 0))
	var a := axis.normalized()
	var pa := -a * stand * 0.5
	var pb := a * stand * 0.5
	lines.append(pa + Vector3(0, y, 0))
	lines.append(pb + Vector3(0, y, 0))
	_circle(lines, pa + Vector3(0, y, 0), 0.5)
	_circle(lines, pb + Vector3(0, y, 0), 0.5)
	_arrow(lines, pb + Vector3(0, y, 0), a, 0.8)


func _cone(node: Node3D, lines: PackedVector3Array) -> void:
	var range_m: float = node.get("ConeRange")
	var angle: float = node.get("ConeAngle")
	var half := deg_to_rad(angle * 0.5)
	var y := 0.1
	var steps := 12
	var prev := Vector3.ZERO
	for i in range(steps + 1):
		var t := -half + (2.0 * half) * float(i) / float(steps)
		var p := Vector3(-sin(t) * range_m, y, -cos(t) * range_m)
		if i == 0 or i == steps:
			lines.append(Vector3(0, y, 0))
			lines.append(p)
		if i > 0:
			lines.append(prev)
			lines.append(p)
		prev = p


func _rect(lines: PackedVector3Array, lo: Vector3, hi: Vector3) -> void:
	var y := lo.y
	var pts := [Vector3(lo.x, y, lo.z), Vector3(hi.x, y, lo.z), Vector3(hi.x, y, hi.z), Vector3(lo.x, y, hi.z)]
	for i in range(4):
		lines.append(pts[i])
		lines.append(pts[(i + 1) % 4])


func _circle(lines: PackedVector3Array, centre: Vector3, radius: float) -> void:
	var steps := 16
	for i in range(steps):
		var t0 := TAU * float(i) / steps
		var t1 := TAU * float(i + 1) / steps
		lines.append(centre + Vector3(cos(t0) * radius, 0, sin(t0) * radius))
		lines.append(centre + Vector3(cos(t1) * radius, 0, sin(t1) * radius))


func _arrow(lines: PackedVector3Array, from: Vector3, dir: Vector3, length: float) -> void:
	var d := dir.normalized()
	var tip := from + d * length
	var side := d.cross(Vector3.UP) * 0.2
	lines.append(from)
	lines.append(tip)
	lines.append(tip)
	lines.append(tip - d * 0.3 + side)
	lines.append(tip)
	lines.append(tip - d * 0.3 - side)
