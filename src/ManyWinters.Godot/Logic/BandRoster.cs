using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// One line of the roster: who somebody is, what they are doing, and how full their belly is. The
// Person comes along so the panel can hand them straight back when the line is pressed, the way
// PersonView does on a click - nothing is looked up between "pressed" and "selected".
//
// The heading carries the age in brackets after the name, because a roster is read to tell people
// apart and a name alone does not - a bare count of winters, not DurationText's prose, so a name
// and what that person is doing fit on one line. Winters need no unit in a game called Many
// Winters, and somebody in their first one reads "(0)". Sex is not written: the sprites are to say
// that (see docs/todo/todo.md).
internal readonly record struct RosterEntry(Person Person, string Heading, string Task, MeterReading Fed);

// The band as a list, for the player who has lost track of where everybody went: whose band it is,
// how many there are, and a line each saying who they are, what they are doing and how hungry.
//
// The living only. The dead are found by their graves, and a roster is read to go and look at
// somebody, which is not a thing to do to a corpse.
//
// Ordered by name, so a line stays where the player last saw it between refreshes and the list
// can be scanned for the name they are hunting for. A band is a handful of people today, so there
// is no search and no paging; both belong to the day the map holds several bands.
//
// Engine-free like PersonActions and SelectionCard: what the panel shows is a plain function of
// world state, testable without a running Godot.
internal sealed record BandRoster(string Title, string Summary, IReadOnlyList<RosterEntry> People)
{
    internal static BandRoster Of(WorldState world)
    {
        var living = world.People.Where(person => person.IsAlive).ToList();
        if (living.Count == 0)
        {
            // A band that has just died out. BandArrival.Of refuses to describe one, and
            // "0 people" is not a sentence about anybody.
            return new BandRoster("The band", "Nobody left.", []);
        }

        var band = BandArrival.Of(world);
        var seekFoodThreshold = world.Configuration.Rules.HungerSeekFoodThreshold;

        var entries = living
            .OrderBy(person => person.Name, StringComparer.Ordinal)
            // Two people can share a name - a child may be named after whoever is still alive -
            // and the order has to be settled even then.
            .ThenBy(person => person.Id.Value)
            .Select(person => new RosterEntry(
                person,
                $"{person.Name} ({world.AgeInYears(person)})",
                InspectorText.ForWork(person, world.Configuration.ResourceCatalog),
                SelectionCard.FedFor(person, seekFoodThreshold)))
            .ToList();

        // Named after its eldest, the way the band is named everywhere else (BandName, and the
        // pause panel's title through it) - but "Liska's band", because this is the band as the
        // player commands it, not as a later band reading its graves will know it.
        return new BandRoster(
            $"{band.Eldest.Name}'s band",
            // The same count, broken down the same three ways, as the pause panel's line
            // (PopulationSummary) - the band is one band however the player comes to look at it.
            PopulationSummary.Of(band.People, band.Men, band.Women, band.Children),
            entries);
    }
}
