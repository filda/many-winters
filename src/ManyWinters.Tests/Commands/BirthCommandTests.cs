using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class BirthCommandTests
{
    private static long AdultAgeTicks(WorldState world) => world.Configuration.Rules.TicksPerYear * LifeStages.AdultAgeYears;

    private static Person SpawnAdult(WorldState world, string name, Position position, Sex sex) =>
        world.SpawnPerson(name, position, initialAgeTicks: AdultAgeTicks(world), sex: sex);

    private static Person SpawnMother(WorldState world, Position position) => SpawnAdult(world, "Sela", position, Sex.Female);

    private static Person SpawnFather(WorldState world, Position position) => SpawnAdult(world, "Doran", position, Sex.Male);

    [Fact]
    public void AddsTheChildToTheWorld()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));

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
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));
        // The clock alone, not a full Advance: fifty ticks would also have the parents wander
        // apart, and the birth would then be refused for being out of reach (another test's job).
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
        var mother = SpawnMother(world, new Position(4, 7));
        var father = SpawnFather(world, new Position(5, 7));

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(mother.Position, world.People[^1].Position);
    }

    // Knowledge in this game is taught, never inherited.
    [Fact]
    public void TheChildInheritsNoTechniquesSkillsOrBelongings()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));
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
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));
        var announced = new List<Person>();
        world.PersonAdded += announced.Add;

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal("Bran", Assert.Single(announced).Name);
    }

    [Fact]
    public void DoesNothingWhenTheMotherIsDead()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));
        mother.IsAlive = false;

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(2, world.People.Count);
    }

    [Fact]
    public void DoesNothingWhenTheFatherIsDead()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));
        father.IsAlive = false;

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(2, world.People.Count);
    }

    [Fact]
    public void DoesNothingWhenTheTwoParentsAreTheSamePerson()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));

        world.Execute(new BirthCommand("Bran", mother, mother));

        Assert.Single(world.People);
    }

    [Fact]
    public void DoesNothingWhenTheMotherIsStillAChild()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = world.SpawnPerson("Sela", new Position(0, 0), initialAgeTicks: rules.TicksPerYear * (LifeStages.AdultAgeYears - 1), sex: Sex.Female);
        var father = SpawnFather(world, new Position(1, 0));

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(2, world.People.Count);
    }

    [Fact]
    public void DoesNothingWhenTheFatherIsStillAChild()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = SpawnMother(world, new Position(0, 0));
        var father = world.SpawnPerson("Doran", new Position(1, 0), initialAgeTicks: rules.TicksPerYear * (LifeStages.AdultAgeYears - 1), sex: Sex.Male);

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(2, world.People.Count);
    }

    // A floor on childhood, not a fertility model.
    [Fact]
    public void EldersCanStillHaveChildren()
    {
        var world = TestCatalogs.CreateWorld();
        var rules = world.Configuration.Rules;
        var mother = world.SpawnPerson("Sela", new Position(0, 0), initialAgeTicks: rules.TicksPerYear * LifeStages.ElderAgeYears, sex: Sex.Female);
        var father = world.SpawnPerson("Doran", new Position(1, 0), initialAgeTicks: rules.TicksPerYear * LifeStages.ElderAgeYears, sex: Sex.Male);

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(3, world.People.Count);
    }

    [Fact]
    public void DoesNothingWhenTheParentsAreNotStandingTogether()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(50, 0));

        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(2, world.People.Count);
    }

    // One at a time: a mother already nursing cannot feed a second newborn.
    [Fact]
    public void DoesNothingWhileTheMotherIsStillNursing()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));
        world.Execute(new BirthCommand("Bran", mother, father));

        world.Execute(new BirthCommand("Ivy", mother, father));

        Assert.Equal(3, world.People.Count);
    }

    [Fact]
    public void AllowsAnotherChildOnceTheFirstIsWeaned()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));
        world.Execute(new BirthCommand("Bran", mother, father));
        world.Clock.Advance(world.Configuration.Rules.TicksPerYear * LifeStages.WeaningAgeYears);

        world.Execute(new BirthCommand("Ivy", mother, father));

        Assert.Equal(4, world.People.Count);
    }

    // The gate is "can this mother feed someone right now", not "has she ever given birth"; an
    // infant out of reach is already starving, and BirthCommand is not where that is noticed.
    [Fact]
    public void AllowsAnotherChildWhileTheFirstIsOutOfReach()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));
        world.Execute(new BirthCommand("Bran", mother, father));
        world.People[^1].Position = new Position(50, 0);

        world.Execute(new BirthCommand("Ivy", mother, father));

        Assert.Equal(4, world.People.Count);
    }

    [Fact]
    public void NothingBlocksTwoAdultsStandingTogether()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));

        Assert.Equal(ActionBlocker.None, new BirthCommand("Bran", mother, father).Blocker(world));
    }

    [Fact]
    public void ADeadMotherBlocksTheBirth()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));
        mother.IsAlive = false;

        Assert.Equal(ActionBlocker.ActorIsDead, new BirthCommand("Bran", mother, father).Blocker(world));
    }

    [Fact]
    public void ADeadFatherBlocksTheBirth()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));
        father.IsAlive = false;

        Assert.Equal(ActionBlocker.TargetIsDead, new BirthCommand("Bran", mother, father).Blocker(world));
    }

    [Fact]
    public void OnePersonNamedAsBothParentsBlocksTheBirth()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));

        Assert.Equal(ActionBlocker.SamePerson, new BirthCommand("Bran", mother, mother).Blocker(world));
    }

    [Fact]
    public void TwoPeopleOfTheSameSexBlockTheBirth()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var other = SpawnAdult(world, "Mira", new Position(1, 0), Sex.Female);

        Assert.Equal(ActionBlocker.WrongSex, new BirthCommand("Bran", mother, other).Blocker(world));
    }

    [Fact]
    public void CloseKinBlockTheBirth()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var son = world.SpawnPerson("Doran", new Position(1, 0), initialAgeTicks: AdultAgeTicks(world), mother: mother, sex: Sex.Male);

        Assert.Equal(ActionBlocker.CloseKin, new BirthCommand("Bran", mother, son).Blocker(world));
    }

    [Fact]
    public void AParentWhoIsStillAChildBlocksTheBirth()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = world.SpawnPerson("Sela", new Position(0, 0), sex: Sex.Female);
        var father = SpawnFather(world, new Position(1, 0));

        Assert.Equal(ActionBlocker.TooYoung, new BirthCommand("Bran", mother, father).Blocker(world));
    }

    [Fact]
    public void ParentsNotStandingTogetherBlockTheBirthAsTooFar()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));

        Assert.Equal(ActionBlocker.TooFar, new BirthCommand("Bran", mother, father).Blocker(world));
    }

    [Fact]
    public void AMotherStillNursingBlocksASecondBirth()
    {
        var world = TestCatalogs.CreateWorld();
        var mother = SpawnMother(world, new Position(0, 0));
        var father = SpawnFather(world, new Position(1, 0));
        world.Execute(new BirthCommand("Bran", mother, father));

        Assert.Equal(ActionBlocker.AlreadyNursing, new BirthCommand("Ivy", mother, father).Blocker(world));
    }
}
