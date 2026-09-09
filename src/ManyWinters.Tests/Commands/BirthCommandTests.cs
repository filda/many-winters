using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class BirthCommandTests
{
    private static long AdultAgeTicks(WorldState world) => world.Configuration.Rules.TicksPerYear * LifeStages.AdultAgeYears;

    private static Person SpawnAdult(WorldState world, string name, Position position) =>
        world.SpawnPerson(name, position, initialAgeTicks: AdultAgeTicks(world));

    [Fact]
    public void AddsTheChildToTheWorld()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));
        var father = SpawnAdult(world, "Doran", new Position(1, 0));

        world.Execute(new BirthCommand("Bran", mother, father));

        var child = Assert.Single(world.People, p => p.Name == "Bran");
        Assert.True(child.IsAlive);
        Assert.Same(mother, child.Mother);
        Assert.Same(father, child.Father);
    }

    [Fact]
    public void TheChildIsBornNowSoItStartsAtAgeZero()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));
        var father = SpawnAdult(world, "Doran", new Position(1, 0));
        // The clock alone, not a full Advance: fifty simulated ticks would also have the two
        // of them wander apart, and the birth would then be refused for being out of reach -
        // which is DoesNothingWhenTheParentsAreNotStandingTogether's job, not this one's.
        world.Clock.Advance(50);

        world.Execute(new BirthCommand("Bran", mother, father));

        var child = world.People[^1];
        Assert.Equal(world.Clock.CurrentTick, child.BirthTick);
        Assert.Equal(0, world.AgeInYears(child));
        Assert.Equal(LifeStage.Infant, world.LifeStageOf(child));
    }

    [Fact]
    public void TheChildIsBornWhereItsMotherIs()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(4, 7));
        var father = SpawnAdult(world, "Doran", new Position(5, 7));

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(mother.Position, world.People[^1].Position);
    }

    // Knowledge in this game is taught, never inherited - see the command's own doc comment.
    [Fact]
    public void TheChildInheritsNoTechniquesSkillsOrBelongings()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));
        var father = SpawnAdult(world, "Doran", new Position(1, 0));
        mother.KnownTechniques.Add(TestCatalogs.BasicForaging);
        mother.Skills.Increase(TestCatalogs.Foraging, 5f);
        mother.Inventory.Add(TestCatalogs.WoodItem, 3);
        father.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);

        world.Execute(new BirthCommand("Bran", mother, father));

        var child = world.People[^1];
        Assert.Empty(child.KnownTechniques);
        Assert.Empty(child.Skills.Levels);
        Assert.Empty(child.Inventory.Counts);
    }

    [Fact]
    public void TellsThePresentationLayerAboutTheChild()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));
        var father = SpawnAdult(world, "Doran", new Position(1, 0));
        var announced = new List<Person>();
        world.PersonAdded += announced.Add;

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal("Bran", Assert.Single(announced).Name);
    }

    [Fact]
    public void DoesNothingWhenTheMotherIsDead()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));
        var father = SpawnAdult(world, "Doran", new Position(1, 0));
        mother.IsAlive = false;

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(2, world.People.Count);
    }

    [Fact]
    public void DoesNothingWhenTheFatherIsDead()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));
        var father = SpawnAdult(world, "Doran", new Position(1, 0));
        father.IsAlive = false;

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(2, world.People.Count);
    }

    [Fact]
    public void DoesNothingWhenTheTwoParentsAreTheSamePerson()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));

        world.Execute(new BirthCommand("Bran", mother, mother));

        Assert.Single(world.People);
    }

    [Fact]
    public void DoesNothingWhenTheMotherIsStillAChild()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = world.SpawnPerson("Sela", new Position(0, 0), initialAgeTicks: rules.TicksPerYear * (LifeStages.AdultAgeYears - 1));
        var father = SpawnAdult(world, "Doran", new Position(1, 0));

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(2, world.People.Count);
    }

    [Fact]
    public void DoesNothingWhenTheFatherIsStillAChild()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));
        var father = world.SpawnPerson("Doran", new Position(1, 0), initialAgeTicks: rules.TicksPerYear * (LifeStages.AdultAgeYears - 1));

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(2, world.People.Count);
    }

    // The gate is a floor on childhood, not a fertility model - see
    // WorldState.IsOldEnoughForChildren.
    [Fact]
    public void EldersCanStillHaveChildren()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = world.SpawnPerson("Sela", new Position(0, 0), initialAgeTicks: rules.TicksPerYear * LifeStages.ElderAgeYears);
        var father = world.SpawnPerson("Doran", new Position(1, 0), initialAgeTicks: rules.TicksPerYear * LifeStages.ElderAgeYears);

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(3, world.People.Count);
    }

    [Fact]
    public void DoesNothingWhenTheParentsAreNotStandingTogether()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));
        var father = SpawnAdult(world, "Doran", new Position(50, 0));

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(2, world.People.Count);
    }

    // One at a time: a mother already nursing cannot feed a second newborn.
    [Fact]
    public void DoesNothingWhileTheMotherIsStillNursing()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));
        var father = SpawnAdult(world, "Doran", new Position(1, 0));
        world.Execute(new BirthCommand("Bran", mother, father));

        world.Execute(new BirthCommand("Ivy", mother, father));

        Assert.Equal(3, world.People.Count);
    }

    [Fact]
    public void AllowsAnotherChildOnceTheFirstIsWeaned()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));
        var father = SpawnAdult(world, "Doran", new Position(1, 0));
        world.Execute(new BirthCommand("Bran", mother, father));
        world.Clock.Advance(world.Configuration.Rules.TicksPerYear * LifeStages.WeaningAgeYears);

        world.Execute(new BirthCommand("Ivy", mother, father));

        Assert.Equal(4, world.People.Count);
    }

    // The gate is "can this mother feed someone right now", not "has she ever given birth" - a
    // mother whose infant has ended up out of reach is a mother whose infant is already
    // starving, and BirthCommand is not the place that notices.
    [Fact]
    public void AllowsAnotherChildWhileTheFirstIsOutOfReach()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnAdult(world, "Sela", new Position(0, 0));
        var father = SpawnAdult(world, "Doran", new Position(1, 0));
        world.Execute(new BirthCommand("Bran", mother, father));
        world.People[^1].Position = new Position(50, 0);

        world.Execute(new BirthCommand("Ivy", mother, father));

        Assert.Equal(4, world.People.Count);
    }
}
