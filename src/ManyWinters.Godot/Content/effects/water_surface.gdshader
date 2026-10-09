shader_type spatial;
render_mode cull_disabled;

// Water lying over the terrain (TerrainRenderer.cs): water areas and river ribbons alike. UV.x
// carries the distance in from the water's edge in metres, as for surface_layer.gdshader,
// which cuts the edge smooth here too and also tells shallow water from deep.
//
// The surface itself is flat; its life is in the light. Two fields of ripples drift across it
// in different directions and tilt the normals, so the sheen off a glossy surface breaks up
// and shifts instead of lying on it as one even coat of paint.

uniform vec4 shallow_color : source_color = vec4(0.33, 0.45, 0.48, 0.7);
uniform vec4 deep_color : source_color = vec4(0.16, 0.26, 0.32, 0.9);

// How far in from the edge the water reaches its deep colour; the ground's shelf under it
// deepens over about this distance too.
uniform float shelf_meters = 3.0;

// Ripple size and drift: long slow swell under a shorter, quicker chop.
const float SWELL_FREQUENCY = 0.22;
const float CHOP_FREQUENCY = 0.8;
const vec2 SWELL_DRIFT = vec2(0.12, 0.05);
const vec2 CHOP_DRIFT = vec2(-0.09, 0.21);
const float RIPPLE_STRENGTH = 0.35;
const float SLOPE_STEP = 0.05;

// Metres per pixel past which ripples fade out: smaller than a pixel, they only alias into
// glitter that flickers as the camera moves.
const float RIPPLE_FADE_METERS_PER_PIXEL = 0.35;

varying vec2 world_xz;

// Hash without sine (Hoskins), over a lattice cell wrapped to a small range. The usual
// fract(sin(...) * 43758.5) loses its precision at world coordinates in the hundreds: two
// evaluations of one cell then disagree, and the noise breaks into visible squares.
float hash(vec2 cell) {
    vec3 p3 = fract(vec3(mod(cell, 289.0).xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

float value_noise(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash(cell), hash(cell + vec2(1.0, 0.0)), u.x),
        mix(hash(cell + vec2(0.0, 1.0)), hash(cell + vec2(1.0, 1.0)), u.x),
        u.y);
}

// Height of the ripples in metres-ish units: what matters is only their slope.
float ripples(vec2 p, float time) {
    float swell = value_noise(p * SWELL_FREQUENCY + time * SWELL_DRIFT);
    float chop = value_noise(mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)) * p * CHOP_FREQUENCY + time * CHOP_DRIFT);
    return 0.65 * swell + 0.35 * chop;
}

void vertex() {
    world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    float edge = max(fwidth(UV.x), 0.0001);
    float depth = clamp(UV.x / shelf_meters, 0.0, 1.0);

    // The surface is level, so the rippled normal can be built in world space straight from
    // the ripple field's slope, sampled a few centimetres either way. Screen-space derivatives
    // would do it per 2x2 pixel block, which shows as a sawtooth in every glint.
    float here = ripples(world_xz, TIME);
    float slope_x = (ripples(world_xz + vec2(SLOPE_STEP, 0.0), TIME) - here) / SLOPE_STEP;
    float slope_z = (ripples(world_xz + vec2(0.0, SLOPE_STEP), TIME) - here) / SLOPE_STEP;
    float resolved = 1.0 - smoothstep(0.0, RIPPLE_FADE_METERS_PER_PIXEL, length(fwidth(world_xz)));
    float strength = RIPPLE_STRENGTH * resolved;
    vec3 world_normal = normalize(vec3(-slope_x * strength, 1.0, -slope_z * strength));
    NORMAL = normalize((VIEW_MATRIX * vec4(world_normal, 0.0)).xyz);

    vec4 color = mix(shallow_color, deep_color, depth);
    ALBEDO = color.rgb;
    ROUGHNESS = 0.25;
    SPECULAR = 0.6;
    ALPHA = color.a * smoothstep(-edge, edge, UV.x);
}
