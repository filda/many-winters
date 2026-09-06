using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// Autonomous "go gather from this specific resource" order, decided by WorldState.Advance's
// idle-AI (see DecideIdleTask) - this task only ever knows how to walk there (same
// self-contained movement pattern as IdleTask's own internal MoveTask); the actual harvesting
// is a WorldState-level side effect (GatherCommand), since PersonTask.Advance only ever sees
// the Person, not the world. Never completes on its own - WorldState.Advance re-evaluates
// every tick whether this is still the right thing to be doing (target still alive and not
// depleted), same as it does for IdleTask.
//
// `reachDistance` is the world's SimulationRules.MaxInteractionDistance, handed in by whoever
// creates the task (DecideIdleTask) - Advance itself never sees the world, so it can't look
// the rule up.
public sealed class GatherTask(ResourceNode target, float reachDistance) : PersonTask
{
    private const float SpeedPerTick = 0.3f;

    // Short of the resource's own position, not standing exactly on it - the same standoff
    // Main.cs uses for a player-directed gather-walk (PresentationSettings.ApproachDistance,
    // via Position.Approach), so an autonomous approach reads the same as a manually clicked
    // one instead of the person visually overlapping the sprite. As a fraction of reach rather
    // than an absolute: a world with a shorter reach still has its people stop inside it, not
    // at a standoff point they can't gather from.
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

        // Computed once, from wherever the person happened to be when they first started
        // walking - same lazy-first-Advance-call pattern as IdleTask's own _anchor, so this
        // doesn't need a WorldState-aware constructor.
        //
        // The walk always ends at the reach check above, never at the standoff point itself:
        // the standoff is shorter than ReachDistance, so a person is already close enough to
        // gather before the leg would finish. The leg is therefore only ever cleared by that
        // check, on the tick it stops the walk.
        _approachPosition ??= Position.Approach(person.Position, Target.Position, ReachDistance * ApproachFractionOfReach);
        _move ??= new MoveTask(_approachPosition.Value, SpeedPerTick);
        _move.Advance(person);
    }
}
