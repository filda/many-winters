using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

public sealed class MoveTask : PersonTask
{
    private readonly float _speedPerTick;
    private bool _arrived;

    public MoveTask(Position destination, float speedPerTick)
    {
        Destination = destination;
        _speedPerTick = speedPerTick;
    }

    public Position Destination { get; }

    public override bool IsComplete => _arrived;

    public override void Advance(Person person)
    {
        if (_arrived)
        {
            return;
        }

        var dx = Destination.X - person.Position.X;
        var dy = Destination.Y - person.Position.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));

        if (distance <= _speedPerTick)
        {
            person.Position = Destination;
            _arrived = true;
            return;
        }

        var ratio = _speedPerTick / distance;
        person.Position = new Position(person.Position.X + (dx * ratio), person.Position.Y + (dy * ratio));
    }
}
