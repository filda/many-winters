shader_type spatial;
// depth_draw_never matters here, not just depth_test_disabled: this quad's own vertex shader
// writes Z=0.0 (right at the near plane - see vertex() below) across the *entire* screen, and
// depth_test_disabled only skips testing *against* the existing depth buffer - it doesn't
// stop this overlay from *writing* to it. Left writable, this overlay silently overwrote the
// real depth buffer with that near-plane value every frame - anything sampling depth
// afterward (fog_of_war_remembered.gdshader's own depth_texture read included) saw "the
// entire screen is one flat surface right at the camera" instead of the real scene, which is
// what tore the tops off tall billboard sprites (tree canopies) in exactly the height range
// where the two disagreed.
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled;

// A full-screen overlay quad (see FogOfWarRenderer.cs) instead of either real 3D geometry or
// real volumetric fog - both of those were tried and reverted (see that class's own history):
// mesh geometry is fundamentally quantized to ExplorationState's coarse grid no matter how
// much its surface gets dressed up, and physically-based volumetric fog's opacity depends on
// how much fog a view ray actually crosses, not on whether its *destination* is explored,
// which blacks out the small explored camp itself the moment the camera drifts outside it.
// This sidesteps both: every SCREEN pixel's own world position is reconstructed from the
// depth buffer (the standard Godot recipe - INV_PROJECTION_MATRIX then INV_VIEW_MATRIX, both
// built into spatial shaders already, no manual matrix uniforms needed) and looked up in
// FogOfWarRenderer's own exploration texture directly - a per-pixel test, not a per-vertex or
// per-ray-step one, so the boundary is exactly as smooth as the texture's own bilinear
// filtering, and there is no "camera is outside the explored area" failure mode at all since
// nothing is integrated along a ray - only the destination point (what's actually on screen)
// is tested.
//
// Handles only the "unknown" tier (never explored) - a full opaque replace, alpha-blended.
// "Remembered" is a second overlay (fog_of_war_remembered.gdshader) using a *multiply* blend
// instead: an earlier version of this shader tried to desaturate the real pixel here by
// reading it back via hint_screen_texture, but that automatic screen copy is taken before
// alpha-tested sprites (every person/tree/resource - AlphaCutMode.OpaquePrepass) are drawn,
// so it only ever saw whatever was *behind* them - every sprite in view rendered as a flat
// grey silhouette instead of being desaturated in place. A multiply blend has no such gap:
// it's a real GPU blend against the actual framebuffer at draw time, which - since this
// overlay draws dead last (RenderPriority) - already includes everything, sprites included.

uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;
uniform sampler2D exploration_texture : filter_linear, repeat_disable;
uniform vec3 fog_albedo : source_color = vec3(0.72, 0.66, 0.58);
uniform float half_extent_meters = 500.0;
// Engraved hatching over the parchment, in world space so it stays pinned to the ground.
// A flat fill - any hue - read as sand or mud; what makes the reference's paper read as
// paper is that it's visibly *drawn*. Tone comes from line density, the same rule every
// sprite in this game follows (see art/generate_sprites.py): lines are thin and sparse
// right past the explored boundary and thicken with distance until they cross-hatch into
// darkness. hatch_spacing is the gap between lines in metres.
uniform vec3 hatch_color : source_color = vec3(0.26, 0.21, 0.16);
uniform float hatch_spacing = 1.3;
uniform float hatch_min_width = 0.06;
uniform float hatch_max_width = 0.35;
// How far the paper mottling below pushes the albedo up or down (a fraction of it). Two
// octaves: broad blotches at mottle_scale metres, a finer grain at ~6x that frequency.
// Kept broad and faint - at 12m and 0.10 the blotches read as sand dunes, not paper.
uniform float mottle_strength = 0.07;
uniform float mottle_scale = 22.0;
// Metres from each map position to the nearest cell the group has ever explored (R, float
// - see FogOfWarRenderer's own _distanceTexture). The parchment holds for fade_start_meters
// past the explored boundary, then darkens into far_color by fade_end_meters: concentric
// rings of party, visible ground, parchment, then darkness. Distance from the explored
// area is what makes this fog-of-war and not a map vignette - an earlier version faded
// toward the map's *edge*, which lit the whole middle of the map regardless of whether
// anyone had been there. far_color is passed in from FogOfWarRenderer.cs as the viewport's
// own clear colour so the sheet dissolves into the empty sky with no visible seam.
uniform sampler2D distance_texture : filter_linear, repeat_disable;
uniform vec3 far_color : source_color = vec3(0.3, 0.3, 0.3);
uniform float fade_start_meters = 10.0;
uniform float fade_end_meters = 70.0;

