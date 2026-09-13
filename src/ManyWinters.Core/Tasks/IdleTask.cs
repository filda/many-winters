using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// A small aimless walk near wherever the person ended up, one leg at a time via an internal
// MoveTask. Never completes; a real order replaces it via PersonTaskQueue.Interrupt.
public sealed class IdleTask : PersonTask
{
    private const float MinWanderRadius = 3f;
    private const float MaxWanderRadius = 8f;
    private const float SpeedPerTick = 0.15f;

    // A pause between wander legs (and before the first), or idle reads as restless constant
    // walking. The ceiling is public because Main._Ready runs the world that long before the
    // player sees it, so the band is already on the move.
    private const int MinPauseTicks = 3;
    public const int MaxPauseTicks = 10;

    // Seeded from the person, so a wander path is reproducible from a start tick regardless of
    // simulation order.
    private Random? _rng;
    private Position? _anchor;
    private float _wanderRadius;
    private MoveTask? _currentLeg;
    private int _pauseTicksRemaining;

    public override bool IsComplete => false;

    public override void Advance(Person person)
    {
        if (_rng is null)
        {
            _rng = new Random(SeedFor(person.Id.Seed));
            _anchor = person.Position;
            // Drawn once per person, not per leg: how far this one tends to roam.
            _wanderRadius = MinWanderRadius + ((float)_rng.NextDouble() * (MaxWanderRadius - MinWanderRadius));
            _pauseTicksRemaining = NextPauseTicks();
        }

        if (_currentLeg is null)
        {
            if (_pauseTicksRemaining > 0)
            {
                _pauseTicksRemaining--;
                return;
            }

            _currentLeg = new MoveTask(NextWanderDestination(_anchor!.Value), SpeedPerTick);
        }

        _currentLeg.Advance(person);
        if (_currentLeg.IsComplete)
        {
            _currentLeg = null;
            _pauseTicksRemaining = NextPauseTicks();
        }
    }

    private int NextPauseTicks() => MinPauseTicks + _rng!.Next(MaxPauseTicks - MinPauseTicks + 1);

    // Uniform over the disk's area, as MapLoader's crowd scatter: independent uniform angle and
    // radius would bunch samples near the anchor.
    private Position NextWanderDestination(Position anchor)
    {
        var angle = _rng!.NextDouble() * Math.Tau;
        var distance = _wanderRadius * Math.Sqrt(_rng.NextDouble());
        return new Position(anchor.X + (distance * Math.Cos(angle)), anchor.Y + (distance * Math.Sin(angle)));
    }

    // Close id seeds (EntityId.SeedOf) would otherwise land their first draws close together,
    // reading as synchronized wandering - see SeedHash.
    private static int SeedFor(int personSeed) => SeedHash.Avalanche(unchecked((uint)personSeed));
}
