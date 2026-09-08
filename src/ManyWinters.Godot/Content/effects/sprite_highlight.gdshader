shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled;

// The hover highlight: an inked rim traced along the *inside* of a hovered entity's own
// silhouette, drawn as one extra pass over the topmost of its sprite layers
// (Sprite3D.MaterialOverlay, set up by HoverOutline).
//
// Inside, not outside, and that is the whole trick. An outward rim needs room beyond the drawn
// shape and these sprites do not have it: a round canopy touches most of its square canvas's
// edges (see SpritePixelHit), so anything drawn outside the silhouette would be clipped by the
// quad exactly where the shape is widest. Growing the quad instead would change the world
// height every extent, collision box and hit test is derived from (SpriteEntityView). Drawn
// inward, the rim always has the shape's own pixels to sit on.
//
// It also has to *replace* colour rather than multiply it. A hover tint was tried first and was
// not reliably visible: Modulate is a pure multiply, and this art is largely black crosshatch
// ink - multiplying a near-black pixel by any colour leaves it near-black (see HoverHighlight's
// own history).
//
// One pass for the whole entity, not one per layer. An entity's layers all share the same quad
// and the same UV mapping (SpriteEntityView registers them at one position and one size), so
// the union of their silhouettes is simply the largest alpha any of them has at a UV - which is
// what this traces. Rimming each layer separately was tried first: on a tree it passes, because
// trunk and canopy barely overlap, but on a person the body's, garment's and hairstyle's own
// rims pile up inside one small figure and fill it in solid.

// Read with mipmaps and linear filtering, and deliberately *not* pinned to level 0. Level 0 was
// the first answer here and it is what left the rim wobbling under any movement: a person
// renders about forty pixels tall out of a 256-pixel texture, so point-sampling the sharpest
// level of a high-frequency alpha undersamples it four or five times over, and every sub-pixel
// step of the entity or the camera resamples different texels. The mip chain is already the
// prefiltered answer to the question being asked - what is the average alpha over this screen
// pixel - and because the rim's width is set in screen pixels below, the mip's blur is about one
// pixel wide, the same scale as the line. That is what went wrong the first time this was tried
// with mips: the rim was measured in texels then, so on a minified sprite the blur was many
// times wider than the line it was supposed to trace.
//
// repeat_disable is load-bearing, not tidiness. Every tap below reads UV +/- the rim's reach, so
// near the canvas edge it reads outside 0..1 - and a sampler repeats by default, wrapping those
// reads round to the opposite side of the texture. Art that sits against the bottom edge then
// grows a rim along the *top* edge of the quad: short horizontal dashes floating in the air above
// the entity, well clear of the silhouette they supposedly trace. Clamped instead, an outside
// read returns the edge texel, which is transparent there, and the `here` factor keeps the rim
// off it anyway.
uniform sampler2D layer_0 : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D layer_1 : source_color, filter_linear_mipmap, repeat_disable, hint_default_transparent;
uniform sampler2D layer_2 : source_color, filter_linear_mipmap, repeat_disable, hint_default_transparent;
uniform sampler2D layer_3 : source_color, filter_linear_mipmap, repeat_disable, hint_default_transparent;
uniform int layer_count = 1;

uniform vec4 rim_color : source_color = vec4(1.0, 0.85, 0.15, 1.0);

