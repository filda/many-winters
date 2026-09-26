using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// "Stay with this person": walks toward the target whenever the gap opens past `keepWithin`.
// Used for an infant keeping up with its mother (WorldState.DecideIdleTask), which is what feeds
// it and keeps it in teaching reach. Never completes; how long to follow is WorldState.Advance's call.
public sealed class FollowTask(Creature target, float keepWithin, float speedPerTick) : CreatureTask
{
    public Creature Target { get; } = target;

    // A gap, not a point to stand on: arriving on top of the target would have the two shoved
    // apart every tick (WorldState.ResolveCollisions) and walking back together the next.
    public float KeepWithin { get; } = keepWithin;

    public override bool IsComplete => false;

    public override void Advance(Creature creature)
    {
        if (WorldState.Distance(creature.Position, Target.Position) <= KeepWithin)
        {
            return;
        }

        // A fresh MoveTask every tick: its destination is fixed at construction, the target moves.
        new MoveTask(Target.Position, speedPerTick).Advance(creature);
    }
}
