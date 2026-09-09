// World-space metre grid for diagnostic scenes (SmokeTest, CameraFraming).
// 1 m major lines, 0.25 m minor lines: the layout and detail grids from
// architecture.md §7 and direction.md. Not used in shipped scenes.
shader_type spatial;
render_mode cull_disabled;

uniform vec4 base_color : source_color = vec4(0.16, 0.17, 0.19, 1.0);
uniform vec4 minor_color : source_color = vec4(0.21, 0.23, 0.26, 1.0);
uniform vec4 major_color : source_color = vec4(0.42, 0.45, 0.5, 1.0);
uniform vec4 axis_color : source_color = vec4(0.85, 0.45, 0.25, 1.0);
uniform float major_step = 1.0;
uniform float minor_step = 0.25;
uniform float line_width = 0.015;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float grid_line(vec2 p, float step, float width) {
	vec2 dist = abs(fract(p / step - 0.5) - 0.5) * step;
	vec2 fw = fwidth(p);
	vec2 line = 1.0 - smoothstep(width - fw, width + fw, dist);
	return max(line.x, line.y);
}

float axis_line(float coord, float width) {
	float fw = fwidth(coord);
	return 1.0 - smoothstep(width - fw, width + fw, abs(coord));
}

void fragment() {
	vec2 p = world_pos.xz;
	float minor = grid_line(p, minor_step, line_width * 0.5);
	float major = grid_line(p, major_step, line_width);
	float axis = max(axis_line(p.x, line_width * 1.5), axis_line(p.y, line_width * 1.5));
	vec3 color = mix(base_color.rgb, minor_color.rgb, minor);
	color = mix(color, major_color.rgb, major);
	color = mix(color, axis_color.rgb, axis);
	ALBEDO = color;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
