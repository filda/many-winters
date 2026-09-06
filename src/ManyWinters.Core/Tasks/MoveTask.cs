using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

public sealed class MoveTask(Position destination, float speedPerTick) : PersonTask
{
    private bool _arrived;

    public Position Destination { get; } = destination;

    public override bool IsComplete => _arrived;

    public override void Advance(Person person)
    {
        if (_arrived)
        {
            return;
        }

        var dx = Destination.X - person.Position.X;
        var dy = Destination.Y - person.Position.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        if (distance <= speedPerTick)
        {
            person.Position = Destination;
            _arrived = true;
            return;
        }

        var ratio = speedPerTick / distance;
        person.Position = new Position(person.Position.X + (dx * ratio), person.Position.Y + (dy * ratio));
    }
}
