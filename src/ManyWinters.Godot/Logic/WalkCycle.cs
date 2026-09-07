using Godot;

namespace ManyWinters.Godot.Logic;

// How a person's sprite moves between the positions the simulation hands it, and how it bobs
// and rocks while it does. PersonView owns the nodes and the per-person tuning; the shape of
// the motion is here.
internal static class WalkCycle
{
    // Below this a person has arrived, so the walk animation stops advancing. Not zero:
    // MoveToward lands exactly on its target eventually, but a float comparison against
    // exactly equal would flicker on the last frame of every leg.
    private const float ArrivedDistance = 0.001f;

    // The rock is deliberately half the bob's frequency: a body dips once per footfall but
    // leans over once per stride, so a rock in step with the bob reads as a limp.
    private const float RockCyclesPerBobCycle = 0.5f;

    // Where the sprite sits and how far it leans at one point in the cycle. All of a person's
    // layers take the same pose - they are one rigid cutout, not independently animated parts.
    internal readonly record struct Pose(Vector3 Offset, Vector3 Rotation);

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

    internal static Pose PoseAt(float phase, float bobAmplitude, float rockAmplitude) =>
        new(
            new Vector3(0f, MathF.Sin(phase) * bobAmplitude, 0f),
            // Rotation about local Z: the cutout leans left and right, which is the only axis
            // a FixedY billboard can rock on without breaking the illusion that it faces the
            // camera.
            new Vector3(0f, 0f, MathF.Sin(phase * RockCyclesPerBobCycle) * rockAmplitude));
}
