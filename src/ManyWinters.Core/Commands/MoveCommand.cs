using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record MoveCommand(Person Person, Position Destination) : ICommand
{
    private const float SpeedPerTick = 1f;

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
