using ManyWinters.Core.Commands;
using ManyWinters.Core.Maps;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Milestones;

/// <summary>
/// The loop reproduction was built for, run whole-band with nobody steering: a band left
/// together grows on its own, and its children learn from the people around them. The numbers
/// in SimulationRules only mean anything together; the per-rule unit tests live in
/// <see cref="ManyWinters.Tests.World.WorldStateAffectionTests"/> and
/// <see cref="ManyWinters.Tests.Commands.BirthCommandTests"/>.
/// </summary>
public class FamilyMilestoneTests
{
    private const int TicksInAYear = 300;

    // Spring through autumn: seasons are 75 ticks, and winter is the fourth.
    private const int TicksBeforeTheFirstWinter = 225;

    private static Person SpawnAdult(WorldState world, string name, Sex sex, Position position)
    {
        var person = world.SpawnPerson(
            name,
            position,
            initialAgeTicks: world.Configuration.Rules.TicksPerYear * LifeStages.AdultAgeYears,
            sex: sex);

        // Foraging and eating have to be learned; granted directly because this test is about
        // the band's year, not how the founders learned.
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        return person;
    }

    [Fact]
    public void ABandLeftTogetherForAYearGrows()
    {
        var world = TestCatalogs.CreateWorld();
        var camp = new Position(0, 0);
        var founders = new List<Person>
        {
            SpawnAdult(world, "Sela", Sex.Female, camp),
            SpawnAdult(world, "Tora", Sex.Female, camp),
            SpawnAdult(world, "Doran", Sex.Male, camp),
            SpawnAdult(world, "Bran", Sex.Male, camp),
        };

        var node = world.SpawnResourceNode(TestCatalogs.Apple, camp, 1_000_000f);

        for (var tick = 0; tick < TicksInAYear; tick++)
        {
            world.Advance(1);
            if (tick % 10 != 0)
            {
                continue;
            }

            // Founders are put back at the food and fed: the test is about the family loop, not
            // walking to a tree. Nursing mothers need it most.
            foreach (var person in founders)
            {
                person.Position = node.Position;
                world.Execute(new GatherCommand(person, node));
                world.Execute(new EatCommand(person, TestCatalogs.AppleItem));
            }
        }

        var children = world.People.Except(founders).ToList();
        Assert.NotEmpty(children);
        Assert.All(founders, person => Assert.True(person.IsAlive));
        Assert.All(children, child => Assert.True(child.IsAlive));
        Assert.All(children, child => Assert.Contains(child.Mother, founders));
        Assert.All(children, child => Assert.Contains(child.Father, founders));
    }

    // The shipped world on its own: does the starting camp keep people close enough, for long
    // enough, while they forage, for the affection numbers to come to anything? Shrink the
    // affection radius to arm's reach and this is the test that goes red. Eating and foraging
    // are granted because nobody is born knowing them; the run stops before the first winter,
    // which would otherwise kill a band without warm clothing.
    [Fact]
    public void TheShippedStartingBandHasChildrenOfItsOwn()
    {
        var map = MapLoader.LoadDefault(TestCatalogs.CreateConfiguration());
        var world = map.World;
        foreach (var person in world.People)
        {
            person.KnownTechniques.Add(TestCatalogs.BasicEating);
            person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        }

        for (var tick = 0; tick < TicksBeforeTheFirstWinter; tick++)
        {
            world.Advance(1);
        }

        var born = world.People.Where(person => person.BirthTick > 0).ToList();
        Assert.NotEmpty(born);
        Assert.All(born, child => Assert.NotSame(Person.Unknown, child.Mother));
        Assert.All(born, child => Assert.NotSame(Person.Unknown, child.Father));
        Assert.All(born, child => Assert.False(Kinship.AreCloseKin(child.Mother, child.Father)));
    }

    // A child is born knowing nothing; being close to somebody who knows things is the only way
    // that changes.
    [Fact]
    public void AChildBornIntoTheBandPicksUpWhatThePeopleAroundItKnow()
    {
        var world = TestCatalogs.CreateWorld();
        var camp = new Position(0, 0);
        var mother = SpawnAdult(world, "Sela", Sex.Female, camp);
        var father = SpawnAdult(world, "Doran", Sex.Male, camp);

        // Somebody has to know how to teach, or nothing spreads.
        mother.KnownTechniques.Add(TestCatalogs.BasicTeaching);

        world.Execute(new BirthCommand("Ava", mother, father));
        var child = world.People[^1];
        Assert.Empty(child.KnownTechniques);

        var node = world.SpawnResourceNode(TestCatalogs.Apple, camp, 1_000_000f);
        for (var tick = 0; tick < TicksInAYear; tick++)
        {
            world.Advance(1);
            if (tick % 10 != 0)
            {
                continue;
            }

            mother.Position = node.Position;
            world.Execute(new GatherCommand(mother, node));
            world.Execute(new EatCommand(mother, TestCatalogs.AppleItem));
        }

        Assert.True(mother.IsAlive);
        Assert.True(child.IsAlive);
        Assert.NotEmpty(child.KnownTechniques);
    }
}
