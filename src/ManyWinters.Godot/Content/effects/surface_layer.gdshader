shader_type spatial;
render_mode cull_disabled;

// A rock layer lying over the terrain (TerrainRenderer.cs). UV.x carries the signed
// distance to the feature's edge in metres, positive inside; interpolated across a triangle
// its zero line is the edge, which therefore runs smooth through the grid's cells instead of
// stepping along them. fwidth keeps the fade one pixel wide at any zoom.
//
// Edge wobble pushes that edge in and out along its length, so a mapped outline - a cliff is
// a line buffered to a strip of even width - does not read as a road. The mesh has to reach
// past the true edge by at least this much, or the wobble is cut off where the mesh ends.
//
// Ruggedness roughens the surface without moving it: a noise field mottles the colour and
// tilts the normals as if the surface were broken into lumps and ledges. Raising real geometry
// would bury whatever stands on the outcrop, since everything is placed on the bare ground.

uniform vec4 albedo : source_color = vec4(1.0);
uniform float ruggedness = 0.0;
uniform float edge_wobble = 0.0;

// Lumps about a metre or two across: finer reads as gravel, not as broken rock.
const float LUMP_FREQUENCY = 0.6;

// Bays and spurs some 5-10 m long, with a finer ripple on top.
const float WOBBLE_FREQUENCY = 0.15;

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

// Three octaves, each rotated off the last so the lumps don't line up along the grid axes. The
// first is ridged (folded at the middle), so the low lines between lumps come out as cracks,
// and it samples a warped copy of the plane, so the cracks wander instead of running straight
// along the noise's lattice. The finer octaves only texture the lumps.
float lumps(vec2 p) {
    vec2 warp = vec2(value_noise(p * 0.7 + vec2(3.1, 1.7)), value_noise(p * 0.7 + vec2(8.3, 5.9)));
    float sum = 0.5 * (1.0 - abs(2.0 * value_noise(p + 1.6 * warp) - 1.0));
    mat2 turn = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
    p = turn * p * 2.1;
    sum += 0.25 * value_noise(p);
    p = turn * p * 2.1;
    sum += 0.125 * value_noise(p);
    return sum / 0.875;
}

void vertex() {
    world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
}

void fragment() {
    float wobble = 0.7 * value_noise(world_xz * WOBBLE_FREQUENCY)
        + 0.3 * value_noise(world_xz * WOBBLE_FREQUENCY * 3.3 + vec2(17.0, 4.0));
    float inside = UV.x + edge_wobble * (2.0 * wobble - 1.0);
    float edge = max(fwidth(inside), 0.0001);
    float height = lumps(world_xz * LUMP_FREQUENCY);

    // Bump mapping from screen-space derivatives (Mikkelsen): no tangents needed, and the
    // layer has no texture coordinates to build them from.
    vec3 dpdx = dFdx(VERTEX);
    vec3 dpdy = dFdy(VERTEX);
    vec3 r1 = cross(dpdy, NORMAL);
    vec3 r2 = cross(NORMAL, dpdx);
    float det = dot(dpdx, r1);
    vec3 gradient = sign(det) * (dFdx(height) * r1 + dFdy(height) * r2);
    vec3 bumped = normalize(abs(det) * NORMAL - gradient);
    NORMAL = normalize(mix(NORMAL, bumped, ruggedness));

    // Cracks shade the stone rather than black it out.
    ALBEDO = albedo.rgb * mix(1.0, 0.62 + 0.5 * height, ruggedness);
    ALPHA = albedo.a * smoothstep(-edge, edge, inside);
}
