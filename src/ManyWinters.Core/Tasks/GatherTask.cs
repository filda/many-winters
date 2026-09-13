using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// Autonomous "go gather from this resource" order from WorldState's idle AI (DecideIdleTask).
// Only walks there; harvesting is GatherCommand at WorldState level, since PersonTask.Advance
// sees only the Person. Never completes - WorldState.Advance re-evaluates it every tick.
// `reachDistance` is SimulationRules.MaxInteractionDistance, passed in as Advance has no world.
public sealed class GatherTask(ResourceNode target, float reachDistance) : PersonTask
{
    private const float SpeedPerTick = 0.3f;

    // Stop short of the resource rather than on it, the same standoff as a player-directed
    // gather-walk (PresentationSettings.ApproachDistance via Position.Approach). A fraction of
    // reach, not an absolute, so a shorter reach still has people stop inside it.
    private const float ApproachFractionOfReach = 0.6f;

    private Position? _approachPosition;
    private MoveTask? _move;

    public ResourceNode Target { get; } = target;

    public float ReachDistance { get; } = reachDistance;

    public override bool IsComplete => false;

    public override void Advance(Person person)
    {
        if (WorldState.Distance(person.Position, Target.Position) <= ReachDistance)
        {
            _move = null;
            return;
        }

        // Computed once from where the person started walking (lazy first Advance, like
        // IdleTask's _anchor). The walk always ends at the reach check above, never at the
        // standoff point, since the standoff is shorter than ReachDistance.
        // Stryker disable once Assignment: same straight line to a resource that never moves, so recomputing walks the same route
        _approachPosition ??= Position.Approach(person.Position, Target.Position, ReachDistance * ApproachFractionOfReach);
        // Stryker disable once Assignment: the reach check above always ends the leg first, so a rebuilt MoveTask steps identically
        _move ??= new MoveTask(_approachPosition.Value, SpeedPerTick);
        _move.Advance(person);
    }
}
