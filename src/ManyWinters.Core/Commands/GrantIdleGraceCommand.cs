using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// A presentation-layer hint, not a player action: buys someone a few ticks of standing still
// before WorldState.Advance drops them into an IdleTask (see Person.IdleGraceUntilTick).
// Renewed every tick while a person stays selected, so they do not wander off mid-attention.
public sealed record GrantIdleGraceCommand(Person Person, long GraceTicks) : ICommand
{
    public ActionBlocker Blocker(WorldState world) =>
        Person.IsAlive ? ActionBlocker.None : ActionBlocker.ActorIsDead;

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        Person.IdleGraceUntilTick = world.Clock.CurrentTick + GraceTicks;
    }
}
