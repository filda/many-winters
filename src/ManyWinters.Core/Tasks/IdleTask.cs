using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// A small aimless walk, one leg at a time via an internal MoveTask. Never completes; a real
// order replaces it.
//
// Without a home (every Person today): anchored wherever the creature happened to be standing on
// the first Advance, radius drawn once from its own id. With one (every Animal): anchored on the
// home range's own drifting Anchor, re-read every leg so the wander follows it, and radius is the
// home's own.
public sealed class IdleTask(
    HomeRange? home,
    float minWanderRadius,
    float maxWanderRadius,
    float speedPerTick,
    int minPauseTicks,
    int maxPauseTicks
    ) : CreatureTask
{
    // Seeded from the person, so a wander path is reproducible from a start tick regardless of
    // simulation order.
    private Random? _rng;

    // Only ever set (and read) when home is null - a homed creature's anchor is home.Anchor
    // itself, read fresh every leg rather than cached here.
    private Position? _anchor;
    private float _wanderRadius;
    private MoveTask? _currentLeg;
    private int _pauseTicksRemaining;

    public override bool IsComplete => false;

    public override void Advance(Creature creature)
    {
        if (_rng is null)
        {
            _rng = new Random(SeedFor(creature.Id.Seed));
            if (home is null)
            {
                _anchor = creature.Position;
                // Drawn once per person, not per leg: how far this one tends to roam.
                _wanderRadius = minWanderRadius + ((float)_rng.NextDouble() * (maxWanderRadius - minWanderRadius));
            }
            else
            {
                _wanderRadius = home.Radius;
            }

            _pauseTicksRemaining = NextPauseTicks();
        }

        if (_currentLeg is null)
        {
            if (_pauseTicksRemaining > 0)
            {
                _pauseTicksRemaining--;
                return;
            }

            _currentLeg = new MoveTask(NextWanderDestination(home?.Anchor ?? _anchor!.Value), speedPerTick);
        }

        _currentLeg.Advance(creature);
        if (_currentLeg.IsComplete)
        {
            _currentLeg = null;
            _pauseTicksRemaining = NextPauseTicks();
        }
    }

    private int NextPauseTicks() => minPauseTicks + _rng!.Next(maxPauseTicks - minPauseTicks + 1);

    // Uniform over the disk's area: independent uniform angle and radius would bunch samples near
    // the anchor.
    private Position NextWanderDestination(Position anchor)
    {
        var angle = _rng!.NextDouble() * Math.Tau;
        var distance = _wanderRadius * Math.Sqrt(_rng.NextDouble());
        return new Position(anchor.X + (distance * Math.Cos(angle)), anchor.Y + (distance * Math.Sin(angle)));
    }

    // Close id seeds would otherwise land their first draws close together, reading as
    // synchronized wandering.
    private static int SeedFor(int personSeed) => SeedHash.Avalanche(unchecked((uint)personSeed));
}
