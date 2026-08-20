using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

public sealed record MoveCommand(PersonId PersonId, Position Destination) : ICommand
{
    private const float SpeedPerTick = 1f;

    public void Execute(WorldState world)
    {
        var person = world.People.FirstOrDefault(p => p.Id == PersonId && p.IsAlive);
        if (person is null)
        {
            return;
        }

        person.Tasks.Interrupt(new MoveTask(Destination, SpeedPerTick));
    }
}
