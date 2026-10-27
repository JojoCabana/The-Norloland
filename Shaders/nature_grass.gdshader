shader_type spatial;
render_mode cull_disabled, diffuse_lambert_wrap;
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D alpha_texture : filter_linear_mipmap, repeat_enable;
uniform bool use_alpha = true;
uniform float wind_strength = 0.14;
uniform vec3 uv_scale = vec3(1.0);
uniform vec3 uv_offset = vec3(0.0);
uniform bool blade_geometry = false;
varying float tint;
void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	float height = clamp(VERTEX.y,0.0,1.0);
	float wind = sin(TIME*1.2+origin.x*0.45+origin.z*0.32)+sin(TIME*2.3+origin.z*0.8)*0.3;
	VERTEX.x += wind*height*wind_strength;
	VERTEX.z += wind*height*wind_strength*0.4;
	tint = 0.86+sin(origin.x*13.1+origin.z*7.7)*0.09;
}
void fragment() {
	vec2 coords = UV*uv_scale.xy+uv_offset.xy;
	if (blade_geometry) {
		ALBEDO = mix(vec3(0.065,0.095,0.03),vec3(0.29,0.36,0.12),smoothstep(0.0,0.85,UV.y))*tint;
		NORMAL_MAP = vec3(0.5,0.5,1.0);
		ALPHA = 1.0;
	} else {
		ALBEDO = texture(albedo_texture,coords).rgb*tint;
		NORMAL_MAP = texture(normal_texture,coords).rgb;
		ALPHA = use_alpha ? texture(alpha_texture,coords).r : 1.0;
	}
	ALPHA_SCISSOR_THRESHOLD = 0.4;
	NORMAL_MAP_DEPTH = 0.25;
	ROUGHNESS = 0.93;
	SPECULAR = 0.1;
	BACKLIGHT = ALBEDO*0.25;
}
