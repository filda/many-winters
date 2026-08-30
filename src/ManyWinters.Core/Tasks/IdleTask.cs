using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// Idle no longer means standing frozen in place - a small aimless walk near wherever the
// person happened to end up, one leg at a time via an internal MoveTask. Never completes;
// a real order (MoveCommand etc.) replaces it via PersonTaskQueue.Interrupt the moment one
// comes in, same as it would replace any other task.
public sealed class IdleTask : PersonTask
{
    private const float MinWanderRadius = 1.5f;
    private const float MaxWanderRadius = 4f;
    private const float SpeedPerTick = 0.3f;

    // Seeded from the person, not shared/time-based, so a given person's wander path is
    // reproducible from a given start tick rather than depending on simulation order.
    private Random? _rng;
    private Position? _anchor;
    private float _wanderRadius;
    private MoveTask? _currentLeg;

    public override bool IsComplete => false;

    public override void Advance(Person person)
    {
        if (_rng is null)
        {
            _rng = new Random(SeedFor(person.Id.Value));
            _anchor = person.Position;
            // Drawn once per person, not per leg - a personal "how far this one tends to
            // roam" rather than everyone sharing the same perimeter.
            _wanderRadius = MinWanderRadius + ((float)_rng.NextDouble() * (MaxWanderRadius - MinWanderRadius));
        }

        if (_currentLeg is null)
        {
            _currentLeg = new MoveTask(NextWanderDestination(_anchor!.Value), SpeedPerTick);
        }

        _currentLeg.Advance(person);
        if (_currentLeg.IsComplete)
        {
            _currentLeg = null;
        }
    }

    // Uniform over the disk's area, not its bounding square - same math as MapLoader's
    // starting-crowd scatter (sampling angle and radius independently and uniformly would
    // bunch samples near the anchor instead).
    private Position NextWanderDestination(Position anchor)
    {
        var angle = _rng!.NextDouble() * Math.Tau;
        var distance = _wanderRadius * Math.Sqrt(_rng.NextDouble());
        return new Position(anchor.X + (distance * Math.Cos(angle)), anchor.Y + (distance * Math.Sin(angle)));
    }

    // Person ids are small sequential integers (1, 2, 3, ...), and System.Random's legacy
    // algorithm correlates badly on nearby small seeds - everyone's first few draws would
    // land eerily close together, reading as synchronized wandering rather than independent
    // people. This avalanches the seed apart first (Thomas Wang's 32-bit integer hash) while
    // staying deterministic per person.
    private static int SeedFor(int personId)
    {
        var x = unchecked((uint)personId);
        x = unchecked(((x >> 16) ^ x) * 0x45d9f3b);
        x = unchecked(((x >> 16) ^ x) * 0x45d9f3b);
        x = (x >> 16) ^ x;
        return unchecked((int)x);
    }
}