// CloudScatter.cs's own sky decoration, exempted from fog-of-war - a cloud is atmosphere,
// not ground content, so fog-of-war (which only ever has an opinion about the *ground*:
// has this XZ been explored?) has nothing meaningful to say about one. This is a real
// render (CloudFogMask.cs - a second camera, mirroring the main one every frame) sampled
// directly by screen position - not an inference from this pixel's own reconstructed
// world position. Several approaches were tried and rejected before this one - see
// CloudFogMask's own doc comment for the full history (a world.y height test, two
// reconstruction-based projection tests, then a cloud-only render with no occluders,
// each fixing the last failure but introducing a smaller one).
//
// A flat, saturated magenta - no natural material in this game's own muted woodcut
// palette produces anything close - rather than plain alpha: this mask's own camera
// also renders ordinary scene geometry (terrain, trees, people) so a hill in front of a
// cloud proxy actually occludes it, and everything ordinary is just as opaque as a
// cloud proxy is. Color, not alpha, is what actually distinguishes "a cloud was the
// frontmost surface here" from "something else was" - see cloud_mask_proxy.gdshader's
// own doc comment.
uniform sampler2D cloud_mask : filter_linear;

// The vertex shader's usual VERTEX * MODEL_VIEW_PROJECTION path is skipped entirely - writing
// directly to clip space (this quad's own local coordinates, assumed to already span at
// least [-1, 1] on both axes - see the QuadMesh size FogOfWarRenderer.cs creates it with)
// makes it cover the full screen regardless of the quad's actual world transform, camera FOV,
// or projection type (perspective/orthogonal - the T key toggle in FreeCameraRig).
// Cheap hash-based value noise. The unknown tier is meant to read as the blank part of an
// old map sheet (docs/ZemanConceptArt.png, "Unknown" panel), and a perfectly flat fill
// reads as fog or snow no matter what colour it is - real paper has uneven, blotchy tone.
// Sampled on the reconstructed WORLD position, not SCREEN_UV, so the blotches stay pinned
// to the ground as the camera pans instead of swimming across it like a screen overlay.
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

// One family of parallel hand-ruled hatch lines. `across` runs perpendicular to the lines
// (in line-spacing units, so consecutive integers are consecutive lines), `along` runs
// with them (metres). Each line gets its own lateral wobble and its own pen lifts, keyed by
// its integer index, so they don't bend in lockstep - the same "per-line personality" the
// sprite generator's hatch uses. Returns ink coverage in [0, 1], anti-aliased, and
// collapsing to its own average tone once the lines get denser than screen pixels (a raw
// step() there just shimmers with moire).
float hatch_ink(float across, float along, float width) {
    float line_index = floor(across);
    // Per-line personality, all keyed by the line's own index: where it sits between its
    // neighbours, how thick the nib was, how it bows, and how long each stroke runs before
    // the pen lifts. Evenly spaced identical lines read as a ploughed field, not a drawing.
    float lateral = (hash21(vec2(line_index, 1.7)) - 0.5) * 0.8;
    float thickness = 0.5 + hash21(vec2(line_index, 9.3)) * 1.0;
    float curve_freq = 0.12 + hash21(vec2(line_index, 4.1)) * 0.25;
    float curve_amp = 0.08 + hash21(vec2(line_index, 6.7)) * 0.16;
    float curve = sin(along * curve_freq + hash21(vec2(line_index, 2.9)) * 6.2832) * curve_amp;
    float stroke_len = 1.5 + hash21(vec2(line_index, 8.2)) * 2.5;
    float segment = floor(along / stroke_len);
    float pen_down = step(0.32, hash21(vec2(line_index * 13.0 + segment, segment * 0.37 + line_index)));

    float w = width * thickness;
    float pos = fract(across - lateral - curve);
    float aa = fwidth(across);
    float line = (1.0 - smoothstep(w - aa, w + aa, pos)) * pen_down;
    float too_dense = smoothstep(0.35, 0.9, aa);
    return mix(line, width * 0.6, too_dense);
}

void vertex() {
    // Z=1.0 (the far clip boundary exactly) risked landing on the wrong side of hardware
    // clipping for some of the quad's vertices due to floating-point rounding right at that
    // edge - each affected triangle then vanished independently, which is exactly the
    // "only part of the screen is covered" artifact this caused. 0.0 sits safely inside the
    // valid range instead - depth doesn't matter here regardless (depth_test_disabled above).
    POSITION = vec4(VERTEX.xy, 0.0, 1.0);
}

