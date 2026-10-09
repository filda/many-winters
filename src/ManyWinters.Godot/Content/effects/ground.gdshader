shader_type spatial;
render_mode cull_disabled;

// The ground mesh (TerrainRenderer.cs). One grass texture tiled over a square kilometre
// repeats visibly, and one colour over all of it reads as a lawn rather than land, so this
// shader does two things with it:
//
// - It breaks the tiling: the texture is sampled twice, at two scales and turned against
//   each other, and a broad noise field blends between the two, so no repeat lines up.
// - It recolours the grass by where it grows, keeping only the texture's light and dark as
//   detail: lush and dark beside water, yellowed on high dry ground and in drifting dry
//   patches, bare earth showing on steep slopes.
//
// Per vertex the mesh carries: COLOR.r the elevation as a fraction of the patch's range,
// UV2.x the distance to the nearest water in metres, UV2.y the slope of the real elevation
// (rise over run, without the procedural bump - the bump would scatter earth everywhere).

uniform sampler2D ground_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform float tile_meters = 16.0;

// Mean luminance of the ground texture once linearised, so detail = 1 on its average texel.
const float TEXTURE_MEAN_LUMINANCE = 0.0974;

// Linear albedo, kept in the range the scene's lighting is set up for: the ground is meant to
// sit dark under the sprites standing on it.
const vec3 LUSH = vec3(0.024, 0.042, 0.007);
const vec3 WATERSIDE = vec3(0.016, 0.034, 0.009);
const vec3 DRY = vec3(0.055, 0.050, 0.014);
const vec3 EARTH = vec3(0.042, 0.030, 0.014);

// How far from water the grass still drinks from it.
const float WATERSIDE_METERS = 25.0;

// Slope (rise over run) where earth starts to show, and where it has taken over. The 25 m
// elevation grid smooths real slopes out: the steepest tenth of the shipped patch lies
// between about 0.17 and 0.3.
const float EARTH_FROM_SLOPE = 0.15;
const float EARTH_FULL_SLOPE = 0.3;

varying vec2 world_xz;
varying float elevation;
varying float water_distance;
varying float slope;

// As in water_surface.gdshader: a hash without sine, safe at world coordinates.
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

float broad_noise(vec2 p) {
    return 0.6 * value_noise(p) + 0.3 * value_noise(p * 2.3 + vec2(5.2, 1.3)) + 0.1 * value_noise(p * 5.1 + vec2(2.7, 8.1));
}

void vertex() {
    world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
    elevation = COLOR.r;
    water_distance = UV2.x;
    slope = UV2.y;
}

void fragment() {
    vec2 near_uv = world_xz / tile_meters;
    vec2 far_uv = mat2(vec2(0.82, 0.57), vec2(-0.57, 0.82)) * world_xz / (tile_meters * 2.7) + vec2(0.37, 0.71);
    float blend = smoothstep(0.35, 0.65, broad_noise(world_xz * 0.025));
    vec3 texel = mix(texture(ground_texture, near_uv).rgb, texture(ground_texture, far_uv).rgb, blend);
    float detail = dot(texel, vec3(0.299, 0.587, 0.114)) / TEXTURE_MEAN_LUMINANCE;

    float wet = 1.0 - smoothstep(0.0, WATERSIDE_METERS, water_distance);
    float dryness = clamp(1.4 * broad_noise(world_xz * 0.012 + vec2(13.0, 7.0)) - 0.55 + 0.45 * elevation - 1.2 * wet, 0.0, 1.0);
    vec3 grass = mix(LUSH, DRY, dryness);
    grass = mix(grass, WATERSIDE, wet * 0.7);

    float bare = smoothstep(EARTH_FROM_SLOPE, EARTH_FULL_SLOPE, slope);
    vec3 color = mix(grass, EARTH, bare);

    // A slow swell of light and dark over tens of metres, so even one kind of ground is not
    // one flat tone.
    color *= 0.85 + 0.3 * broad_noise(world_xz * 0.04 + vec2(3.0, 11.0));

    ALBEDO = color * detail;
}
