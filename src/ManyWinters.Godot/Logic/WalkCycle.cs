using Godot;

namespace ManyWinters.Godot.Logic;

// How a person's sprite moves between the positions the simulation hands it, and how it bobs
// while it does. PersonView owns the nodes and per-person tuning. Only a bob: the sprites are
// FixedY billboards, and Godot discards a billboard node's rotation when drawing it.
internal static class WalkCycle
{
    // Below this a person has arrived and the walk animation stops. Not zero: MoveToward does
    // land exactly on its target, but an exact float comparison flickers on the last frame.
    private const float ArrivedDistance = 0.001f;

    // Speed that covers `distance` in `overSeconds`, so the view arrives exactly as the next
    // tick hands it a new target. No time at all means snap: a corpse stops where it fell.
    internal static float InterpolationSpeed(float distance, float overSeconds) =>
        overSeconds > 0f ? distance / overSeconds : float.MaxValue;

    // Stryker disable once Equality: a distance landing exactly on the epsilon has probability zero
    internal static bool IsWalking(Vector3 position, Vector3 target) =>
        position.DistanceTo(target) > ArrivedDistance;

    internal static float Advanced(float phase, float delta, float cyclesPerSecond) =>
        phase + (delta * cyclesPerSecond);

    // Straight up and down, so the cutout never slides off its own feet. All of a person's
    // layers take the same offset - one rigid cutout, not independently animated parts.
    internal static Vector3 BobAt(float phase, float amplitude) => new(0f, MathF.Sin(phase) * amplitude, 0f);
}
