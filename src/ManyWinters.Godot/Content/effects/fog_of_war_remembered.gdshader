shader_type spatial;
// depth_draw_never matters here, not just depth_test_disabled - see
// fog_of_war_screen.gdshader's own doc comment on why leaving this overlay's own near-plane
// depth writable corrupted every later depth read this frame, itself included.
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled, blend_mul;

// The "remembered" tier (explored, not currently in sight) of FogOfWarRenderer.cs's fog of
// war - see fog_of_war_screen.gdshader's own doc comment for why this had to be a *second*
// overlay with its own (multiply) blend mode rather than one more branch in that shader: a
// first attempt read the already-rendered pixel back via hint_screen_texture to desaturate it
// in place, but that automatic screen copy is taken before alpha-tested sprites (every
// person/tree/resource) are drawn, so every sprite in view rendered as a flat grey silhouette
// instead of being muted in place - it was reading whatever was *behind* them.
//
// A multiply blend sidesteps that: it's a real GPU blend against the actual framebuffer at
// the moment this overlay draws (dead last, via RenderPriority - see FogOfWarRenderer.cs),
// which by then already includes every sprite. Output near-white (1,1,1) leaves a pixel
// unchanged under multiply (x * 1 = x); output the muted tint instead to darken/greY it
// toward that tint - no need to know or read the pixel's own original color at all.

uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;
uniform sampler2D exploration_texture : filter_linear, repeat_disable;
uniform vec3 remembered_tint : source_color = vec3(0.72, 0.72, 0.76);
uniform float half_extent_meters = 500.0;

// See fog_of_war_screen.gdshader's own doc comment on cloud_mask for the full reasoning.
uniform sampler2D cloud_mask : filter_linear;

void vertex() {
    // See fog_of_war_screen.gdshader's own doc comment on why Z=0.0 (not 1.0) and why the
    // vertex transform is skipped entirely - same full-screen-quad trick, same reasoning.
    POSITION = vec4(VERTEX.xy, 0.0, 1.0);
}

void fragment() {
    float depth = texture(depth_texture, SCREEN_UV).r;

    vec3 ndc = vec3((SCREEN_UV * 2.0) - 1.0, depth);

    vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    view.xyz /= view.w;
    vec3 world = (INV_VIEW_MATRIX * vec4(view.xyz, 1.0)).xyz;

    vec2 uv = ((world.xz / half_extent_meters) * 0.5) + 0.5;

    // See fog_of_war_screen.gdshader's own doc comment on the flat "ceiling" this guards
    // against - a legitimately distant tree's own depth losing enough precision to
    // reconstruct an implausible world position, multiplying whatever fog-tint state the
    // texture's clamped edge happens to hold into an unrelated, perfectly normal tree.
    bool implausible = uv.x < -0.1 || uv.x > 1.1 || uv.y < -0.1 || uv.y > 1.1;
    // See fog_of_war_screen.gdshader's own doc comment on why this checks color, not
    // just alpha.
    vec4 maskSample = texture(cloud_mask, SCREEN_UV);
    bool cloudPixel = maskSample.a > 0.5 && maskSample.r > 0.9 && maskSample.b > 0.9 && maskSample.g < 0.1;

    // See fog_of_war_screen.gdshader's own doc comment on "fleky" - the empty sky reconstructs
    // to a wildly out-of-range world position, and multiplying by a stray sampled value there
    // is exactly as visible as alpha-blending one. White (1,1,1) is multiply's own no-op.
    // Godot's shading language doesn't allow an early `return` out of fragment() (unlike plain
    // GLSL), so this has to stay one big if/else rather than a guard clause.
    if (depth >= 0.9999 || implausible || cloudPixel) {
        ALBEDO = vec3(1.0);
    } else {
        vec4 state = texture(exploration_texture, uv);
        // G: exact ("sharp") remembered mask, re-thresholded. A: the same mask, blurred - see
        // fog_of_war_screen.gdshader's own doc comment on why gating the blur by the sharp
        // value (rather than using the blur alone) matters: it keeps this tint from bleeding
        // onto anything whose cell is actually still currently Visible. See that shader for
        // the R/B (unexplored) channels.
        float rememberedSharp = step(0.5, state.g);
        float remembered = rememberedSharp * state.a;

        ALBEDO = mix(vec3(1.0), remembered_tint, remembered);
    }
}
