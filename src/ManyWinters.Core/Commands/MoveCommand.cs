using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record MoveCommand(Person Person, Position Destination) : ICommand
{
    private const float SpeedPerTick = 1f;

    public void Execute(WorldState world)
    {
        if (!Person.IsAlive)
        {
            return;
        }

        Person.Tasks.Interrupt(new MoveTask(Destination, SpeedPerTick));
    }
}
