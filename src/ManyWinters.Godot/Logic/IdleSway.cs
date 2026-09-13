using Godot;

namespace ManyWinters.Godot.Logic;

// What a standing person does instead of freezing: the walk cycle's own bob (WalkCycle.BobAt)
// at a slightly slower rate and a smaller amplitude, running on the frame clock rather than the
// simulation's - so a band under a stopped clock (an inscription on screen) still moves, and
// people at rest stop reading as paper cutouts pinned to the ground.
//
// What this class adds to the walk cycle is only how the bob comes and goes. It is too fast to
// be eased directly - a low-pass over an eight-hertz signal is mostly damping - so PersonView
// scales it by a weight instead, and that weight is what eases here.
internal static class IdleSway
{
    // A value eases toward its target rather than jumping to it. Exponential rather than
    // linear so the approach never overshoots and never visibly "arrives"; `settleSeconds` is
    // the time constant, about two thirds of the way there after that long.
    internal static float Settle(float current, float target, float delta, float settleSeconds) =>
        Mathf.Lerp(current, target, Weight(delta, settleSeconds));

    // The same easing for an offset - what brings the last step's bounce down to nothing once
    // a person stands, rather than leaving them frozen mid-stride.
    internal static Vector3 Settle(Vector3 current, Vector3 target, float delta, float settleSeconds) =>
        current.Lerp(target, Weight(delta, settleSeconds));

    private static float Weight(float delta, float settleSeconds) => 1f - MathF.Exp(-delta / settleSeconds);
}
