namespace ManyWinters.Godot;

// Set by the screenshot E2E suite (MW_E2E_DETERMINISTIC=1) to freeze the presentation that runs on
// real time rather than the simulation clock — the person idle bob and the live status-bar
// counters — so a captured frame is identical run to run and can be pixel-compared against a
// committed baseline. Off by default; normal play is untouched.
internal static class DeterministicPresentation
{
    public static readonly bool Enabled = Environment.GetEnvironmentVariable("MW_E2E_DETERMINISTIC") == "1";
}
