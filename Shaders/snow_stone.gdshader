shader_type spatial;
uniform vec4 stone_color : source_color = vec4(0.48,0.49,0.46,1.0);
uniform float snow_cover = 0.0;
varying vec3 world_normal;
void vertex() { world_normal = normalize(MODEL_NORMAL_MATRIX * NORMAL); }
void fragment() {
    float cover = snow_cover * smoothstep(0.05,0.65,world_normal.y);
    ALBEDO = mix(stone_color.rgb,vec3(0.88,0.92,0.96),cover);
    ROUGHNESS = 0.94;
    SPECULAR = 0.1;
}
