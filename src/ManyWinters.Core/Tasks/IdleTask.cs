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
public sealed class IdleTask(HomeRange? home = null) : CreatureTask
{
    private const float MinWanderRadius = 3f;
    private const float MaxWanderRadius = 8f;
    private const float SpeedPerTick = 0.15f;

    // A pause between wander legs (and before the first), or idle reads as restless constant
    // walking. The ceiling is public because startup runs the world that long before the player
    // sees it, so the band is already on the move.
    private const int MinPauseTicks = 3;
    public const int MaxPauseTicks = 10;

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
                _wanderRadius = MinWanderRadius + ((float)_rng.NextDouble() * (MaxWanderRadius - MinWanderRadius));
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

            _currentLeg = new MoveTask(NextWanderDestination(home?.Anchor ?? _anchor!.Value), SpeedPerTick);
        }

        _currentLeg.Advance(creature);
        if (_currentLeg.IsComplete)
        {
            _currentLeg = null;
            _pauseTicksRemaining = NextPauseTicks();
        }
    }

    private int NextPauseTicks() => MinPauseTicks + _rng!.Next(MaxPauseTicks - MinPauseTicks + 1);

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
