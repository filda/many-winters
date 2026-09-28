using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// "Go take apart this carcass": a carcass lies where it fell like a pile, not a node or building,
// so this walks to it exactly the way GatherTask walks to a pile - the standoff, the reach, all
// of it. Only walks; the simulation runs a butchering command every tick this task is current,
// exactly as it does for eating from a pile, and that command's own blocker no-ops while still
// out of reach. Never completes on its own. speedPerTick comes from whoever installs this task: a
// player-directed butchering walks at the directed speed like every other order, while the
// autonomous idle AI passes GatherTask's unhurried pace - the same asymmetry every other directed
// order already has against idling.
public sealed class ButcherTask(
    Animal carcass,
    float reach,
    float speedPerTick,
    float approachFractionOfReach
    ) : CreatureTask
{
    private Position? _approachPosition;
    private MoveTask? _move;

    public Animal Carcass { get; } = carcass;

    public float Reach { get; } = reach;

    // Read-only: exposed so a directed butchering can be asserted to carry the directed speed
    // rather than the idle one.
    public float SpeedPerTick { get; } = speedPerTick;

    public override bool IsComplete => false;

    public override void Advance(Creature creature)
    {
        if (WorldState.Distance(creature.Position, Carcass.Position) <= Reach)
        {
            _move = null;
            return;
        }

        // Stryker disable once Assignment: a carcass does not move, so recomputing walks the
        // same route.
        _approachPosition ??= Position.Approach(creature.Position, Carcass.Position, Reach * approachFractionOfReach);
        // Stryker disable once Assignment: the reach check above always ends the leg first, so
        // a rebuilt MoveTask steps identically.
        _move ??= new MoveTask(_approachPosition.Value, SpeedPerTick);
        _move.Advance(creature);
    }
}
