shader_type sky;

// A painted sky, not a photographic one. Everything else in this game is a woodcut - the
// clouds in front of this are hand-drawn sprites (CloudScatter), the ground is a hatched
// texture - so what belongs behind them is flat-ish blue with just enough life in it to stop
// reading as a wall: deeper overhead, paler at the skyline, with a few faint high wisps. No
// sun disk, no atmospheric scattering model; both would state a light source and a time of day
// that nothing else in the art commits to.
//
// The colours are set from SkySetup.cs rather than left at these defaults, because the
// fog-of-war sheet has to dissolve into the same horizon colour this draws (see
// FogOfWarRenderer) - one of them holding its own copy is how the two drift apart into a
// visible seam along the skyline.

uniform vec3 zenith_color : source_color = vec3(0.26, 0.45, 0.70);
uniform vec3 horizon_color : source_color = vec3(0.64, 0.76, 0.85);
uniform vec3 streak_color : source_color = vec3(0.90, 0.94, 0.98);
// Below the skyline the terrain covers nearly everything, but the camera can tilt under its
// edge; this is what it finds there.
uniform vec3 below_horizon_color : source_color = vec3(0.32, 0.29, 0.26);

// How quickly the pale skyline gives way to blue. Well under 1.0, because of where this game's
// camera points: at its usual tilt only the first ten or twenty degrees above the horizon are
// ever in frame, so a gradient that saves its blue for high up would show the player nothing
// but haze - the entire sky they see is the bottom of it. Tuned by looking at that band, not
// at the whole dome.
uniform float gradient_bias = 0.42;

uniform float streak_strength = 0.55;
// How far the wisps are drawn out sideways. High enough that they read as streaks rather than
// as blotches, low enough that they don't become perfectly straight bands.
uniform float streak_stretch = 8.0;
uniform float streak_scale = 1.6;
// Only the top of the noise range becomes a wisp, so the sky is mostly open with occasional
// streaks across it - not a uniformly cloudy ceiling.
uniform float streak_threshold = 0.52;

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

// The same cheap value noise the fog-of-war sheet's paper mottling uses - deliberately, since
// both are "hand-made surface, not a clean fill" and a second noise implementation would be a
// second thing to tune.
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

    // The wisps sit on a flat layer overhead rather than on the dome itself: dividing the
    // ray's horizontal direction by its own height is the projection a real cloud layer has,
    // so streaks bunch up towards the skyline instead of marching across the sky at one size.
    // The max() keeps that division away from the singularity at the horizon, where the fade
    // below has already taken the streaks out anyway.
    vec2 layer = EYEDIR.xz / max(up, 0.06);
    float wisps = fbm(vec2(layer.x / streak_stretch, layer.y) * streak_scale);
    float streak = smoothstep(streak_threshold, streak_threshold + 0.35, wisps);
    // Faded out at both ends: nothing at the skyline, where the fog-of-war sheet dissolves
    // into flat horizon colour and any texture would show the seam, and nothing straight
    // overhead, where the flat-layer projection degenerates.
    streak *= smoothstep(0.01, 0.14, up) * (1.0 - smoothstep(0.50, 0.92, up));
    color = mix(color, streak_color, streak * streak_strength);

    COLOR = mix(color, below_horizon_color, smoothstep(0.0, 0.25, -EYEDIR.y));
}
