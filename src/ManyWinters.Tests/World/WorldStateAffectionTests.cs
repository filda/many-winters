using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// What Advance does with the bonds between people: grows them, lets them fade, and eventually
// turns a strong enough one into a child without the player asking.
public class WorldStateAffectionTests
{
    private static long AdultAgeTicks(WorldState world) => world.Configuration.Rules.TicksPerYear * LifeStages.AdultAgeYears;

    private static Person SpawnAdult(WorldState world, string name, Position position, Sex sex) =>
        world.SpawnPerson(name, position, initialAgeTicks: AdultAgeTicks(world), sex: sex);

    // Age is read off the clock, which Advance moves before its loop runs, so a multi-tick call
    // would age everyone to the end of it on the very first tick. The game steps one at a time.
    private static void AdvanceTickByTick(WorldState world, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Advance(1);
        }
    }

    [Fact]
    public void StandingTogetherGrowsABond()
    {
        var world = TestCatalogs.CreateWorld();
        var ava = SpawnAdult(world, "Ava", new Position(0, 0), Sex.Female);
        var bran = SpawnAdult(world, "Bran", new Position(1, 0), Sex.Male);

        world.Advance(1);

        Assert.Equal(world.Configuration.Rules.AffectionGainedPerTickTogether, world.Affections.Between(ava.Id, bran.Id));
    }

    [Fact]
    public void ABondGrowsWithEveryTickSpentTogether()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var ava = SpawnAdult(world, "Ava", new Position(0, 0), Sex.Female);
        var bran = SpawnAdult(world, "Bran", new Position(1, 0), Sex.Male);

        AdvanceTickByTick(world, 4);

        Assert.Equal(rules.AffectionGainedPerTickTogether * 4, world.Affections.Between(ava.Id, bran.Id), precision: 3);
    }

    [Fact]
    public void PeopleTooFarApartFormNoBondAtAll()
    {
        var world = TestCatalogs.CreateWorld();
        var ava = SpawnAdult(world, "Ava", new Position(0, 0), Sex.Female);
        var bran = SpawnAdult(world, "Bran", new Position(500, 0), Sex.Male);

        AdvanceTickByTick(world, 10);

        Assert.Equal(0f, world.Affections.Between(ava.Id, bran.Id));
        Assert.Empty(world.Affections.All);
    }

    [Fact]
    public void ABondFadesOnceTheTwoAreApart()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var ava = SpawnAdult(world, "Ava", new Position(0, 0), Sex.Female);
        var bran = SpawnAdult(world, "Bran", new Position(1, 0), Sex.Male);
        world.Affections.Set(ava.Id, bran.Id, 40f);
        bran.Position = new Position(500, 0);

        world.Advance(1);

        Assert.Equal(40f - rules.AffectionLostPerTickApart, world.Affections.Between(ava.Id, bran.Id), precision: 3);
    }

    [Fact]
    public void ABondNeverGrowsPastItsCeiling()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var ava = SpawnAdult(world, "Ava", new Position(0, 0), Sex.Female);
        var bran = SpawnAdult(world, "Bran", new Position(1, 0), Sex.Male);
        world.Affections.Set(ava.Id, bran.Id, rules.MaxAffection);

        AdvanceTickByTick(world, 5);

        Assert.Equal(rules.MaxAffection, world.Affections.Between(ava.Id, bran.Id));
    }

    // What someone meant to the people around them outlives them - it is the raw material for
    // anything the game later wants to say about a life that has ended.
    [Fact]
    public void ABondWithSomeoneWhoHasDiedNeitherGrowsNorFades()
    {
        var world = TestCatalogs.CreateWorld();
        var ava = SpawnAdult(world, "Ava", new Position(0, 0), Sex.Female);
        var bran = SpawnAdult(world, "Bran", new Position(1, 0), Sex.Male);
        world.Affections.Set(ava.Id, bran.Id, 40f);
        bran.IsAlive = false;

        AdvanceTickByTick(world, 10);

        Assert.Equal(40f, world.Affections.Between(ava.Id, bran.Id));
    }

    [Fact]
    public void ANewbornStartsOutCloseToBothItsParents()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = SpawnAdult(world, "Sela", new Position(0, 0), Sex.Female);
        var father = SpawnAdult(world, "Doran", new Position(1, 0), Sex.Male);

        world.Execute(new BirthCommand("Bran", mother, father));

        var child = world.People[^1];
        Assert.Equal(rules.StartingAffectionWithParents, world.Affections.Between(child.Id, mother.Id));
        Assert.Equal(rules.StartingAffectionWithParents, world.Affections.Between(child.Id, father.Id));
    }

    [Fact]
    public void TwoPeopleFondEnoughOfEachOtherHaveAChildWithoutBeingTold()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0), Sex.Female);
        var father = SpawnAdult(world, "Doran", new Position(1, 0), Sex.Male);
        world.Affections.Set(mother.Id, father.Id, world.Configuration.Rules.AffectionNeededToHaveAChild);

        world.Advance(1);

        var child = Assert.Single(world.People, p => p != mother && p != father);
        Assert.Same(mother, child.Mother);
        Assert.Same(father, child.Father);
    }

    [Fact]
    public void ABondShortOfTheThresholdIsNotEnough()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = SpawnAdult(world, "Sela", new Position(0, 0), Sex.Female);
        var father = SpawnAdult(world, "Doran", new Position(1, 0), Sex.Male);
        world.Affections.Set(mother.Id, father.Id, rules.AffectionNeededToHaveAChild - rules.AffectionGainedPerTickTogether - 0.1f);

        world.Advance(1);

        Assert.Equal(2, world.People.Count);
    }

    [Fact]
    public void TwoPeopleOfTheSameSexNeverHaveOne()
    {
        var world = TestCatalogs.CreateWorld();
        var first = SpawnAdult(world, "Sela", new Position(0, 0), Sex.Female);
        var second = SpawnAdult(world, "Tora", new Position(1, 0), Sex.Female);
        world.Affections.Set(first.Id, second.Id, world.Configuration.Rules.MaxAffection);

        AdvanceTickByTick(world, 5);

        Assert.Equal(2, world.People.Count);
    }

    // The reason a newborn's bond with its parents starts high and the reproduction threshold
    // sits higher still - closeness to family must never be the kind that makes more family.
    [Fact]
    public void CloseKinNeverHaveAChildNoMatterHowFondTheyAre()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0), Sex.Female);
        var son = world.SpawnPerson("Bran", new Position(1, 0), initialAgeTicks: AdultAgeTicks(world), mother: mother, sex: Sex.Male);
        world.Affections.Set(mother.Id, son.Id, world.Configuration.Rules.MaxAffection);

        AdvanceTickByTick(world, 5);

        Assert.Equal(2, world.People.Count);
    }

    [Fact]
    public void SiblingsNeverHaveAChild()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0), Sex.Female);
        var daughter = world.SpawnPerson("Ava", new Position(1, 0), initialAgeTicks: AdultAgeTicks(world), mother: mother, sex: Sex.Female);
        var son = world.SpawnPerson("Bran", new Position(1, 0), initialAgeTicks: AdultAgeTicks(world), mother: mother, sex: Sex.Male);
        world.Affections.Set(daughter.Id, son.Id, world.Configuration.Rules.MaxAffection);

        AdvanceTickByTick(world, 5);

        Assert.Equal(3, world.People.Count);
    }

    [Fact]
    public void PeopleTooYoungNeverHaveOne()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var girl = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: rules.TicksPerYear, sex: Sex.Female);
        var boy = world.SpawnPerson("Bran", new Position(1, 0), initialAgeTicks: rules.TicksPerYear, sex: Sex.Male);
        world.Affections.Set(girl.Id, boy.Id, rules.MaxAffection);

        AdvanceTickByTick(world, 5);

        Assert.Equal(2, world.People.Count);
    }

    // The nursing gate is what keeps a devoted couple from producing a child every single tick
    // - the second one has to wait until the first is weaned (see BirthCommand).
    [Fact]
    public void ADevotedCoupleHaveOneChildAtATimeRatherThanOnePerTick()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0), Sex.Female);
        var father = SpawnAdult(world, "Doran", new Position(1, 0), Sex.Male);
        world.Affections.Set(mother.Id, father.Id, world.Configuration.Rules.MaxAffection);

        AdvanceTickByTick(world, 20);

        Assert.Equal(3, world.People.Count);
    }

    [Fact]
    public void ANewbornIsNotItselfAConsideredParentOnTheTickItIsBorn()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0), Sex.Female);
        var father = SpawnAdult(world, "Doran", new Position(1, 0), Sex.Male);
        world.Affections.Set(mother.Id, father.Id, world.Configuration.Rules.MaxAffection);

        world.Advance(1);

        var child = world.People[^1];
        Assert.Equal(LifeStage.Infant, world.LifeStageOf(child));
        Assert.Equal(3, world.People.Count);
    }
}
