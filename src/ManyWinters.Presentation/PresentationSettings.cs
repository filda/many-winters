namespace ManyWinters.Presentation;

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
    // box) is unreachable by raycast: the trunk pixel is a real hit. Screen distance to the
    // person's projected position ignores 3D occlusion entirely.
    //
    // Measured from the origin at mid-body, so it also decides how much ground around a
    // bystander's feet a walk order cannot target; 20 still covers a half-hidden torso. The
    // selected person is exempt.
    public float PersonClickScreenRadius { get; } = 20f;

    // Inside the world's max interaction distance (2f), but far enough that the person's sprite
    // doesn't overlap the node's - a visual standoff, hence here and not among the world's rules.
    public float ApproachDistance { get; } = 1.2f;

    // Same standoff as ApproachDistance, scaled down for the world's pile-reach distance (1f):
    // a pile sits underfoot, so the ordinary approach distance would leave the walk short of
    // reach and the order stuck (EatFromPileCommand, PickUpItemCommand).
    public float PileApproachDistance { get; } = 0.6f;

    // The starting band spans roughly 8x4 units centered on the camp, so a close zoom fills the
    // frame with it instead of a handful of specks in an empty field.
    public float InitialZoomDistance { get; init; } = 10f;

    public float MinZoom { get; } = 3f;

    public float MaxZoom { get; } = 2000f;

    // Pan speed scales with zoom distance: the zoom range spans 3 to 2000, so a fixed speed is
    // glacial zoomed out and wild zoomed in - the same reason zoom is multiplicative.
    public float PanSpeedPerZoomUnit { get; } = 1f;

    // How fast velocity eases toward its target - higher = snappier, lower = floatier.
    // 1/PanEaseRate is roughly the time constant (seconds) to close ~63% of the gap.
    public float PanEaseRate { get; } = 10f;

    public float RotateSpeed { get; } = 1.5f;

    public float ZoomRatePerSecond { get; } = 2.5f;

    // A wheel notch has no delta of its own, so it's treated as this many seconds' worth of
    // R/F's held-key rate - keeps a single zoom feel instead of a separately tuned step.
    public float ScrollZoomNotchSeconds { get; } = 0.05f;

    // Right-drag rotate/tilt, alongside Q/E and Page Up/Down for mouse-less control.
    public float MouseRotateRadiansPerPixel { get; } = 0.005f;

    public float MouseTiltDegreesPerPixel { get; } = 0.15f;

    // How far (screen pixels) the cursor may wander between a right press and release before it
    // counts as a drag rather than a click. A few pixels of slack rather than none: a mouse
    // drifts under a real finger, and a menu that refuses to open half the time is worse than
    // one that occasionally opens after a nudge.
    public float RightClickDragThresholdPixels { get; } = 4f;

    // Degrees of elevation above the rig's plane; height = zoom * sin, distance = zoom * cos.
    // The clamp keeps the view from going fully overhead or edge-on, both of which break the
    // cutout illusion. The upper bound matters most: FixedY billboards only yaw toward the
    // camera's horizontal direction, so at 90 deg every sprite renders edge-on.
    public float DefaultTiltDegrees { get; } = 20f;

    public float MinTiltDegrees { get; } = 12f;

    public float MaxTiltDegrees { get; } = 70f;

    public float TiltSpeedDegreesPerSecond { get; } = 45f;

    // Minimum clearance the camera keeps above the ground directly under it.
    public float MinCameraGroundClearance { get; } = 0.3f;

    // ViewRadius's margin over the raw zoom distance: at the default tilt the ground footprint
    // in view reaches well past the zoom distance itself (perspective spread plus the diagonal
    // of a non-square viewport), and this is a cheap over-estimate rather than a per-frustum
    // computation - WorldPresenter only uses it to decide which decorations are worth a node,
    // where popping in a touch early costs nothing a real culling error would.
    public float ViewRadiusMultiplier { get; } = 3f;

    // Depth precision depends on the Far/Near ratio, not Far alone. The engine default Near
    // (0.05) against this Far gave 100,000:1 - so little precision remained at background-tree
    // depths that the fog-of-war depth-reconstruction shaders (fog_of_war_screen.gdshader,
    // fog_of_war_remembered.gdshader) cut a flat "ceiling" through unrelated canopies. 0.5 cuts
    // the ratio 10x; nothing is ever legitimately closer to the camera than that.
    public float CameraFar { get; } = 5000f;

    public float CameraNear { get; } = 0.5f;

    // Fog of war's "remembered" tier on a sprite: losing sight is memory gradually taking over,
    // so it eases out over about a second; regaining it is an event, so it snaps back in a
    // fraction of that. Equal durations read as the world lagging behind the group.
    public float FadeToRememberedSeconds { get; } = 1.2f;

    public float FadeToVisibleSeconds { get; } = 0.35f;
}
