using ManyWinters.Core.Commands;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// The band has a home too: a Person with a HomeRange wanders around it and its idle food search
// is bounded from it, exactly as an Animal's already was - no new logic in WorldState, only a
// Home to give a Person.
public class PersonHomeRangeTests
{
    // Hunger accrual is switched off here: these tests are about wandering and search bounds,
    // not survival, and a real hunger clock would kill the lone, unfed person under test long
    // before hundreds of idle ticks have played out.
    private static WorldState NewWorldWithoutHunger() =>
        new(TestCatalogs.CreateConfiguration() with { Rules = SimulationRules.Default with { HungerPerTick = 0f } });

    [Fact]
    public void APersonWithAHomeReturnsWithinItsRadiusAfterADirectedTripEndsOutsideIt()
    {
        var world = NewWorldWithoutHunger();
        var home = new HomeRange(new Position(0, 0)) { Radius = 8f, DriftMetresPerSeason = 0f };
        world.AddHomeRange(home);
        var person = world.SpawnPerson("Ava", home.Anchor, initialAgeTicks: TestCatalogs.AdultAgeTicks, home: home);

        // Sent 30 m out - well outside the home's radius - and given time to arrive.
        world.Execute(new MoveCommand(person, new Position(30, 0)));
        world.Advance(35);

        Assert.True(
            WorldState.Distance(home.Anchor, person.Position) > home.Radius,
            "The directed trip did not actually leave the home radius.");

        // Idle resumes once the order completes; plenty of ticks for it to draw them back.
        world.Advance(500);

        Assert.True(
            WorldState.Distance(home.Anchor, person.Position) <= home.Radius + 0.01f,
            $"Ended {WorldState.Distance(home.Anchor, person.Position):0.0} m from the anchor, outside the {home.Radius} m radius.");
    }

    [Fact]
    public void APersonWithNoHomeWandersFromWhereverTheyStandInsteadOfAnAnchor()
    {
        var world = NewWorldWithoutHunger();
        var start = new Position(100, 100);
        var person = world.SpawnPerson("Ava", start, initialAgeTicks: TestCatalogs.AdultAgeTicks);

        world.Advance(500);

        Assert.Null(person.Home);
        // IdleTask's default band (3..8 m); this only confirms WorldState still hands a homeless
        // person a no-home IdleTask.
        Assert.True(WorldState.Distance(start, person.Position) <= 8f + 0.01f);
    }

    // The fallback food search bounds its search by IdleSearchRadius from the creature's home
    // anchor, not from wherever the creature is standing - built explicitly here: a tree much
    // nearer to the person than the camp is, but far enough from camp to fall outside
    // IdleSearchRadius, must be ignored in favour of one further from the person but still
    // within IdleSearchRadius of camp.
    [Fact]
    public void AnIdleSearchNeverReachesATreeFartherThanIdleSearchRadiusFromCampEvenIfNearerToThePersonThemself()
    {
        var world = NewWorldWithoutHunger();
        var idleSearchRadius = world.Configuration.Rules.IdleSearchRadius;
        var home = new HomeRange(new Position(0, 0)) { Radius = 8f, DriftMetresPerSeason = 0f };
        world.AddHomeRange(home);

        // Standing well outside the home's radius but still within IdleSearchRadius of camp.
        var person = world.SpawnPerson("Ava", new Position(65, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks, home: home);
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);

        // Just 3 m from the person, but beyond IdleSearchRadius from camp - must be ignored.
        var tooFarFromCamp = world.SpawnResourceNode(TestCatalogs.Wood, new Position(68, 0), amount: 200f);
        Assert.True(WorldState.Distance(home.Anchor, tooFarFromCamp.Position) > idleSearchRadius);

        // Far from the person, but within IdleSearchRadius of camp - the only valid target.
        var withinRangeOfCamp = world.SpawnResourceNode(TestCatalogs.Wood, new Position(10, 0), amount: 200f);
        Assert.True(WorldState.Distance(home.Anchor, withinRangeOfCamp.Position) <= idleSearchRadius);

        world.Advance(1);

        var gatherTask = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Same(withinRangeOfCamp, gatherTask.Target);
    }
}
