shader_type spatial;
render_mode blend_mix, cull_disabled;
uniform vec4 deep_color : source_color = vec4(0.025, 0.24, 0.34, 0.94);
varying vec2 water_world;
varying float shore_depth;
void vertex() {
    shore_depth = COLOR.r;
    water_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
    float wave_fade = smoothstep(0.0, 0.25, shore_depth);
    VERTEX.y += wave_fade * ( sin(water_world.x * 2.2 + TIME * 1.4) * 0.014
        + cos(water_world.y * 1.8 - TIME * 1.1) * 0.012);
    NORMAL = normalize(vec3(-cos(water_world.x * 2.2 + TIME * 1.4) * 0.0308,
        1.0, sin(water_world.y * 1.8 - TIME * 1.1) * 0.0216));
}
void fragment() {
    float fresnel = pow(1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), 3.0);
    float ripple = sin(water_world.x * 3.0 + water_world.y * 2.0 + TIME) * 0.5 + 0.5;
    ALBEDO = mix(deep_color.rgb, vec3(0.35, 0.67, 0.72), fresnel * 0.65 + ripple * 0.08);
    ROUGHNESS = 0.55;
    SPECULAR = 0.25;
    ALPHA = deep_color.a * smoothstep(0.0, 0.18, shore_depth);
}
