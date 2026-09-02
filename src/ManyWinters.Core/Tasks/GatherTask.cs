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
        // ApproachDistance is shorter than MaxInteractionDistance, so the walk always ends at
        // the check above rather than at the standoff point itself: the leg never completes,
        // and neither the caching nor the reset below can be reached by any starting position.
        // They're kept because the standoff is a rendering choice that could yet be moved past
        // interaction range, at which point both start mattering again.
        // Stryker disable all: unreachable while ApproachDistance stays under
        // MaxInteractionDistance - the reach check above returns before either the caching or
        // the leg reset can ever come into play
        _approachPosition ??= ApproachPosition(person.Position, TargetPosition, ApproachDistance);
        _move ??= new MoveTask(_approachPosition.Value, SpeedPerTick);

        // Stryker restore all
        _move.Advance(person);

        // Stryker disable all: as above - the leg never reaches the standoff point, so it
        // never reports itself complete
        if (_move.IsComplete)
        {
            _move = null;
        }

        // Stryker restore all
    }

    private static Position ApproachPosition(Position from, Position to, float standoffDistance)
    {
        var dx = from.X - to.X;
        var dy = from.Y - to.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        // Stryker disable all: the only caller checks a larger reach first, so nobody already
        // inside the standoff distance ever gets this far
        if (distance <= standoffDistance)
        {
            return from;
        }

        // Stryker restore all

        var ratio = standoffDistance / distance;
        return new Position(to.X + (dx * ratio), to.Y + (dy * ratio));
    }
}