// In pixels of the screen, not texels of the texture: an entity is drawn once and rendered at
// every distance and zoom the camera has, and an ink line that keeps its weight is what the art
// itself does. fwidth(UV) is how much UV one screen pixel covers right here, which turns that
// into the texture's own space.
//
// Three rather than two, and the reason is the antialiasing below rather than taste. A line
// needs about half a pixel of ramp at each end to stop it snapping between widths as things
// move sub-pixel; at two pixels wide, those ramps are the whole line, leaving no opaque core, so
// its apparent weight follows whatever it happens to be drawn over - and the thing under a
// cursor is routinely dimmed by the fog or ghosted by the occlusion fade while being pointed at.
// Three pixels keeps a solid pixel in the middle that no background can touch.
uniform float rim_pixels = 3.0;
// Never more than this share of the sprite's own UV span, however small it renders: a mushroom
// twenty metres away is a handful of pixels across, and an unclamped screen-space rim would
// paint the whole thing solid instead of outlining it.
uniform float max_rim_uv = 0.03;
// Below this a texel counts as see-through - the same threshold SpritePixelHit uses to decide
// what is clickable, so what looks like the edge and what acts like the edge agree.
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
    // FixedY billboarding, by hand: every sprite in this game is drawn with
    // BillboardModeEnum.FixedY (BillboardSprite.Create), but that is BaseMaterial3D's own vertex
    // work in the *first* pass - a second pass gets the quad in its untouched local orientation
    // and would render edge-on to the camera. This is the documented equivalent: yaw to face the
    // camera's horizontal direction, keep local up pinned to world up.
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
            vec4(normalize(cross(vec3(0.0, 1.0, 0.0), INV_VIEW_MATRIX[2].xyz)), 0.0),
            vec4(0.0, 1.0, 0.0, 0.0),
            vec4(normalize(cross(INV_VIEW_MATRIX[0].xyz, vec3(0.0, 1.0, 0.0))), 0.0),
            MODEL_MATRIX[3]);
    // No depth bias against the sprite this is drawn over, though a coincident second quad is
    // exactly the situation that usually calls for one: two captures of a hovered, unmoving tree
    // came back bit-identical with the rim whole, so the tie between the two passes is in fact
    // settled the same way every frame here and a bias would be guarding against nothing.
    //
    // The node's own scale is not in the basis above, so it has to be re-applied - otherwise a
    // scaled sprite (every tree: ResourceNodeView varies width and height separately) would get
    // a rim at the wrong size and the two passes would not line up.
    MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(
            vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0),
            vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0),
            vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0),
            vec4(0.0, 0.0, 0.0, 1.0));
}

void fragment() {
    vec2 reach = min(fwidth(UV) * rim_pixels, vec2(max_rim_uv));
    float here = silhouette_alpha(UV);

    // Eight directions rather than four: a four-tap cross leaves the rim visibly thinner on
    // diagonal edges, which on hand-drawn silhouettes is most of them.
    vec2 offsets[8] = {
        vec2(1.0, 0.0), vec2(-1.0, 0.0), vec2(0.0, 1.0), vec2(0.0, -1.0),
        vec2(0.707, 0.707), vec2(-0.707, 0.707), vec2(0.707, -0.707), vec2(-0.707, -0.707)
    };

    // Three rings, averaged into one smooth measure of how near the edge is. A single ring only
    // answers whether there is a transparent texel exactly that far away, a yes/no that flips as
    // things move and reads as the line changing weight; averaging several radii turns each flip
    // into a fraction of a step and, just as importantly, gives the value below a gradient to
    // antialias against. It also fills the gap one ring leaves on a tight convex corner, where
    // the whole ring can miss the edge it is meant to be tracing.
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

    // Opaque through the middle of the band, antialiased at its two boundaries. Both halves of
    // that matter, and they pull against each other:
    //
    //   A band that is semi-transparent throughout takes its colour from whatever it is drawn
    //   over, so one and the same line looked heavier over a brightly lit sprite than over one
    //   dimmed into the remembered tier - the fog passing a hovered thing was enough to change
    //   its apparent weight. Hence the plateau: edge_found only has to reach 0.45 to be solid.
    //
    //   A band with hard boundaries snaps between two and three pixels wide as the thing under
    //   the cursor moves sub-pixel, which is the same flicker in its most visible form. Hence a
    //   ramp at each end rather than a step.
    ALBEDO = rim_color.rgb;
    // Narrow ramps at both ends of the band, with the plateau between them left solid: enough
    // to stop the line snapping by a pixel, not enough to turn it into a glow.
    float inner = smoothstep(0.18, 0.42, edge_found);
    float outer = smoothstep(0.0, opaque_threshold, here);
    ALPHA = inner * outer * rim_color.a;
}
