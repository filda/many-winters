using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// "Close in on this deer": walks toward the *moving* prey with a fresh MoveTask every tick, the
// same pattern FollowTask uses for a target that does not sit still. Only walks; the throw itself
// happens elsewhere, since CreatureTask.Advance sees only the Creature and a throw costs time
// (NextAttemptTick) the way a workbench attempt does. Never completes on its own - a
// reconsideration loop decides when hunting this prey stops being worth it.
// speedPerTick comes from whoever installs this task: a player-directed hunt walks at the
// directed speed like every other order, while the autonomous idle AI passes GatherTask's
// unhurried pace - the same asymmetry every other directed order already has against idling.
public sealed class HuntTask(Animal prey, float range, float speedPerTick) : CreatureTask
{
    public Animal Prey { get; } = prey;

    public float Range { get; } = range;

    // Read-only: exposed only so TargetActionsTests can assert a directed hunt carries the
    // directed speed rather than the idle one.
    public float SpeedPerTick { get; } = speedPerTick;

    // Ticks before which the simulation won't throw again - an attempt costs time, same as a
    // workbench attempt does. 0 until the first attempt sets it.
    public long NextAttemptTick { get; set; }

    public override bool IsComplete => false;

    public override void Advance(Creature creature)
    {
        if (WorldState.Distance(creature.Position, Prey.Position) <= Range)
        {
            return;
        }

        // A fresh MoveTask every tick: its destination is fixed at construction, the prey moves.
        new MoveTask(Prey.Position, SpeedPerTick).Advance(creature);
    }
}
