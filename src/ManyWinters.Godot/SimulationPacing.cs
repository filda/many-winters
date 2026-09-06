namespace ManyWinters.Godot;

// How this client runs the world against real time - how many wall-clock seconds one tick
// takes, and how long a person the player is currently looking at keeps waiting for orders.
// Neither is a rule of the world (the headless SimulationRunner advances ticks with no clock
// at all, and SimulationRules never mentions seconds) - only of the live, watched session.
// Configuration, not state; Default is what the shipped game uses. Same setter rule as
// SimulationRules: a property grows an `init` setter once something actually overrides it.
public sealed record SimulationPacing
{
    public static SimulationPacing Default { get; } = new();

    public double TickIntervalSeconds { get; } = 1.0;

    // Renewed every tick the person stays selected (see Main._Process), so they never wander
    // off mid-attention - only once selection moves on does this window actually run out.
    public long SelectedPersonIdleGraceTicks { get; } = 5;
}
