using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// The inspector's "Extinguish Band" button (docs/todo/todo.md): a debug lever that ends the band
// by writing the very death WorldState.Advance writes, so the ending announcement, the epitaph
// and its "Another band comes" offer all follow through the world's own machinery on the next
// tick. The deaths land at once rather than through hunger, because a merely maxed hunger would
// not end the band at all: anyone carrying food and knowing how to eat is fed back below the
// threshold every tick (WorldState.TryAutoEat), and a nursing infant is fed at its mother's
// side. Hunger is the cause written because it is what the epitaph reads for any death that was
// not of old age. Nothing in the simulation calls this.
public sealed record ExtinguishBandCommand : ICommand
{
    // A debug lever, not a player action: there is no state in which it refuses.
    public ActionBlocker Blocker(WorldState world) => ActionBlocker.None;

    public void Execute(WorldState world)
    {
        foreach (var person in world.People)
        {
            if (!person.IsAlive)
            {
                continue;
            }

            person.IsAlive = false;
            person.DeathTick = world.Clock.CurrentTick;
            person.CauseOfDeath = DeathCause.Hunger;
        }
    }
}
