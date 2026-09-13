// Inverted-hull outline: the second pass of shaders/materials/toon.tres
// (issue #27). The mesh is drawn again with front faces culled and every
// vertex pushed out along its normal, so the back faces show as a contour
// around the silhouette. Hard-edged meshes (kit cubes) show small gaps at the
// corners; smooth-shaded characters and props are continuous.
shader_type spatial;
render_mode cull_front, unshaded, depth_draw_opaque, shadows_disabled;

// direction.md palette: outline / dark fabric #293344.
uniform vec4 outline_color : source_color = vec4(0.161, 0.2, 0.267, 1.0);
// Contour thickness in metres at reference_distance (0.015 m ≈ 2 px at 1080p
// from the 12 m overworld camera).
uniform float outline_width : hint_range(0.0, 0.05, 0.001) = 0.015;
// 0 = constant thickness in metres (thinner far away); 1 = constant thickness
// on screen (grows with distance). The overworld camera sits 12–14 m away.
uniform float distance_scale : hint_range(0.0, 1.0) = 0.8;
uniform float reference_distance : hint_range(1.0, 30.0) = 12.0;

void vertex() {
	vec4 view_pos = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	float distance = max(-view_pos.z, 0.1);
	float scale = mix(1.0, distance / reference_distance, distance_scale);
	VERTEX += normalize(NORMAL) * outline_width * scale;
}

void fragment() {
	ALBEDO = outline_color.rgb;
}
