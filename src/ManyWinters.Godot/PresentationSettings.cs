namespace ManyWinters.Godot;

// The tunable numbers the presentation layer draws and picks with - screen-space sizes, fade
// alphas, camera zoom bounds, a debug panel's font size. The Godot-side counterpart of
// SimulationRules: configuration, not state, and none of it is a rule of the world (nothing in
// ManyWinters.Core ever sees it) - only of how this client shows the world and reads the mouse
// over it. Default is what the shipped game uses; TerrainSandbox starts from it and overrides
// only the zoom its full-scale terrain needs. Same setter rule as SimulationRules: only what
// something actually overrides has an `init` setter, the rest stay get-only until needed.
public sealed record PresentationSettings
{
    public static PresentationSettings Default { get; } = new();

    // Godot's UI default (16) reads oversized for a dense debug/dev panel crammed with
    // labels and a growing list of contextual buttons.
    public int InspectorFontSize { get; } = 13;

    // Extra margin (in meters) added on top of each candidate's own on-screen half-width -
    // roughly the selected person's own half-width, so something has to clear the target's
    // own silhouette, not just its exact center point, to not count as blocking it - and how
    // transparent something fades to once it does.
    public float OcclusionMargin { get; } = 0.3f;

    public float OcclusionFadedAlpha { get; } = 0.25f;

    // Slack (in meters) added past the target's own distance before something stops counting
    // as "in the way" - without it, a tree that was fading to reveal a whole crowd (the
    // no-selection fallback target sits farther out) can snap solid the instant you select
    // one specific person who happens to stand just this side of it, since the strict
    // distance check alone then says the tree is beyond, not blocking, that exact point. That
    // reads as broken right at the start of a session, when a first-time player is still
    // clicking around to get their bearings.
    public float OcclusionDistanceTolerance { get; } = 2f;

    // A fixed screen-space size/gap, not a 3D world one: the marker used to be a billboarded
    // Sprite3D offset in local space, but a billboard's own on-screen "left/right" is
    // redefined every frame to match whatever the camera's current right vector is (that's
    // what "always face the camera" means) - so a fixed local offset drifted sideways by a
    // different amount depending on which way the camera was currently facing. Projecting a
    // single stable world point (the head) with Camera3D.UnprojectPosition and drawing the
    // marker as a plain 2D UI overlay above it sidesteps that entirely - Godot's own
    // projection handles the camera math, nothing here has to reconstruct it by hand.
    public float SelectionMarkerScreenSize { get; } = 28f;

    public float SelectionMarkerScreenGap { get; } = 6f;

    // A person standing genuinely behind something opaque (a tree trunk, say - not just
    // sharing an oversized collision box with it, see Main.OnMissedClick) can never be
    // reached by raycasting at all: the ray hits the opaque trunk pixel first and that *is* a
    // real hit, not a miss to fall through from. Screen-space distance to a person's own
    // projected position sidesteps 3D occlusion entirely - close enough on screen counts as
    // "aiming at them" regardless of what's actually in front of them along the ray.
    //
    // Measured from the person's origin at mid-body, so it also decides how much ground
    // around a bystander's feet a walk order can't be aimed at (the click selects them
    // instead) - 32 reached well past the silhouette at the usual zoom. 20 still covers the
    // torso of a half-hidden figure; the selected person is exempt regardless (see
    // Main.FindNearestPersonOnScreen).
    public float PersonClickScreenRadius { get; } = 20f;

    // Comfortably inside SimulationRules.MaxInteractionDistance (2f by default), but far enough
    // out that a person's own sprite doesn't overlap the resource node's - a visual standoff,
    // which is why it lives here rather than among the world's rules.
    public float ApproachDistance { get; } = 1.2f;

    // The starting band spans roughly 8x4 units and is centered exactly on campPosition
    // (see MapLoader.LoadDefault), so a close default zoom lets it fill most of the frame
    // right away rather than reading as a handful of specks in a huge empty field.
    public float InitialZoomDistance { get; init; } = 10f;

    public float MinZoom { get; } = 3f;

    public float MaxZoom { get; } = 2000f;
}
