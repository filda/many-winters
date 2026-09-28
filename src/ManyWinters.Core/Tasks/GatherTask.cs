using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// Autonomous "go gather from this resource" order from the idle AI, or "go eat from this pile"
// for a hungry person. Only walks there; the taking happens elsewhere, since advancing only
// sees the Creature. Never completes - the caller re-evaluates it every tick.
// `reachDistance` is the maximum interaction distance, passed in as Advance has no world.
public sealed class GatherTask(Entity target, float reachDistance, float speedPerTick, float approachFractionOfReach) : CreatureTask
{
    private Position? _approachPosition;
    private MoveTask? _move;

    public Entity Target { get; } = target;

    public float ReachDistance { get; } = reachDistance;

    public override bool IsComplete => false;

    public override void Advance(Creature creature)
    {
        if (WorldState.Distance(creature.Position, Target.Position) <= ReachDistance)
        {
            _move = null;
            return;
        }

        // Computed once from where the person started walking (lazy first Advance). The walk
        // always ends at the reach check above, never at the standoff point, since the standoff
        // is shorter than ReachDistance.
        // Stryker disable once Assignment: same straight line to a resource that never moves, so recomputing walks the same route
        _approachPosition ??= Position.Approach(creature.Position, Target.Position, ReachDistance * approachFractionOfReach);
        // Stryker disable once Assignment: the reach check above always ends the leg first, so a rebuilt MoveTask steps identically
        _move ??= new MoveTask(_approachPosition.Value, speedPerTick);
        _move.Advance(creature);
    }
}
