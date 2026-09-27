using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// Autonomous "go gather from this resource" order from the idle AI, or "go eat from this pile"
// for a hungry person. Only walks there; the taking happens elsewhere, since CreatureTask.Advance
// sees only the Creature. Never completes - the caller re-evaluates it every tick.
// `reachDistance` is the maximum interaction distance, passed in as Advance has no world.
public sealed class GatherTask(Entity target, float reachDistance) : CreatureTask
{
    // Public: the idle AI reuses this exact number for the autonomous HuntTask and ButcherTask it
    // installs, so the three autonomous foraging tasks all walk at the same unhurried pace; tests
    // reuse it too, to build an idle-speed task without duplicating the number.
    public const float SpeedPerTick = 0.3f;

    // Stop short of the resource rather than on it, the same standoff as a player-directed
    // gather-walk. A fraction of reach, not an absolute, so a shorter reach still has people stop
    // inside it.
    private const float ApproachFractionOfReach = 0.6f;

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
        _approachPosition ??= Position.Approach(creature.Position, Target.Position, ReachDistance * ApproachFractionOfReach);
        // Stryker disable once Assignment: the reach check above always ends the leg first, so a rebuilt MoveTask steps identically
        _move ??= new MoveTask(_approachPosition.Value, SpeedPerTick);
        _move.Advance(creature);
    }
}
