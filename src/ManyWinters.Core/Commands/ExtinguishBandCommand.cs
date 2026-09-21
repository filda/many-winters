using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// The inspector's "Extinguish Band" button (docs/todo/todo.md): a debug lever that writes the
// same death WorldState.Advance would, so the ending announcement, epitaph and "Another band
// comes" offer all follow through the world's own machinery next tick. Deaths land at once
// rather than through hunger, because merely maxed hunger would not end the band: anyone
// carrying food who knows how to eat is fed back below the threshold every tick
// (WorldState.TryAutoEat), and a nursing infant is fed at its mother's side. Hunger is the cause
// written because it's what the epitaph reads for any death not of old age. Nothing in the
// simulation calls this.
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
