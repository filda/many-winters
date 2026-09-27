using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// "Close in on this deer": walks toward the *moving* prey with a fresh MoveTask every tick, the
// same pattern FollowTask uses for a target that does not sit still (docs/todo/fauna-plan.md,
// phase 3). Only walks; the throw itself is WorldState.Advance's call, since CreatureTask.Advance
// sees only the Creature and a throw costs time (NextAttemptTick) the way a workbench attempt
// does (SimulationRules.TicksPerWorkAttempt). Never completes on its own - the loop in
// WorldState.ShouldReconsiderIdleTask decides when hunting this prey stops being worth it.
public sealed class HuntTask(Animal prey, float range) : CreatureTask
{
    // Mirrors GatherTask's own walking speed - closing in on prey is not a purposeful sprint any
    // more than gathering from a resource is.
    private const float SpeedPerTick = 0.3f;

    public Animal Prey { get; } = prey;

    public float Range { get; } = range;

    // Ticks (WorldState.Clock.CurrentTick) before which WorldState.Advance won't throw again -
    // an attempt costs time, same as SimulationRules.TicksPerWorkAttempt does at the workbench.
    // 0 until the first attempt sets it.
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
