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
public sealed class GatherTask : PersonTask
{
    private const float SpeedPerTick = 0.3f;

    // Short of the resource's own position, not standing exactly on it - same standoff Main.cs
    // uses for a player-directed gather-walk (ApproachPosition), so an autonomous approach
    // reads the same as a manually clicked one instead of the person visually overlapping the
    // sprite.
    private const float ApproachDistance = 1.2f;

    private Position? _approachPosition;
    private MoveTask? _move;

    public GatherTask(ResourceNodeId targetNodeId, Position targetPosition)
    {
        TargetNodeId = targetNodeId;
        TargetPosition = targetPosition;
    }

    public ResourceNodeId TargetNodeId { get; }

    public Position TargetPosition { get; }

    public override bool IsComplete => false;

    public override void Advance(Person person)
    {
        if (WorldState.Distance(person.Position, TargetPosition) <= WorldState.MaxInteractionDistance)
        {
            _move = null;
            return;
        }

        // Computed once, from wherever the person happened to be when they first started
        // walking - same lazy-first-Advance-call pattern as IdleTask's own _anchor, so this
        // doesn't need a WorldState-aware constructor.
        //
        // The walk always ends at the reach check above, never at the standoff point itself:
        // ApproachDistance is shorter than MaxInteractionDistance, so a person is already close
        // enough to gather before the leg would finish. The leg is therefore only ever cleared
        // by that check, on the tick it stops the walk.
        _approachPosition ??= ApproachPosition(person.Position, TargetPosition, ApproachDistance);
        _move ??= new MoveTask(_approachPosition.Value, SpeedPerTick);
        _move.Advance(person);
    }

    // Only ever called from further away than standoffDistance (see Advance's reach check), so
    // the distance below is never zero.
    private static Position ApproachPosition(Position from, Position to, float standoffDistance)
    {
        var dx = from.X - to.X;
        var dy = from.Y - to.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));
        var ratio = standoffDistance / distance;
        return new Position(to.X + (dx * ratio), to.Y + (dy * ratio));
    }
}
