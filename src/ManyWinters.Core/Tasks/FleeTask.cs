using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// "Move directly away from this threat" - the species' own flight rule: a creature whose species
// defines Flee breaks off whatever it was doing the moment a living person comes within
// FleeDistance and runs a straight line away from them until the gap reaches SafeDistance or the
// threat dies. No seeded jitter - a straight line is enough for the shipped tuning.
public sealed class FleeTask(Creature threat, SpeciesDefinition.FleeDefinition flee) : CreatureTask
{
    public Creature Threat { get; } = threat;

    // Set by Advance, the same pattern as MoveTask's _arrived: the queue calls Advance(creature)
    // then checks IsComplete with no creature to hand it.
    private bool _safe;

    public override bool IsComplete => _safe || !Threat.IsAlive;

    public override void Advance(Creature creature)
    {
        if (!Threat.IsAlive || WorldState.Distance(creature.Position, Threat.Position) >= flee.SafeDistance)
        {
            _safe = true;
            return;
        }

        var dx = creature.Position.X - Threat.Position.X;
        var dy = creature.Position.Y - Threat.Position.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        if (distance <= 0d)
        {
            // Standing exactly on the threat: no direction to flee in yet, next tick's collision
            // resolution is what actually separates them.
            return;
        }

        var ratio = flee.SpeedPerTick / distance;
        creature.Position = new Position(creature.Position.X + (dx * ratio), creature.Position.Y + (dy * ratio));
    }
}
