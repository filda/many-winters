using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class SpawnAnimalCommandTests
{
    private static HomeRange NewHome(Position anchor) => new(anchor) { Radius = 15f, DriftMetresPerSeason = 20f };

    [Fact]
    public void GrantsEveryInnateTechniqueTheSpeciesDescribes()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var home = NewHome(new Position(0, 0));

        world.Execute(new SpawnAnimalCommand(CreatureId.New(), TestCatalogs.DeerSpeciesId, new Position(0, 0), home, Sex.Female, world.Clock.CurrentTick));

        var deer = Assert.Single(world.Animals);
        Assert.Contains(TestCatalogs.BasicEating, deer.KnownTechniques);
        Assert.Contains(TestCatalogs.BasicForaging, deer.KnownTechniques);
    }

    [Fact]
    public void SetsSpeciesHomeAndMother()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var home = NewHome(new Position(3, 4));
        world.Execute(new SpawnAnimalCommand(CreatureId.New(), TestCatalogs.DeerSpeciesId, new Position(3, 4), home, Sex.Female, world.Clock.CurrentTick));
        var mother = world.Animals[0];

        world.Execute(new SpawnAnimalCommand(CreatureId.New(), TestCatalogs.DeerSpeciesId, new Position(3, 4), home, Sex.Male, world.Clock.CurrentTick, mother));
        var fawn = world.Animals[1];

        Assert.Equal(TestCatalogs.DeerSpeciesId, fawn.Species);
        Assert.Same(home, fawn.Home);
        Assert.Same(mother, fawn.Mother);
        Assert.Same(mother, fawn.NursingMother);
    }

    [Fact]
    public void AnAdultSpawnedWithNoMotherHasNone()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var home = NewHome(new Position(0, 0));

        world.Execute(new SpawnAnimalCommand(CreatureId.New(), TestCatalogs.DeerSpeciesId, new Position(0, 0), home, Sex.Female, world.Clock.CurrentTick));

        var deer = Assert.Single(world.Animals);
        Assert.Null(deer.Mother);
        Assert.Null(deer.NursingMother);
    }

    [Fact]
    public void RaisesAnimalAdded()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var home = NewHome(new Position(0, 0));
        Animal? raised = null;
        world.AnimalAdded += a => raised = a;

        world.Execute(new SpawnAnimalCommand(CreatureId.New(), TestCatalogs.DeerSpeciesId, new Position(0, 0), home, Sex.Female, world.Clock.CurrentTick));

        Assert.Same(world.Animals[0], raised);
    }
}
