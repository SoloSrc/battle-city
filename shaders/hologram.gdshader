// Holographic card material for DuelStaging card anchors (issue #27,
// systems.md §6.1, asset-list §4.4, sheet 03). A card is a 0.20 × 0.29 m quad
// with the composed card face in `face_texture` and the card back on the
// reverse. The face stays readable (opaque-ish, lightly tinted by the side
// colour) with a narrow emissive edge, moving scanlines, a little flicker and
// a hover bob. Per-card state (hover phase, selected, reveal, dissolve) is set
// with instance shader parameters, so every card of a side shares one material:
// shaders/materials/hologram_player.tres / hologram_opponent.tres.
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_prepass_alpha, world_vertex_coords, shadows_disabled;

group_uniforms card;
uniform sampler2D face_texture : source_color, hint_default_white, filter_linear_mipmap;
uniform sampler2D back_texture : source_color, hint_default_black, filter_linear_mipmap;
// Opacity of the printed face; the edge glow is always fully visible.
uniform float face_opacity : hint_range(0.0, 1.0) = 0.88;
// How much the side colour washes over the face (0 = untinted print).
uniform float tint_strength : hint_range(0.0, 1.0) = 0.12;

group_uniforms edge;
// Side colour (direction.md: player #79D8FF, opponent #FFA18B).
uniform vec4 edge_color : source_color = vec4(0.475, 0.847, 1.0, 1.0);
// Edge band width as a fraction of the card width.
uniform float edge_width : hint_range(0.0, 0.2) = 0.035;
// Emissive multiplier; above 1 the edge blooms when glow is enabled.
uniform float edge_glow : hint_range(0.0, 8.0) = 1.8;
// Extra edge brightness when the instance is selected.
uniform float selected_boost : hint_range(1.0, 4.0) = 1.8;

group_uniforms scanlines;
uniform float scanline_density : hint_range(0.0, 400.0) = 120.0;
uniform float scanline_strength : hint_range(0.0, 1.0) = 0.16;
uniform float scanline_speed : hint_range(-5.0, 5.0) = 0.35;
uniform float flicker_strength : hint_range(0.0, 0.5) = 0.04;
uniform float flicker_speed : hint_range(0.0, 40.0) = 11.0;

group_uniforms hover;
// Vertical bob in metres and cycles per second.
uniform float hover_amplitude : hint_range(0.0, 0.1) = 0.012;
uniform float hover_speed : hint_range(0.0, 5.0) = 0.9;

group_uniforms per_card;
// Offsets the bob so neighbouring cards do not move in step (0–1 turn).
instance uniform float hover_phase : hint_range(0.0, 1.0) = 0.0;
// 1 while the cursor is on the card (asset-list §6.3 "edge brighten").
instance uniform float selected : hint_range(0.0, 1.0) = 0.0;
// Materialise wipe from the bottom, 0 = invisible, 1 = whole card (§6.2).
instance uniform float reveal : hint_range(0.0, 1.0) = 1.0;
// End dissolve, 0 = intact, 1 = gone (§6.7).
instance uniform float dissolve : hint_range(0.0, 1.0) = 0.0;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void vertex() {
	float bob = sin((TIME * hover_speed + hover_phase) * TAU) * hover_amplitude;
	VERTEX.y += bob;
}

void fragment() {
	vec2 uv = UV;
	vec4 print = FRONT_FACING ? texture(face_texture, uv) : texture(back_texture, vec2(1.0 - uv.x, uv.y));
	vec3 side = edge_color.rgb;

	// Narrow emissive frame along all four edges, in card-width units (aspect 590:860).
	float aspect = 860.0 / 590.0;
	vec2 dist = min(uv, 1.0 - uv) * vec2(1.0, aspect);
	float border = min(dist.x, dist.y);
	float edge = 1.0 - smoothstep(edge_width * 0.6, edge_width, border);

	// Scanlines drift down the card; the flicker is a slow shimmer, not strobe.
	float scan = 0.5 + 0.5 * sin((uv.y * scanline_density - TIME * scanline_speed * 10.0) * TAU);
	float flicker = 1.0 - flicker_strength * (0.5 + 0.5 * sin(TIME * flicker_speed + hover_phase * TAU));
	float lines = 1.0 - scanline_strength * scan;

	// Materialise: rows above the reveal line are cut, the line itself glows.
	float reveal_line = smoothstep(reveal - 0.06, reveal, uv.y) * step(uv.y, reveal + 0.001);
	float revealed = step(uv.y, reveal);

	// Dissolve: noise threshold with a glowing rim just before a pixel goes.
	float noise = value_noise(uv * vec2(9.0, 13.0));
	float gone = step(noise, dissolve * 1.05);
	float burning = smoothstep(dissolve * 1.05 - 0.08, dissolve * 1.05, noise) * (1.0 - gone) * step(0.001, dissolve);

	vec3 face = mix(print.rgb, print.rgb * side, tint_strength) * lines * flicker;
	float glow_amount = max(edge, max(reveal_line, burning)) * (1.0 + selected * (selected_boost - 1.0));
	vec3 glow = side * edge_glow * glow_amount;

	float alpha = max(print.a * face_opacity, edge) * revealed * (1.0 - gone);
	// Unshaded materials output ALBEDO only (EMISSION is ignored), so the glow
	// is added here; values above 1.0 still bloom when the environment has glow.
	ALBEDO = face * (1.0 - edge * 0.85) + glow * flicker;
	ALPHA = alpha;
	ALPHA_SCISSOR_THRESHOLD = 0.05;
}
