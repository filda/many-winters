shader_type spatial;
// depth_draw_never, not just depth_test_disabled: vertex() writes Z=0.0 over the whole screen,
// and depth_test_disabled only skips the test, not the write. Left writable, this overlay
// replaced the depth buffer with the near plane every frame and broke every later depth read
// (fog_of_war_remembered.gdshader included), tearing the tops off tall billboard sprites.
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;

// The "unknown" tier (never explored) of FogOfWarRenderer.cs's fog of war, as a full-screen
// overlay quad. Each screen pixel's world position is reconstructed from the depth buffer
// (INV_PROJECTION_MATRIX, then INV_VIEW_MATRIX) and looked up in the exploration texture, so
// the boundary is as smooth as the texture's bilinear filter, and only the point on screen is
// tested - the camera sitting outside the explored area changes nothing. Mesh geometry would
// be quantized to ExplorationState's grid; volumetric fog would black out the camp as soon as
// the camera left it.
//
// The "remembered" tier is a second overlay (fog_of_war_remembered.gdshader) with a multiply
// blend rather than a branch here: hint_screen_texture's screen copy is taken before the
// alpha-tested sprites (AlphaCutMode.OpaquePrepass) are drawn, so reading the pixel back to
// desaturate it turned every sprite into a flat grey silhouette. A GPU blend against the
// framebuffer at draw time (dead last, by RenderPriority) already includes them.

uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;
uniform sampler2D exploration_texture : filter_linear, repeat_disable;
uniform vec3 fog_albedo : source_color = vec3(0.70, 0.73, 0.78);
uniform float half_extent_meters = 500.0;
// How far the paper mottling pushes the albedo up or down (a fraction of it). Two octaves:
// broad blotches at mottle_scale metres, a finer grain at ~6x that frequency. Kept broad and
// faint - at 12m and 0.10 the blotches read as sand dunes, not paper.
uniform float mottle_strength = 0.07;
uniform float mottle_scale = 22.0;
// Metres from each map position to the nearest cell the band has ever explored (R, float -
// FogOfWarRenderer._distanceTexture). The parchment holds for fade_start_meters past the
// explored boundary, then darkens into far_color by fade_end_meters. Distance from the
// explored area, not from the map's edge - the latter lit the whole middle of the map.
// far_color is SkyPalette.FogFar (the horizon colour, mostly desaturated), set by
// FogOfWarRenderer.cs so the sheet dissolves into the sky without a seam.
uniform sampler2D distance_texture : filter_linear, repeat_disable;
uniform vec3 far_color : source_color = vec3(0.3, 0.3, 0.3);
uniform float fade_start_meters = 10.0;
uniform float fade_end_meters = 70.0;

// Clouds (CloudScatter.cs) are sky, not ground, so fog-of-war leaves them alone. This is a
// real render (CloudFogMask.cs: a second camera mirroring the main one) sampled by screen
// position; inferring "cloud" from this pixel's reconstructed world position failed along
// grazing view rays. The mask camera also renders ordinary geometry so a hill occludes a cloud
// proxy, and everything in it is equally opaque - so the flag is a colour, not alpha: the
// proxies are Sprite3Ds with CloudScatter.MaskFlagModulate (12, 0, 12) as Modulate.
uniform sampler2D cloud_mask : filter_linear;

// Cheap hash-based value noise. The unknown tier is the blank part of an old map sheet
// (docs/ZemanConceptArt.png, "Unknown" panel), and a flat fill reads as fog or snow whatever
// its colour. Sampled on the reconstructed WORLD position, not SCREEN_UV, so the blotches stay
// pinned to the ground as the camera pans.
float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float value_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), u.x),
               mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), u.x), u.y);
}

void vertex() {
    // Straight to clip space, skipping MODEL_VIEW_PROJECTION: the quad's local coordinates
    // span at least [-1, 1] (FogOfWarRenderer.OverlayQuadSize), so it covers the screen
    // whatever the transform, FOV or projection (perspective/orthogonal, T key in Main).
    // Z=0.0, not 1.0: right on the far clip boundary, rounding dropped whole triangles and
    // left part of the screen uncovered. Depth is irrelevant here anyway.
    POSITION = vec4(VERTEX.xy, 0.0, 1.0);
}

void fragment() {
    float depth = texture(depth_texture, SCREEN_UV).r;
    ALBEDO = fog_albedo;

    vec3 ndc = vec3((SCREEN_UV * 2.0) - 1.0, depth);

    vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    view.xyz /= view.w;
    vec3 world = (INV_VIEW_MATRIX * vec4(view.xyz, 1.0)).xyz;

    vec2 uv = ((world.xz / half_extent_meters) * 0.5) + 0.5;

    // Depth precision near the far plane is poor, and the inverse-depth math is highly
    // non-linear there, so distant geometry can reconstruct to a wildly wrong world position -
    // seen as a flat horizontal "ceiling" cut through normal trees. A uv well outside [0, 1]
    // is that signal: skip the fog rather than trust it. A distant tree unfogged one tick early
    // beats a nearby one with a chunk cut out.
    bool implausible = uv.x < -0.1 || uv.x > 1.1 || uv.y < -0.1 || uv.y > 1.1;
    // Matches CloudScatter.MaskFlagModulate (12, 0, 12). Only green is tested: any texel times
    // 0 is exactly 0, while the >1 red/blue still vary hatch-to-highlight after tonemapping and
    // testing them rejected the proxy's own hatch pixels. No real material in the game's muted
    // palette has exactly 0 green.
    vec4 maskSample = texture(cloud_mask, SCREEN_UV);
    bool cloudPixel = maskSample.a > 0.5 && maskSample.g < 0.02;

    // Empty sky clears to the far plane exactly; there the divide by w sits on a singularity
    // and the reconstructed position is garbage (dark speckles across the sky). Nothing to fog
    // there anyway. Godot's shading language has no early return from fragment(), hence one
    // if/else rather than a guard clause.
    if (depth >= 0.9999 || implausible || cloudPixel) {
        ALPHA = 0.0;
    } else {
        vec4 state = texture(exploration_texture, uv);
        // R: sharp unexplored mask, re-thresholded to 0/1 after bilinear sampling. B: the same
        // mask blurred (FogOfWarRenderer.RebuildExplorationTexture). Multiplying by the sharp
        // value confines the softness to unexplored ground, so an Explored cell - which always
        // has a real tree/resource on it - never gets a ghost of fog for sitting near the
        // boundary. G/A (remembered) are read by fog_of_war_remembered.gdshader.
        float unexploredSharp = step(0.5, state.r);
        ALPHA = unexploredSharp * state.b;

        float blotch = value_noise(world.xz / mottle_scale) - 0.5;
        float grain = value_noise(world.xz / mottle_scale * 6.0) - 0.5;
        vec3 paper = fog_albedo * (1.0 + mottle_strength * (blotch + 0.4 * grain));

        // The blotch noise nudges the distance so the darkening creeps in unevenly, like a
        // stain, not a clean ring around the explored area.
        float meters = texture(distance_texture, uv).r + blotch * 0.15 * fade_end_meters;
        float far = smoothstep(fade_start_meters, fade_end_meters, meters);
        ALBEDO = mix(paper, far_color, far * far);
    }
}
