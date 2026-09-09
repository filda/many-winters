using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// "Stay with this person" - walks toward the target whenever the gap opens past `keepWithin`
// and stands still once inside it. Today's only user is an infant keeping up with its mother
// (see WorldState.DecideIdleTask), which is also what feeds it and the only reason it is ever
// close enough to be taught anything (TeachCommand checks reach).
//
// Never completes, same as IdleTask and GatherTask: how long following is the right thing to
// be doing is WorldState.Advance's call, not the task's - it has no idea the child will one
// day be weaned.
//
// Unlike GatherTask's one-off approach to a resource that never moves, the destination is
// re-aimed every tick, because the mother is walking around too.
public sealed class FollowTask(Person target, float keepWithin, float speedPerTick) : PersonTask
{
    public Person Target { get; } = target;

    // Deliberately a gap, not a point to stand on: arriving exactly on top of the target
    // would leave the two of them shoving each other apart every tick (see
    // WorldState.ResolveCollisions) and walking back together the next.
    public float KeepWithin { get; } = keepWithin;

    public override bool IsComplete => false;

    public override void Advance(Person person)
    {
        if (WorldState.Distance(person.Position, Target.Position) <= KeepWithin)
        {
            return;
        }

        // A fresh leg every tick rather than a kept MoveTask: its destination is fixed at
        // construction, and this one moves.
        new MoveTask(Target.Position, speedPerTick).Advance(person);
    }
}
