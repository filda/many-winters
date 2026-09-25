namespace ManyWinters.Godot;

// Set by the screenshot E2E suite (MW_E2E_DETERMINISTIC=1) to freeze the presentation that runs on
// real time rather than the simulation clock — the person idle bob, the live status-bar counters,
// and (below) the simulation tick itself — so a captured frame is identical run to run and can be
// pixel-compared against a committed baseline. Off by default; normal play is untouched.
internal static class DeterministicPresentation
{
    public static readonly bool Enabled = Environment.GetEnvironmentVariable("MW_E2E_DETERMINISTIC") == "1";

    // With the deterministic presentation on, the simulation clock is also held at the boot tick
    // rather than ticking once a second: a screenshot test that dismisses the prologue and then
    // works the UI would otherwise land on whichever tick the wall clock happens to have reached
    // (one or two, depending on how long the preceding input and captures took), and a moving band
    // is not a frame that pixel-compares. The suite advances the world deliberately, one tick at a
    // time, with the "advance one tick" key (see Main._Input) - the same single-step the
    // TickAccumulator owes after a held clock is let go - so a placed order (a craft, a building)
    // is resolved exactly once and the frame settles at a fixed tick. Normal play (Enabled false)
    // never freezes; the key is ignored.
    public static bool SimulationFrozen => Enabled;
}
