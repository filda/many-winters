using Godot;

namespace ManyWinters.Godot.Logic;

// How a person's sprite moves between the positions the simulation hands it, and how it bobs
// while it does. PersonView owns the nodes and the per-person tuning; the shape of the motion
// is here. Only a bob: the sprites are FixedY billboards, and Godot discards a billboard node's
// rotation when it draws it, so the side-to-side rock this once had was never seen and is gone.
internal static class WalkCycle
{
    // Below this a person has arrived, so the walk animation stops advancing. Not zero:
    // MoveToward lands exactly on its target eventually, but a float comparison against
    // exactly equal would flicker on the last frame of every leg.
    private const float ArrivedDistance = 0.001f;

    // Speed that covers `distance` in `overSeconds`, so a person's view arrives exactly as the
    // next simulation tick hands it a new target rather than stuttering early or lagging
    // behind. No time at all means snap: a corpse should stop where it fell, with nothing left
    // to glide.
    internal static float InterpolationSpeed(float distance, float overSeconds) =>
        overSeconds > 0f ? distance / overSeconds : float.MaxValue;

    // Stryker disable once Equality: a distance landing exactly on the epsilon has probability zero
    internal static bool IsWalking(Vector3 position, Vector3 target) =>
        position.DistanceTo(target) > ArrivedDistance;

    internal static float Advanced(float phase, float delta, float cyclesPerSecond) =>
        phase + (delta * cyclesPerSecond);

    // Where the sprite sits at one point in the cycle: straight up and down, so the cutout
    // never slides off its own feet. All of a person's layers take the same offset - they are
    // one rigid cutout, not independently animated parts.
    internal static Vector3 BobAt(float phase, float amplitude) => new(0f, MathF.Sin(phase) * amplitude, 0f);
}