void fragment() {
    float depth = texture(depth_texture, SCREEN_UV).r;
    ALBEDO = fog_albedo;

    // The empty sky (nothing drawn there at all) clears to the far plane exactly - dividing
    // by INV_PROJECTION_MATRIX's own w there lands right on/past a singularity, reconstructing
    // some wildly out-of-range world.xz. Sampling the exploration texture at that point used
    // to read as small dark speckles scattered across the sky ("fleky") - repeat_disable
    // above already clamps the *sample* to the texture's edge instead of wrapping/repeating,
    // but skipping the fog effect outright here is both cheaper and semantically correct:
    // there's no terrain out there to hide in the first place. Godot's shading language
    // doesn't allow an early `return` out of fragment() (unlike plain GLSL), so this has to
    // stay one big if/else rather than a guard clause.
    vec3 ndc = vec3((SCREEN_UV * 2.0) - 1.0, depth);

    vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    view.xyz /= view.w;
    vec3 world = (INV_VIEW_MATRIX * vec4(view.xyz, 1.0)).xyz;

    vec2 uv = ((world.xz / half_extent_meters) * 0.5) + 0.5;

    // Real geometry legitimately farther out than the map's own half-extent (background
    // decoration is scattered across the whole terrain, and the camera can zoom/look well
    // past it) still needs *some* depth value, and depth buffer precision doesn't stay
    // perfect all the way to the far plane - a small quantization error there, run back
    // through the (highly non-linear near the far end) inverse-depth math, becomes a huge
    // error in the reconstructed world position. That showed up as a dead-flat horizontal
    // "ceiling" cutting through unrelated, perfectly normal trees at a consistent screen
    // height - not the boundary's own shape at all, since a bad reconstruction doesn't care
    // what's actually at that pixel. uv landing well outside the texture's own [0, 1] is the
    // signal something reconstructed implausibly far: skip the fog effect rather than trust
    // it, the same as the sky guard below - a real but very distant tree staying unfogged one
    // tick early beats a nearby one getting a bogus chunk cut out of it.
    bool implausible = uv.x < -0.1 || uv.x > 1.1 || uv.y < -0.1 || uv.y > 1.1;
    // Matches cloud_mask_proxy.gdshader's own flat vec3(1.0, 0.0, 1.0) output - loose
    // thresholds on each channel (rather than an exact equality no float comparison
    // should ever rely on) still comfortably reject every real material this game's own
    // muted palette can produce, which has nothing anywhere near this saturated.
    // Only the green channel is checked - CloudScatter.cs's own proxy Modulate has a
    // green component of exactly 0, and any color times 0 is exactly 0 regardless of
    // the source texture's own value there, unlike the red/blue channels (whose >1
    // multiplied values still visibly differ hatch-to-highlight after the render
    // pipeline's own tonemapping/clamping - checking those too rejected the proxy's own
    // darker hatch-line pixels, exactly the same "fog painted over most of the cloud,
    // a thin sliver of real texture left" symptom as the very first version of this
    // mask). No real material in this game's own muted palette lands on exactly 0.0
    // green, so this alone is still a reliable signal.
    vec4 maskSample = texture(cloud_mask, SCREEN_UV);
    bool cloudPixel = maskSample.a > 0.5 && maskSample.g < 0.02;

    // The empty sky (nothing drawn there at all) clears to the far plane exactly - dividing
    // by INV_PROJECTION_MATRIX's own w there lands right on/past a singularity, reconstructing
    // some wildly out-of-range world.xz. Sampling the exploration texture at that point used
    // to read as small dark speckles scattered across the sky ("fleky") - repeat_disable
    // above already clamps the *sample* to the texture's edge instead of wrapping/repeating,
    // but skipping the fog effect outright here is both cheaper and semantically correct:
    // there's no terrain out there to hide in the first place. Godot's shading language
    // doesn't allow an early `return` out of fragment() (unlike plain GLSL), so this has to
    // stay one big if/else rather than a guard clause.
    if (depth >= 0.9999 || implausible || cloudPixel) {
        ALPHA = 0.0;
    } else {
        vec4 state = texture(exploration_texture, uv);
        // R: exact ("sharp") unexplored mask - re-thresholded back to a hard 0/1 despite
        // bilinear sampling blending it slightly right at the boundary. B: the same mask,
        // blurred (see FogOfWarRenderer.RebuildExplorationTexture) - multiplying by the sharp
        // value confines that blur's softness to genuinely unexplored territory only, so nothing
        // whose cell is actually already Explored (which - unlike unexplored ground - always
        // has a real, already-instantiated tree/resource behind it) ever gets a ghost of fog
        // blended over it just for sitting near the boundary. See fog_of_war_remembered.gdshader
        // for the G/A (remembered) channels.
        float unexploredSharp = step(0.5, state.r);
        ALPHA = unexploredSharp * state.b;

        float blotch = value_noise(world.xz / mottle_scale) - 0.5;
        float grain = value_noise(world.xz / mottle_scale * 6.0) - 0.5;
        vec3 paper = fog_albedo * (1.0 + mottle_strength * (blotch + 0.4 * grain));

        // The blotch noise nudges the distance so the darkening creeps in unevenly, like a
        // stain spreading across the sheet, not a clean ring around the explored area.
        float meters = texture(distance_texture, uv).r + blotch * 0.15 * fade_end_meters;
        float far = smoothstep(fade_start_meters, fade_end_meters, meters);

        // Diagonal hatching whose line width grows with distance from the explored area;
        // a second, perpendicular family joins in past the halfway point so the far zone
        // cross-hatches into darkness rather than just getting thicker stripes.
        vec2 diag = vec2(world.x + world.z, world.x - world.z) * 0.7071;
        float width = mix(hatch_min_width, hatch_max_width, far);
        float ink = hatch_ink(diag.x / hatch_spacing, diag.y, width);
        float cross_width = hatch_max_width * smoothstep(0.45, 1.0, far);
        ink = max(ink, hatch_ink(diag.y / hatch_spacing, diag.x, cross_width));
        vec3 drawn = mix(paper, hatch_color, ink);
        ALBEDO = mix(drawn, far_color, far * far);
    }
}
