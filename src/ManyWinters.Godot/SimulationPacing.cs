namespace ManyWinters.Godot;

// How this client runs the world against real time. Neither number is a rule of the world (the
// headless SimulationRunner has no clock and SimulationRules never mentions seconds) - only of
// the live session. Configuration, not state; same `init` setter rule as SimulationRules.
public sealed record SimulationPacing
{
    public static SimulationPacing Default { get; } = new();

    public double TickIntervalSeconds { get; } = 1.0;

    // Renewed every tick while the person stays selected (see Main._Process); the window only
    // runs out once selection moves on.
    public long SelectedPersonIdleGraceTicks { get; } = 5;
}
