shader_type canvas_item;
render_mode blend_add;
uniform vec2 sun_uv = vec2(0.8, 0.2);
uniform float aspect = 1.6;
uniform float strength = 0.4;
void fragment() {
    vec2 p = UV;
    vec2 axis = vec2(0.5) - sun_uv;
    vec2 d = (p - sun_uv) * vec2(aspect, 1.0);
    float glow = exp(-length(d) * 40.0) * 0.75;
    vec2 d1 = (p - (sun_uv + axis * 1.4)) * vec2(aspect, 1.0);
    vec2 d2 = (p - (sun_uv + axis * 1.9)) * vec2(aspect, 1.0);
    float orb1 = (1.0 - smoothstep(0.02, 0.045, length(d1))) * 0.10;
    float orb2 = (1.0 - smoothstep(0.04, 0.075, length(d2))) * 0.06;
    COLOR = vec4((vec3(1.0, 0.80, 0.50) * glow + vec3(0.4, 0.75, 1.0) * orb1 + vec3(1.0, 0.45, 0.25) * orb2) * strength, 1.0);
}
