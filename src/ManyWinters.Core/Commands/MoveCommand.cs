using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record MoveCommand(Person Person, Position Destination) : ICommand
{
    // The speed every player-directed walk uses - a purposeful trip, not the idle AI's own
    // unhurried pace (GatherTask.SpeedPerTick). Public: TargetActions builds HuntTask/ButcherTask
    // with this same number rather than a copy of it (docs/todo/fauna-plan.md, phase 3,
    // "rozhodnuto 2026-09-27").
    public const float SpeedPerTick = 1f;

    public ActionBlocker Blocker(WorldState world) =>
        Person.IsAlive ? ActionBlocker.None : ActionBlocker.ActorIsDead;

    public void Execute(WorldState world)
    {
        if (Blocker(world) is not ActionBlocker.None)
        {
            return;
        }

        Person.Tasks.Interrupt(new MoveTask(Destination, SpeedPerTick));
    }
}
