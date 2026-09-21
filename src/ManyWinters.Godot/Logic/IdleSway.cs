using Godot;

namespace ManyWinters.Godot.Logic;

// What a standing person does instead of freezing: the walk cycle's bob at a slower rate and
// smaller amplitude, on the frame clock rather than the simulation's, so a band under a stopped
// clock still moves. The bob itself is too fast to ease - a low-pass over it is mostly damping -
// so the caller scales it by a weight, and that weight is what eases here.
internal static class IdleSway
{
    // Exponential rather than linear, so the approach never overshoots or visibly "arrives";
    // `settleSeconds` is the time constant (about two thirds of the way there after that long).
    internal static float Settle(float current, float target, float delta, float settleSeconds) =>
        Mathf.Lerp(current, target, Weight(delta, settleSeconds));

    // The same easing for an offset - brings the last step's bounce to nothing once a person
    // stands, rather than leaving them frozen mid-stride.
    internal static Vector3 Settle(Vector3 current, Vector3 target, float delta, float settleSeconds) =>
        current.Lerp(target, Weight(delta, settleSeconds));

    private static float Weight(float delta, float settleSeconds) => 1f - MathF.Exp(-delta / settleSeconds);
}
