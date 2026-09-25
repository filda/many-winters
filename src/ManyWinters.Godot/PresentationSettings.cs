namespace ManyWinters.Godot;

// The tunable numbers the presentation layer draws and picks with. The Godot-side counterpart
// of SimulationRules: configuration, not state, and none of it is a rule of the world (nothing
// in ManyWinters.Core sees it). Default is what the shipped game uses; TerrainSandbox overrides
// only the zoom. Same setter rule as SimulationRules: only what something overrides has `init`.
public sealed record PresentationSettings
{
    public static PresentationSettings Default { get; } = new();

    // Godot's UI default (16) reads oversized for a dense debug panel.
    public int InspectorFontSize { get; } = 13;

    // Extra margin (meters) on top of each candidate's own half-width, roughly the selected
    // person's half-width: something has to clear the target's silhouette, not just its center
    // point, to not count as blocking. OcclusionFadedAlpha is how transparent a blocker fades to.
    public float OcclusionMargin { get; } = 0.3f;

    public float OcclusionFadedAlpha { get; } = 0.25f;

    // Slack (meters) past the target's distance before something stops counting as in the way.
    // Without it a tree fading for the farther no-selection fallback target snaps solid the
    // instant you select a person standing just this side of it.
    public float OcclusionDistanceTolerance { get; } = 2f;

    // Screen pixels, not world units: a billboard redefines its local left/right every frame to
    // the camera's right vector, so a marker offset in billboard space drifts sideways with
    // camera facing. Projecting the head with Camera3D.UnprojectPosition and drawing a 2D
    // overlay above it leaves the camera math to Godot.
    public float SelectionMarkerScreenSize { get; } = 28f;

    public float SelectionMarkerScreenGap { get; } = 6f;

    // A person genuinely behind something opaque (a tree trunk, not just an oversized collision
    // box - see HoverRescue) is unreachable by raycast: the trunk pixel is a real hit. Screen
    // distance to the person's projected position ignores 3D occlusion entirely.
    //
    // Measured from the origin at mid-body, so it also decides how much ground around a
    // bystander's feet a walk order cannot target; 20 still covers a half-hidden torso. The
    // selected person is exempt (see Main.FindNearestPersonOnScreen).
    public float PersonClickScreenRadius { get; } = 20f;

    // Inside SimulationRules.MaxInteractionDistance (2f), but far enough that the person's sprite
    // doesn't overlap the node's - a visual standoff, hence here and not among the world's rules.
    public float ApproachDistance { get; } = 1.2f;

    // Same standoff as ApproachDistance, scaled down for SimulationRules.PileReachDistance (1f):
    // a pile sits underfoot, so the ordinary approach distance would leave the walk short of
    // reach and the order stuck (EatFromPileCommand, PickUpItemCommand).
    public float PileApproachDistance { get; } = 0.6f;

    // The starting band spans roughly 8x4 units centered on the camp (MapLoader.LoadDefault), so
    // a close zoom fills the frame with it instead of a handful of specks in an empty field.
    public float InitialZoomDistance { get; init; } = 10f;

    public float MinZoom { get; } = 3f;

    public float MaxZoom { get; } = 2000f;
}
