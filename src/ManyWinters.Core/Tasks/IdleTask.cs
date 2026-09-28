using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Tasks;

// A small aimless walk, one leg at a time via an internal MoveTask. Never completes; a real
// order replaces it.
//
// Anchored on the home range's own drifting Anchor, re-read every leg so the wander follows it,
// within the home's own radius.
public sealed class IdleTask(
    HomeRange home,
    float speedPerTick,
    int minPauseTicks,
    int maxPauseTicks
    ) : CreatureTask
{
    // Seeded from the creature, so a wander path is reproducible from a start tick regardless of
    // simulation order.
    private Random? _rng;

    private MoveTask? _currentLeg;
    private int _pauseTicksRemaining;

    public override bool IsComplete => false;

    public override void Advance(Creature creature)
    {
        if (_rng is null)
        {
            _rng = new Random(SeedFor(creature.Id.Seed));
            _pauseTicksRemaining = NextPauseTicks();
        }

        if (_currentLeg is null)
        {
            if (_pauseTicksRemaining > 0)
            {
                _pauseTicksRemaining--;
                return;
            }

            _currentLeg = new MoveTask(NextWanderDestination(home.Anchor), speedPerTick);
        }

        _currentLeg.Advance(creature);
        if (_currentLeg.IsComplete)
        {
            _currentLeg = null;
            _pauseTicksRemaining = NextPauseTicks();
        }
    }

    // Close id seeds would otherwise land their first draws close together, reading as
    // synchronized wandering.
    private static int SeedFor(int personSeed) => SeedHash.Avalanche(unchecked((uint)personSeed));

    private int NextPauseTicks() => minPauseTicks + _rng!.Next(maxPauseTicks - minPauseTicks + 1);

    // Uniform over the disk's area: independent uniform angle and radius would bunch samples near
    // the anchor.
    private Position NextWanderDestination(Position anchor)
    {
        var angle = _rng!.NextDouble() * Math.Tau;
        var distance = home.Radius * Math.Sqrt(_rng.NextDouble());
        return new Position(anchor.X + (distance * Math.Cos(angle)), anchor.Y + (distance * Math.Sin(angle)));
    }
}
