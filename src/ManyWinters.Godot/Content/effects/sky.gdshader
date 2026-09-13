shader_type sky;

// A painted sky, to sit behind woodcut clouds (CloudScatter) and hatched ground: flat-ish blue,
// deeper overhead, paler at the skyline, a few faint high wisps. No sun disk or scattering
// model - both would commit to a light source and time of day nothing else in the art does.
//
// Colours are set from SkySetup.cs (SkyPalette), not these defaults: the fog-of-war sheet fades
// to the same horizon colour (FogOfWarRenderer), and a second copy would drift into a visible
// seam along the skyline.

uniform vec3 zenith_color : source_color = vec3(0.26, 0.45, 0.70);
uniform vec3 horizon_color : source_color = vec3(0.64, 0.76, 0.85);
uniform vec3 streak_color : source_color = vec3(0.90, 0.94, 0.98);
// What the camera finds when it tilts under the terrain's edge.
uniform vec3 below_horizon_color : source_color = vec3(0.32, 0.29, 0.26);

// How quickly the pale skyline gives way to blue. Well under 1.0: at the usual camera tilt only
// the first ten or twenty degrees above the horizon are in frame, so a gradient that saves its
// blue for high up shows the player nothing but haze. Tuned by looking at that band.
uniform float gradient_bias = 0.42;

uniform float streak_strength = 0.55;
// Sideways stretch of the wisps: high enough to read as streaks, not blotches; low enough not to
// become straight bands.
uniform float streak_stretch = 8.0;
uniform float streak_scale = 1.6;
// Only the top of the noise range becomes a wisp, so the sky is mostly open.
uniform float streak_threshold = 0.52;

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

// The same value noise as fog_of_war_screen.gdshader's paper mottling, so there is one noise to
// tune.
float value_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), u.x),
               mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
    float total = 0.0;
    float amplitude = 0.5;
    for (int octave = 0; octave < 4; octave++) {
        total += value_noise(p) * amplitude;
        p *= 2.0;
        amplitude *= 0.5;
    }

    return total;
}

void sky() {
    float up = clamp(EYEDIR.y, 0.0, 1.0);
    vec3 color = mix(horizon_color, zenith_color, pow(up, gradient_bias));

    // The wisps lie on a flat layer overhead, not on the dome: dividing the ray's horizontal
    // direction by its height is a cloud layer's projection, so streaks bunch up toward the
    // skyline. The max() keeps clear of the singularity at the horizon, where the fade below
    // has removed the streaks anyway.
    vec2 layer = EYEDIR.xz / max(up, 0.06);
    float wisps = fbm(vec2(layer.x / streak_stretch, layer.y) * streak_scale);
    float streak = smoothstep(streak_threshold, streak_threshold + 0.35, wisps);
    // Faded out at the skyline, where the fog-of-war sheet dissolves into flat horizon colour
    // and any texture shows the seam, and straight overhead, where the flat-layer projection
    // degenerates.
    streak *= smoothstep(0.01, 0.14, up) * (1.0 - smoothstep(0.50, 0.92, up));
    color = mix(color, streak_color, streak * streak_strength);

    COLOR = mix(color, below_horizon_color, smoothstep(0.0, 0.25, -EYEDIR.y));
}
