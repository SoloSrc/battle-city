// Shared cel shader for characters, props and kit (issue #27, style-brief.md,
// architecture.md §6.4). One ShaderMaterial per source material named toon_*,
// created at load by src/Rendering/ToonMaterials.cs from shaders/materials/toon.tres:
// the source colour and albedo texture are copied into `albedo` / `albedo_texture`
// and every other parameter is shared. The outline is the material's next_pass
// (shaders/outline.gdshader). Forward+ only. Parameters are documented in
// shaders/README.md; gpt-astra tunes them on the .tres, not here.
shader_type spatial;
render_mode cull_back, depth_draw_opaque, specular_disabled;

group_uniforms surface;
// Base colour; multiplies albedo_texture. Skin tone and accent colour are this
// value on the materials made from toon_skin and toon_accent.
uniform vec4 albedo : source_color = vec4(1.0);
uniform sampler2D albedo_texture : source_color, hint_default_white, filter_linear_mipmap;

group_uniforms shading;
// Number of light bands from shadow to lit (2 = flat two-tone, 3 = the brief).
uniform int bands : hint_range(1, 5) = 3;
// Width of the blend between bands, in light units (0 = hard edge).
uniform float band_softness : hint_range(0.0, 0.25) = 0.03;
// Where the fully lit band starts in n·l space; below it the surface steps down.
uniform float lit_threshold : hint_range(0.0, 1.0) = 0.55;
// Brightness of the darkest band relative to the lit one.
uniform float shade_floor : hint_range(0.0, 1.0) = 0.38;
// Colour kept in shadow (direction.md: "retain colour in shadow"), multiplied in.
uniform vec4 shade_tint : source_color = vec4(0.70, 0.74, 0.90, 1.0);

group_uniforms rim;
// Light-side rim, a thin lift along the silhouette facing the light.
uniform float rim_strength : hint_range(0.0, 2.0) = 0.35;
uniform float rim_width : hint_range(0.0, 1.0) = 0.22;
uniform vec4 rim_color : source_color = vec4(1.0, 0.98, 0.92, 1.0);

group_uniforms specular;
// Small cel highlight; 0 turns it off. Kept low: the brief wants matte skin
// and fabric, not glossy plastic.
uniform float specular_strength : hint_range(0.0, 1.0) = 0.05;
uniform float specular_size : hint_range(0.0, 0.5) = 0.03;

void fragment() {
	vec4 tex = texture(albedo_texture, UV);
	ALBEDO = albedo.rgb * tex.rgb;
	ROUGHNESS = 1.0;
	METALLIC = 0.0;
	SPECULAR = 0.0;
}

// Quantises n·l into `bands` steps with a soft edge between them.
float cel(float ndl) {
	float x = clamp(ndl / max(lit_threshold, 0.001), 0.0, 1.0);
	float steps = float(max(bands - 1, 1));
	if (bands <= 1) {
		return 1.0;
	}
	float scaled = x * steps;
	float base = floor(scaled);
	float t = scaled - base;
	float soft = max(band_softness * steps, 0.0001);
	float edge = smoothstep(0.5 - soft, 0.5 + soft, t);
	return clamp((base + edge) / steps, 0.0, 1.0);
}

void light() {
	float ndl = dot(NORMAL, LIGHT);
	float lit = cel(clamp(ndl, 0.0, 1.0) * ATTENUATION);
	vec3 shade = shade_tint.rgb * shade_floor;
	vec3 diffuse = mix(shade, vec3(1.0), lit);
	// LIGHT_COLOR carries energy × PI (Godot's physically based convention),
	// so /PI makes the lit band match a Lambert surface under the same sun.
	DIFFUSE_LIGHT += diffuse * LIGHT_COLOR / PI;

	float rim = 1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	rim = smoothstep(1.0 - rim_width, 1.0 - rim_width + 0.08, rim);
	rim *= smoothstep(0.0, 0.35, ndl) * ATTENUATION;
	DIFFUSE_LIGHT += rim * rim_strength * rim_color.rgb * LIGHT_COLOR / PI;

	vec3 half_dir = normalize(VIEW + LIGHT);
	float ndh = clamp(dot(NORMAL, half_dir), 0.0, 1.0);
	float spec = smoothstep(1.0 - specular_size, 1.0 - specular_size + 0.02, ndh);
	SPECULAR_LIGHT += spec * specular_strength * ATTENUATION * LIGHT_COLOR / PI;
}
