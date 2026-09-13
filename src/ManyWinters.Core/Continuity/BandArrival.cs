using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Continuity;

// The facts the inscription over a band's beginning is written from (see Prologue): who came,
// how many, in what season, led in years by whom, and whether they knew anything at all. Read
// off the world the moment the band is there, never stored - like BandEnding, it is a snapshot
// of people, not a record kept beside them.
public sealed record BandArrival
{
    public required string BandName { get; init; }

    public required Season Season { get; init; }

    public required long ArrivalTick { get; init; }

    public required int People { get; init; }

    // Grown men and women (LifeStages.AdultAgeYears and up); everyone younger is a child,
    // whichever sex, so the three always add up to People.
    public required int Men { get; init; }

    public required int Women { get; init; }

    public required int Children { get; init; }

    // The one the band is named after (see BandName), and how many winters they had seen.
    public required Person Eldest { get; init; }

    public required int EldestWinters { get; init; }

    // Whether anyone in the band knows a single technique. The shipped starting band knows
    // none - nobody is born knowing how to eat - and the prologue says so plainly, because
    // teaching them is the game.
    public required bool KnowsAnything { get; init; }

    public static BandArrival Of(WorldState world)
    {
        var living = world.People.Where(person => person.IsAlive).ToList();
        if (living.Count == 0)
        {
            throw new ArgumentException("A band nobody living belongs to has not arrived anywhere.", nameof(world));
        }

        var adults = living.Where(person => world.LifeStageOf(person) is LifeStage.Adult or LifeStage.Elder).ToList();
        var eldest = Continuity.BandName.EldestOf(living);

        return new BandArrival
        {
            BandName = Continuity.BandName.Of(living),
            Season = world.CurrentSeason,
            ArrivalTick = world.Clock.CurrentTick,
            People = living.Count,
            Men = adults.Count(person => person.Sex == Sex.Male),
            Women = adults.Count(person => person.Sex == Sex.Female),
            Children = living.Count - adults.Count,
            Eldest = eldest,
            EldestWinters = (int)world.AgeInYears(eldest),
            KnowsAnything = living.Any(person => person.KnownTechniques.Count > 0),
        };
    }
}
