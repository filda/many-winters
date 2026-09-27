using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// A presentation-layer hint, not a player action: buys someone a few ticks of standing still
// before WorldState.Advance drops them into an IdleTask. Renewed every tick while a person or an
// animal stays selected, so they do not wander off mid-attention. Creature, not Person: only
// IsAlive and IdleGraceUntilTick are touched, both shared by every Creature, and a selected
// animal deserves the same courtesy.
public sealed record GrantIdleGraceCommand(Creature Creature, long GraceTicks) : ICommand
{
    public ActionBlocker Blocker(WorldState world) =>
        Creature.IsAlive ? ActionBlocker.None : ActionBlocker.ActorIsDead;

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        Creature.IdleGraceUntilTick = world.Clock.CurrentTick + GraceTicks;
    }
}
