using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// A presentation-layer hint, not a player action - lets whoever's driving the simulation
// (the currently-selected person, say) buy someone a few more ticks of standing still before
// WorldState.Advance would otherwise drop them into an IdleTask (see Person.IdleGraceUntilTick).
// Calling it every tick while a person stays selected keeps extending the window, so they never
// wander off mid-attention; the grace simply runs out a few ticks after they stop being renewed.
public sealed record GrantIdleGraceCommand(Person Person, long GraceTicks) : ICommand
{
    public void Execute(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return;
        }

        Person.IdleGraceUntilTick = world.Clock.CurrentTick + GraceTicks;
    }
}
