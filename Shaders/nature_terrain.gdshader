shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform sampler2D grass_color : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D grass_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D grass_rough : filter_linear_mipmap, repeat_enable;
uniform sampler2D sand_color : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D sand_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D sand_rough : filter_linear_mipmap, repeat_enable;
uniform sampler2D soil_color : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D soil_normal : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D soil_rough : filter_linear_mipmap, repeat_enable;
varying vec3 world_pos;
varying vec3 world_normal;

vec3 tri_sample(sampler2D tex, vec3 p, vec3 w) {
	vec3 value = vec3(0.0);
	if (w.x > 0.0) value += texture(tex,p.zy).rgb*w.x;
	if (w.y > 0.0) value += texture(tex,p.xz).rgb*w.y;
	if (w.z > 0.0) value += texture(tex,p.xy).rgb*w.z;
	return value;
}
float tri_rough(sampler2D tex, vec3 p, vec3 w) {
	float value = 0.0;
	if (w.x > 0.0) value += texture(tex,p.zy).r*w.x;
	if (w.y > 0.0) value += texture(tex,p.xz).r*w.y;
	if (w.z > 0.0) value += texture(tex,p.xy).r*w.z;
	return value;
}
vec3 tri_normal(sampler2D tex, vec3 p, vec3 w, vec3 n) {
	vec3 value = vec3(0.0);
	if (w.x > 0.0) {
		vec3 x = texture(tex,p.zy).rgb*2.0-1.0;
		x.z = sqrt(max(0.001,1.0-dot(x.xy,x.xy)));
		value += vec3(x.z*sign(n.x),x.y,x.x)*w.x;
	}
	if (w.y > 0.0) {
		vec3 y = texture(tex,p.xz).rgb*2.0-1.0;
		y.z = sqrt(max(0.001,1.0-dot(y.xy,y.xy)));
		value += vec3(y.x,y.z*sign(n.y),y.y)*w.y;
	}
	if (w.z > 0.0) {
		vec3 z = texture(tex,p.xy).rgb*2.0-1.0;
		z.z = sqrt(max(0.001,1.0-dot(z.xy,z.xy)));
		value += vec3(z.x,z.y,z.z*sign(n.z))*w.z;
	}
	return normalize(value);
}
void vertex() {
	world_pos = (MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;
	world_normal = normalize(MODEL_NORMAL_MATRIX*NORMAL);
}
void fragment() {
	vec3 n = normalize(world_normal);
	vec3 w = pow(abs(n),vec3(4.0));
	w *= step(vec3(0.001),w);
	w /= max(w.x+w.y+w.z,0.0001);
	vec3 p = world_pos*0.5;
	float sand = COLOR.r*(1.0-COLOR.b);
	float soil = max(COLOR.g,COLOR.b);
	vec3 grass = tri_sample(grass_color,p,w);
	vec3 earth = tri_sample(soil_color,p*0.8,w);
	vec3 beach = tri_sample(sand_color,p,w);
	float macro = sin(world_pos.x*0.39+sin(world_pos.z*0.28))*0.035;
	ALBEDO = mix(mix(grass,earth,soil),beach,sand)*(0.72+macro);
	float strata = sin(world_pos.y*3.2+sin(world_pos.x*0.16)+sin(world_pos.z*0.13))*0.035;
	vec3 stone = vec3(0.28,0.29,0.275)*(0.72+dot(earth,vec3(0.22)));
	ALBEDO = mix(ALBEDO,stone+vec3(strata),COLOR.a);
	ALBEDO = mix(ALBEDO,vec3(0.34,0.16,0.065)*(0.85+strata),COLOR.a*COLOR.b);
	vec3 detail = mix(tri_normal(grass_normal,p,w,n),tri_normal(soil_normal,p*0.8,w,n),soil);
	detail = normalize(mix(detail,tri_normal(sand_normal,p,w,n),sand));
	NORMAL = normalize((VIEW_MATRIX*vec4(normalize(mix(n,detail,mix(0.32,0.16,COLOR.a))),0.0)).xyz);
	ROUGHNESS = clamp(mix(mix(tri_rough(grass_rough,p,w),tri_rough(soil_rough,p*0.8,w),soil),tri_rough(sand_rough,p,w),sand),0.65,1.0);
	SPECULAR = 0.12;
}
