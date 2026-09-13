using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Continuity;

// The facts an inscription over a band's end is written from (see Epitaph) - what ended, when,
// who died last, who is left, what they left in the ground. A snapshot computed from the
// world's own people and graves, never stored: the record of a band is its graves (see
// docs/chronicles-and-memory-architecture.md), and this only reads them.
public sealed record BandEnding
{
    // The band arrived with the world. When a later band arrives into a world an earlier one
    // died in (the permaworld item in docs/todo/todo.md), this becomes that band's own
    // arrival tick instead of a constant.
    private const long FoundingTick = 0;

    // Never Living: a band whose line can still go on has no ending to write about, and Of
    // says so with null rather than with a record full of blanks.
    public required BandFate Fate { get; init; }

    public required string BandName { get; init; }

    // Whose death ended the line: the last man for SpearSideEnded, the last woman for
    // SpindleSideEnded, the last of everyone for Ended. Null only when nobody of that sex ever
    // belonged to the band, so there was no death to end it - a line that was never open.
    public required Person? LastToDie { get; init; }

    public required long EndingTick { get; init; }

    public required Season SeasonOfEnding { get; init; }

    // Winters that began between the band's arrival and its ending, inclusive of one it died
    // in - "nine winters they saw" counts the one that killed them.
    public required int WintersSeen { get; init; }

    public required int Survivors { get; init; }

    // Children born into the band, as opposed to the people it arrived with.
    public required int Born { get; init; }

    public required int Graves { get; init; }

    public required int MarkedGraves { get; init; }

    // Dead who lie where they fell. At least one whenever the band has Ended: the last to die
    // had nobody left to bury them.
    public required int Unburied { get; init; }

    // For a band that has Ended: which of its two lines closed first, or null when both closed
    // at once (a last couple dying the same tick). Null too while only one of them has.
    public required BandFate? SideThatEndedFirst { get; init; }

    // Winters that began after that first line closed and before the band ended - how long the
    // survivors of one sex kept going with no child to hope for.
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

    // Null while the band is Living - there is nothing to write yet.
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

    // The most recent death among these people, or null when none of them is dead. Two who
    // died the same tick are told apart by name and then id, as BandName does, so the answer
    // does not depend on list order.
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

    // How many winters began in the closed tick range [from, to]: the season that starts at
    // each multiple of TicksPerSeason inside it, counted when it is Winter. A loop over season
    // starts rather than a closed form - a band lives a few hundred seasons at most, and the
    // arithmetic of "which quarter-years fall in here" is easier to get wrong than to run.
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
