shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

// The hover highlight: an inked rim traced along the *inside* of a hovered entity's silhouette,
// one extra pass over the topmost of its sprite layers (Sprite3D.MaterialOverlay, HoverOutline).
//
// Inside, because there is no room outside: a round canopy touches its square canvas's edges
// (SpritePixelHit), so an outward rim is clipped where the shape is widest, and growing the
// quad would change the world height every extent, collision box and hit test derives from
// (SpriteEntityView). Replacing colour rather than tinting, because Modulate is a multiply and
// this art is mostly near-black ink - a multiplied tint is invisible on it.
//
// One pass for the whole entity: all layers share one quad and UV mapping (SpriteEntityView),
// so the union silhouette is the max alpha across layers. Rimming each layer separately fills a
// person in solid, since body, garment and hairstyle rims pile up inside one small figure.

// Mipmapped and linear, not pinned to level 0: a person is ~40 px tall out of a 256 px texture,
// so point-sampling level 0 undersamples the alpha 4-5x and the rim wobbles on every sub-pixel
// move. The rim width is set in screen pixels below, so the mip blur is about one pixel wide,
// the same scale as the line. (Measured in texels it was many times wider than the line.)
//
// repeat_disable is load-bearing: taps near the canvas edge read outside 0..1, and a repeating
// sampler wraps them to the opposite edge - art on the bottom edge grew dashes floating above
// the entity. Clamped, an outside read returns the (transparent) edge texel.
uniform sampler2D layer_0 : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D layer_1 : source_color, filter_linear_mipmap, repeat_disable, hint_default_transparent;
uniform sampler2D layer_2 : source_color, filter_linear_mipmap, repeat_disable, hint_default_transparent;
uniform sampler2D layer_3 : source_color, filter_linear_mipmap, repeat_disable, hint_default_transparent;
uniform int layer_count = 1;

uniform vec4 rim_color : source_color = vec4(1.0, 0.85, 0.15, 1.0);

// Screen pixels, not texels, so the line keeps its weight at every distance and zoom, like
// the art's own ink; fwidth(UV) converts it into texture space. Three, not two: the
// antialiasing ramps below take about half a pixel each, and at two pixels they are the whole
// line, so its weight follows whatever it is drawn over (fog, occlusion fade). Three leaves a
// solid core.
uniform float rim_pixels = 3.0;
// Cap as a share of the sprite's UV span: a mushroom twenty metres away is a handful of pixels
// across, and an uncapped screen-space rim would paint it solid.
uniform float max_rim_uv = 0.03;
// Below this a texel counts as see-through - SpritePixelHit's threshold, so what looks like the
// edge and what acts like the edge agree.
uniform float opaque_threshold = 0.1;

float silhouette_alpha(vec2 uv) {
    float alpha = texture(layer_0, uv).a;
    if (layer_count > 1) {
        alpha = max(alpha, texture(layer_1, uv).a);
    }

    if (layer_count > 2) {
        alpha = max(alpha, texture(layer_2, uv).a);
    }

    if (layer_count > 3) {
        alpha = max(alpha, texture(layer_3, uv).a);
    }

    return alpha;
}

void vertex() {
    // FixedY billboarding by hand: BaseMaterial3D does it for the first pass
    // (BillboardSprite.Create), but an overlay pass gets the untouched local orientation and
    // would render edge-on. Yaw to face the camera horizontally, keep local up as world up.
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
            vec4(normalize(cross(vec3(0.0, 1.0, 0.0), INV_VIEW_MATRIX[2].xyz)), 0.0),
            vec4(0.0, 1.0, 0.0, 0.0),
            vec4(normalize(cross(INV_VIEW_MATRIX[0].xyz, vec3(0.0, 1.0, 0.0))), 0.0),
            MODEL_MATRIX[3]);
    // No depth bias against the coincident first pass: captures of a hovered tree came back
    // bit-identical with the rim whole, so the tie is settled the same way every frame.
    //
    // The basis above drops the node's scale; re-apply it or scaled sprites (every tree -
    // ResourceNodeView varies width and height separately) get a misaligned rim.
    MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
            vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
            vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
            vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
            vec4(0.0, 0.0, 0.0, 1.0));
}

void fragment() {
    vec2 reach = min(fwidth(UV) * rim_pixels, vec2(max_rim_uv));
    float here = silhouette_alpha(UV);

    // Eight directions, not four: a cross leaves the rim thinner on diagonal edges, which on
    // hand-drawn silhouettes is most of them.
    vec2 offsets[8] = {
        vec2(1.0, 0.0), vec2(-1.0, 0.0), vec2(0.0, 1.0), vec2(0.0, -1.0),
        vec2(0.707, 0.707), vec2(-0.707, 0.707), vec2(0.707, -0.707), vec2(-0.707, -0.707)
    };

    // Three rings averaged into one measure of edge proximity. A single ring is a yes/no that
    // flips as things move (visible as the line changing weight) and can miss the edge entirely
    // on a tight convex corner; averaging radii gives a gradient to antialias against.
    const float rings[3] = { 0.4, 0.7, 1.0 };

    float edge_found = 0.0;
    for (int ring = 0; ring < 3; ring++) {
        float on_this_ring = 0.0;
        for (int i = 0; i < 8; i++) {
            float neighbour = silhouette_alpha(UV + (offsets[i] * reach * rings[ring]));
            on_this_ring = max(on_this_ring, 1.0 - smoothstep(0.0, opaque_threshold, neighbour));
        }

        edge_found += on_this_ring / 3.0;
    }

    // Opaque through the middle of the band, antialiased at both boundaries. A band that is
    // semi-transparent throughout takes its colour from what it is drawn over, so the same line
    // looked heavier over a lit sprite than over a remembered-tier one - hence the plateau. A
    // band with hard boundaries snaps between two and three pixels wide under sub-pixel motion -
    // hence the ramps, kept narrow so the line does not become a glow.
    ALBEDO = rim_color.rgb;
    float inner = smoothstep(0.18, 0.42, edge_found);
    float outer = smoothstep(0.0, opaque_threshold, here);
    ALPHA = inner * outer * rim_color.a;
}
