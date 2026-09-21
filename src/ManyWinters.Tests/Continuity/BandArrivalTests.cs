using ManyWinters.Core.Continuity;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Continuity;

public class BandArrivalTests
{
    // Default rules: 300 ticks to a year, adulthood at age 4.
    private const long Year = 300;

    private static WorldState World => TestCatalogs.CreateWorld();

    [Fact]
    public void ABandIsCountedByGrownMenWomenAndChildren()
    {
        var world = World;
        world.SpawnPerson("Liska", new Position(0, 0), initialAgeTicks: 9 * Year, sex: Sex.Female);
        world.SpawnPerson("Bran", new Position(0, 0), initialAgeTicks: 5 * Year, sex: Sex.Male);
        world.SpawnPerson("Doran", new Position(0, 0), initialAgeTicks: 4 * Year, sex: Sex.Male);
        world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: 2 * Year, sex: Sex.Female);
        world.SpawnPerson("Kael", new Position(0, 0), initialAgeTicks: 0, sex: Sex.Male);

        var arrival = BandArrival.Of(world);

        Assert.Equal("Liska's people", arrival.BandName);
        Assert.Equal(Season.Spring, arrival.Season);
        Assert.Equal(0, arrival.ArrivalTick);
        Assert.Equal(5, arrival.People);
        Assert.Equal(2, arrival.Men);
        Assert.Equal(1, arrival.Women);
        Assert.Equal(2, arrival.Children);
        Assert.Equal("Liska", arrival.Eldest.Name);
        Assert.Equal(9, arrival.EldestWinters);
        Assert.False(arrival.KnowsAnything);
    }

    // Someone exactly at the adult age is grown: the boundary is defined elsewhere, and this
    // only has to agree with it.
    [Fact]
    public void SomeoneJustGrownCountsAsAnAdult()
    {
        var world = World;
        world.SpawnPerson("Doran", new Position(0, 0), initialAgeTicks: LifeStages.AdultAgeYears * Year, sex: Sex.Male);
        world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: (LifeStages.AdultAgeYears * Year) - 1, sex: Sex.Female);

        var arrival = BandArrival.Of(world);

        Assert.Equal(1, arrival.Men);
        Assert.Equal(0, arrival.Women);
        Assert.Equal(1, arrival.Children);
    }

    [Fact]
    public void ABandThatKnowsATechniqueKnowsSomething()
    {
        var world = World;
        var sela = world.SpawnPerson("Sela", new Position(0, 0), initialAgeTicks: 5 * Year, sex: Sex.Female);
        world.SpawnPerson("Doran", new Position(0, 0), initialAgeTicks: 5 * Year, sex: Sex.Male);
        sela.KnownTechniques.Add(new TechniqueId("basic_eating"));

        Assert.True(BandArrival.Of(world).KnowsAnything);
    }

    [Fact]
    public void TheDeadDoNotArrive()
    {
        var world = World;
        var old = world.SpawnPerson("Orla", new Position(0, 0), initialAgeTicks: 9 * Year, sex: Sex.Female);
        old.IsAlive = false;
        world.SpawnPerson("Sela", new Position(0, 0), initialAgeTicks: 5 * Year, sex: Sex.Female);

        var arrival = BandArrival.Of(world);

        Assert.Equal("Sela's people", arrival.BandName);
        Assert.Equal(1, arrival.People);
        Assert.Equal("Sela", arrival.Eldest.Name);
    }

    [Fact]
    public void ArrivalIsDatedToTheMomentItIsRead()
    {
        var world = World;
        world.SpawnPerson("Sela", new Position(0, 0), initialAgeTicks: 5 * Year, sex: Sex.Female);
        world.Clock.Advance(250);

        var arrival = BandArrival.Of(world);

        Assert.Equal(250, arrival.ArrivalTick);
        Assert.Equal(Season.Winter, arrival.Season);
    }

    [Fact]
    public void ABandNobodyLivingBelongsToHasNotArrived()
    {
        var world = World;
        var dead = world.SpawnPerson("Sela", new Position(0, 0), sex: Sex.Female);
        dead.IsAlive = false;

        var error = Assert.Throws<ArgumentException>(() => BandArrival.Of(world));

        Assert.Contains("nobody living", error.Message);
    }
}
