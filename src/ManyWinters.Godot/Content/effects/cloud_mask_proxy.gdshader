shader_type spatial;
// CloudFogMask.cs's own mask-only stand-in for a CloudScatter sprite - never seen by the
// main camera (FreeCameraRig's own CullMask excludes CloudFogMask.CloudLayerBit, the only
// layer this is on), only by the mask camera. That camera's own cull mask *also* includes
// ordinary scene geometry (terrain, trees, people - CloudScatter.cs's own doc comment on
// why: a cloud actually hidden behind a hill from the main camera's view still needs to
// read as hidden here too, or the ground/hill in front of it wrongly inherited the
// cloud's own fog-of-war exemption - a visible green sliver of real, still-unexplored
// ground breaking through right where a distant cloud's low edge met the horizon).
// Ordinary geometry keeps its own real material in this same render, so telling "was a
// cloud the frontmost surface here" apart from "was it something else" needs a signal
// besides plain opacity - both are equally opaque. A flat, saturated flag color no
// natural material in this game's own muted palette could ever produce (checked against
// in fog_of_war_screen.gdshader's own cloud_pixel test) is that signal; the real alpha
// cutout shape (this sprite shares its texture with the real one) is what keeps that flag
// confined to the cloud's own silhouette instead of its whole square canvas.
render_mode unshaded, cull_disabled, depth_draw_opaque;

uniform sampler2D cloud_texture : filter_linear;

// The real cloud sprite's Billboard=FixedY (BillboardSprite.Create) is a property of
// Sprite3D's own *auto-generated* material, not of the node itself - Godot bakes that
// facing rotation into the vertex transform only for the material it builds internally.
// Overriding the material (as this proxy does, for its own flag-color ALBEDO) throws that
// away entirely, leaving the raw, never-rotating node transform - so as the camera orbited,
// the real sprite kept facing it correctly while this proxy stayed fixed in place, each
// showing a different slice of the same texture's silhouette (confirmed - the user's own
// diagnosis, from two screenshots taken while panning). Replicated by hand here: face the
// camera by rotating only around world Y, the same as FixedY does.
void vertex() {
    vec3 objectWorldPos = MODEL_MATRIX[3].xyz;
    vec3 cameraWorldPos = INV_VIEW_MATRIX[3].xyz;
    vec3 toCamera = cameraWorldPos - objectWorldPos;
    toCamera.y = 0.0;
    float toCameraLength = length(toCamera);
    // Directly above/below the object - no well-defined facing direction; the mask
    // exemption just goes stale for that one texel until the camera moves again, which
    // beats a divide-by-zero.
    if (toCameraLength > 0.0001) {
        toCamera /= toCameraLength;
    } else {
        toCamera = vec3(0.0, 0.0, 1.0);
    }

    vec3 right = normalize(cross(vec3(0.0, 1.0, 0.0), toCamera));
    vec3 up = vec3(0.0, 1.0, 0.0);

    // MODEL_MATRIX's own basis vectors already carry this sprite's scale (PixelSize *
    // texture size, set by BillboardSprite.Apply) - preserved here so the billboard
    // ends up the right *size*, not just facing the right way.
    vec3 modelScale = vec3(length(MODEL_MATRIX[0].xyz), length(MODEL_MATRIX[1].xyz), length(MODEL_MATRIX[2].xyz));
    mat4 billboardModel = mat4(
        vec4(right * modelScale.x, 0.0),
        vec4(up * modelScale.y, 0.0),
        vec4(toCamera * modelScale.z, 0.0),
        vec4(objectWorldPos, 1.0)
    );

    MODELVIEW_MATRIX = VIEW_MATRIX * billboardModel;
}

void fragment() {
    vec4 tex = texture(cloud_texture, UV);
    ALBEDO = vec3(1.0, 0.0, 1.0);
    ALPHA = tex.a;
    ALPHA_SCISSOR_THRESHOLD = 0.5;
}
