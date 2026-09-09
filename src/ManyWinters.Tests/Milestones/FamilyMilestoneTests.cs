using ManyWinters.Core.Commands;
using ManyWinters.Core.Maps;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Milestones;

/// <summary>
/// The loop reproduction was built for, end to end and with nobody steering it: a band left
/// together long enough grows on its own, and what the children born into it end up knowing
/// comes from the people around them rather than from their parents' blood.
///
/// Deliberately a whole-band run rather than a unit test of any one rule (those live in
/// <see cref="ManyWinters.Tests.World.WorldStateAffectionTests"/> and
/// <see cref="ManyWinters.Tests.Commands.BirthCommandTests"/>) - the numbers in
/// SimulationRules only mean anything together, and it is their combination that decides
/// whether a band actually survives itself.
/// </summary>
public class FamilyMilestoneTests
{
    private const int TicksInAYear = 300;

    // Spring through autumn: SimulationRules.TicksPerSeason is 75, and winter is the fourth.
    private const int TicksBeforeTheFirstWinter = 225;

    private static Person SpawnAdult(WorldState world, string name, Sex sex, Position position)
    {
        var person = world.SpawnPerson(
            name,
            position,
            initialAgeTicks: world.Configuration.Rules.TicksPerYear * LifeStages.AdultAgeYears,
            sex: sex);

        // Foraging and eating both have to be learned (see SkillDefinition.BaseTechnique);
        // granted directly, because this test is about what happens to the band over a year,
        // not about how its founders came by what they know.
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

            // Same shortcut SurvivalMilestoneTests takes: the founders are put back at the
            // food and fed, because this test is about the family loop rather than about
            // whether anyone can walk to a tree. Nursing mothers need it more than most - see
            // SimulationRules.NursingHungerMultiplier.
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

    // The band above is held together by the test itself. This one is the shipped world doing
    // its own thing: whether the starting camp actually keeps people close enough, for long
    // enough, while they wander off to forage, for the numbers in SimulationRules to ever come
    // to anything. Set the affection radius to arm's reach and this is the test that goes red
    // while every other one stays green - the feature real in Core and invisible in the game.
    //
    // Two things are handed to the band that the shipped game expects the player to give it.
    // Eating and foraging, because nobody is born knowing either (see
    // SkillDefinition.BaseTechnique) and a band that cannot eat starves inside a hundred ticks
    // with or without any of this. And two thirds of a year rather than a whole one, because
    // the first winter kills a band with no warm clothing (see WinterSurvivalMilestoneTests) -
    // surviving it is preparation, which is a different story from this one.
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

    // The point of a child having to stay at its mother's side: it is born knowing nothing,
    // and being close to somebody who knows things is the only way that ever changes.
    [Fact]
    public void AChildBornIntoTheBandPicksUpWhatThePeopleAroundItKnow()
    {
        var world = TestCatalogs.CreateWorld();
        var camp = new Position(0, 0);
        var mother = SpawnAdult(world, "Sela", Sex.Female, camp);
        var father = SpawnAdult(world, "Doran", Sex.Male, camp);

        // Somebody in the band has to know how to teach at all, or nothing can ever spread -
        // see WorldState.AutoTeachNearbyPeople.
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
