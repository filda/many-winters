shader_type spatial;
// depth_draw_never, not just depth_test_disabled - see fog_of_war_screen.gdshader: a writable
// near-plane depth from this overlay corrupts every later depth read, its own included.
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, fog_disabled, shadows_disabled, blend_mul;

// The "remembered" tier (explored, not currently in sight) of FogOfWarRenderer.cs's fog of
// war. A separate overlay with a multiply blend, because reading the rendered pixel back via
// hint_screen_texture misses the alpha-tested sprites (see fog_of_war_screen.gdshader). Under
// multiply, white (1,1,1) leaves a pixel unchanged and the tint darkens it toward itself - no
// need to know the pixel's own colour.

uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;
uniform sampler2D exploration_texture : filter_linear, repeat_disable;
uniform vec3 remembered_tint : source_color = vec3(0.80, 0.74, 0.64);
uniform float half_extent_meters = 500.0;

// See fog_of_war_screen.gdshader's cloud_mask.
uniform sampler2D cloud_mask : filter_linear;

void vertex() {
    // Same full-screen-quad trick as fog_of_war_screen.gdshader, Z=0.0 for the same reason.
    POSITION = vec4(VERTEX.xy, 0.0, 1.0);
}

void fragment() {
    float depth = texture(depth_texture, SCREEN_UV).r;

    vec3 ndc = vec3((SCREEN_UV * 2.0) - 1.0, depth);

    vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
    view.xyz /= view.w;
    vec3 world = (INV_VIEW_MATRIX * vec4(view.xyz, 1.0)).xyz;

    vec2 uv = ((world.xz / half_extent_meters) * 0.5) + 0.5;

    // Same three guards as fog_of_war_screen.gdshader (far-plane sky, implausible distant
    // reconstruction, cloud pixel by green channel). White is multiply's no-op. No early
    // return from fragment() in Godot's shading language, hence the if/else.
    bool implausible = uv.x < -0.1 || uv.x > 1.1 || uv.y < -0.1 || uv.y > 1.1;
    vec4 maskSample = texture(cloud_mask, SCREEN_UV);
    bool cloudPixel = maskSample.a > 0.5 && maskSample.g < 0.02;

    if (depth >= 0.9999 || implausible || cloudPixel) {
        ALBEDO = vec3(1.0);
    } else {
        vec4 state = texture(exploration_texture, uv);
        // G: sharp remembered mask, re-thresholded. A: the same mask blurred. Gating the blur by
        // the sharp value keeps the tint off cells that are still Visible; R/B (unexplored) are
        // read by fog_of_war_screen.gdshader.
        float rememberedSharp = step(0.5, state.g);
        float remembered = rememberedSharp * state.a;

        ALBEDO = mix(vec3(1.0), remembered_tint, remembered);
    }
}
