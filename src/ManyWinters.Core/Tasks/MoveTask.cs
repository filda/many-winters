using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

public sealed class MoveTask(Position destination, float speedPerTick) : CreatureTask
{
    private bool _arrived;

    public Position Destination { get; } = destination;

    public override bool IsComplete => _arrived;

    public override void Advance(Creature creature)
    {
        if (_arrived)
        {
            return;
        }

        var dx = Destination.X - creature.Position.X;
        var dy = Destination.Y - creature.Position.Y;
        var distance = Math.Sqrt((dx * dx) + (dy * dy));

        if (distance <= speedPerTick)
        {
            creature.Position = Destination;
            _arrived = true;
            return;
        }

        var ratio = speedPerTick / distance;
        creature.Position = new Position(creature.Position.X + (dx * ratio), creature.Position.Y + (dy * ratio));
    }
}
