using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Continuity;

// The facts an epitaph is written from (see Epitaph): what ended, when, who died last, who is
// left, what lies in the ground. Computed from the world's people and graves, never stored
// (see docs/chronicles-and-memory-architecture.md).
public sealed record BandEnding
{
    // When a later band arrives into a world an earlier one died in (docs/todo/todo.md,
    // "Another band comes"), this becomes that band's own arrival tick.
    private const long FoundingTick = 0;

    // Never Living: Of returns null for a band whose line can still go on.
    public required BandFate Fate { get; init; }

    public required string BandName { get; init; }

    // The last man for SpearSideEnded, the last woman for SpindleSideEnded, the last of everyone
    // for Ended. Null only when nobody of that sex ever belonged to the band.
    public required Person? LastToDie { get; init; }

    public required long EndingTick { get; init; }

    public required Season SeasonOfEnding { get; init; }

    // Winters that began between arrival and ending, including the one the band died in.
    public required int WintersSeen { get; init; }

    public required int Survivors { get; init; }

    // Children born into the band, not the people it arrived with.
    public required int Born { get; init; }

    public required int Graves { get; init; }

    public required int MarkedGraves { get; init; }

    // Dead who lie where they fell. At least one when the band has Ended: nobody buried the last.
    public required int Unburied { get; init; }

    // Which line closed first. Null unless the band has Ended, and null when both lines closed
    // the same tick.
    public required BandFate? SideThatEndedFirst { get; init; }

    // Winters that began after the first line closed and before the band ended.
    public required int WintersKeptAfterwards { get; init; }

    public static BandFate FateOf(IEnumerable<Person> people)
    {
        var living = people.Where(person => person.IsAlive).ToList();
        var menLive = living.Any(person => person.Sex == Sex.Male);
        var womenLive = living.Any(person => person.Sex == Sex.Female);

        if (menLive && womenLive)
        {
            return BandFate.Living;
        }

        if (womenLive)
        {
            return BandFate.SpearSideEnded;
        }

        return menLive ? BandFate.SpindleSideEnded : BandFate.Ended;
    }

    public static BandEnding? Of(WorldState world)
    {
        var people = world.People;
        if (people.Count == 0)
        {
            throw new ArgumentException("A world with nobody in it has no band to speak of.", nameof(world));
        }

        var fate = FateOf(people);
        if (fate == BandFate.Living)
        {
            return null;
        }

        var rules = world.Configuration.Rules;
        var lastMan = LastToDieAmong(people.Where(person => person.Sex == Sex.Male));
        var lastWoman = LastToDieAmong(people.Where(person => person.Sex == Sex.Female));
        var lastToDie = fate switch
        {
            BandFate.SpearSideEnded => lastMan,
            BandFate.SpindleSideEnded => lastWoman,
            _ => LastToDieAmong(people),
        };

        var endingTick = DeathTickOf(lastToDie) ?? world.Clock.CurrentTick;

        // A sex nobody in the band ever had was closed from the day the band arrived.
        var spearSideClosedAt = DeathTickOf(lastMan) ?? FoundingTick;
        var spindleSideClosedAt = DeathTickOf(lastWoman) ?? FoundingTick;
        BandFate? sideThatEndedFirst = null;
        var wintersKeptAfterwards = 0;
        if (fate == BandFate.Ended && spearSideClosedAt != spindleSideClosedAt)
        {
            // Stryker disable once Equality: the equal case is ruled out just above, so < and <= agree
            var spearFirst = spearSideClosedAt < spindleSideClosedAt;
            sideThatEndedFirst = spearFirst ? BandFate.SpearSideEnded : BandFate.SpindleSideEnded;
            wintersKeptAfterwards = WintersBegunWithin(rules, Math.Min(spearSideClosedAt, spindleSideClosedAt) + 1, endingTick);
        }

        return new BandEnding
        {
            Fate = fate,
            BandName = Continuity.BandName.Of(people),
            LastToDie = lastToDie,
            EndingTick = endingTick,
            SeasonOfEnding = rules.SeasonAt(endingTick),
            WintersSeen = WintersBegunWithin(rules, FoundingTick, endingTick),
            Survivors = people.Count(person => person.IsAlive),
            Born = people.Count(person => person.BirthTick > FoundingTick),
            Graves = world.Graves.Count,
            MarkedGraves = world.Graves.Count(grave => grave.IsMarked),
            Unburied = people.Count(person => !person.IsAlive && !person.IsBuried),
            SideThatEndedFirst = sideThatEndedFirst,
            WintersKeptAfterwards = wintersKeptAfterwards,
        };
    }

    // The most recent death, or null when nobody is dead. Same-tick deaths are ordered by name
    // then id, as BandName does, so the answer does not depend on list order.
    // Stryker disable Linq: which of two same-named ids wins the tie is arbitrary; only that the
    // same one wins from either list order matters
    private static Person? LastToDieAmong(IEnumerable<Person> people) =>
        people
            .Where(person => !person.IsAlive)
            .OrderByDescending(person => person.DeathTick ?? long.MaxValue)
            .ThenBy(person => person.Name, StringComparer.Ordinal)
            .ThenBy(person => person.Id.Value)
            .FirstOrDefault();

    // Stryker restore Linq

    private static long? DeathTickOf(Person? person) => person?.DeathTick;

    // Winters that began in the closed tick range [from, to]; seasons start at multiples of
    // TicksPerSeason. A loop rather than a closed form: a band lives a few hundred seasons at most.
    private static int WintersBegunWithin(SimulationRules rules, long from, long to)
    {
        var winters = 0;
        var firstSeasonStart = (from + rules.TicksPerSeason - 1) / rules.TicksPerSeason * rules.TicksPerSeason;
        for (var start = firstSeasonStart; start <= to; start += rules.TicksPerSeason)
        {
            if (rules.SeasonAt(start) == Season.Winter)
            {
                winters++;
            }
        }

        return winters;
    }
}
