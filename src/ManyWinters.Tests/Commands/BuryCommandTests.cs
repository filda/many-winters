using ManyWinters.Core.Commands;
using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class BuryCommandTests
{
    [Fact]
    public void BuryingWithoutTheTechniqueProducesAnAnonymousGrave()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        var deceased = world.AddPerson("Ava", new Position(1, 1));
        deceased.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        deceased.IsAlive = false;
        deceased.DeathTick = WorldState.TicksPerYear * 3;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        var grave = Assert.Single(world.Graves);
        Assert.False(grave.IsMarked);
        Assert.Null(grave.Name);
        Assert.Null(grave.AgeAtDeath);
        Assert.Empty(grave.KnownTechniques);
        Assert.Equal(deceased.Position, grave.Position);
        Assert.True(deceased.IsBuried);
    }

    [Fact]
    public void BuryingWithTheTechniqueProducesAFullyRecordedGrave()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        var deceased = world.AddPerson("Ava", new Position(1, 1));
        deceased.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        deceased.IsAlive = false;
        deceased.DeathTick = WorldState.TicksPerYear * 3;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        var grave = Assert.Single(world.Graves);
        Assert.True(grave.IsMarked);
        Assert.Equal("Ava", grave.Name);
        Assert.Equal(3, grave.AgeAtDeath);
        Assert.Contains(TestCatalogs.EfficientForaging, grave.KnownTechniques);
    }

    [Fact]
    public void BuryingWithTheTechniqueRecordsCauseOfDeathAndParentNames()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        var mother = world.AddPerson("Sela", new Position(0, 0));
        var father = world.AddPerson("Doran", new Position(0, 0));
        var deceased = world.AddPerson("Ava", new Position(1, 1), motherId: mother.Id, fatherId: father.Id);
        deceased.IsAlive = false;
        deceased.CauseOfDeath = DeathCause.OldAge;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        var grave = Assert.Single(world.Graves);
        Assert.Equal(DeathCause.OldAge, grave.CauseOfDeath);
        Assert.Equal("Sela", grave.MotherName);
        Assert.Equal("Doran", grave.FatherName);
    }

    [Fact]
    public void BuryingWithTheTechniqueLeavesParentNamesNullWhenNoneAreRecorded()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        var deceased = world.AddPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        var grave = Assert.Single(world.Graves);
        Assert.Null(grave.CauseOfDeath);
        Assert.Null(grave.MotherName);
        Assert.Null(grave.FatherName);
    }

    [Fact]
    public void BuryingWithTheTechniqueLeavesParentNamesNullWhenTheRecordedParentIdDoesNotExist()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        var deceased = world.AddPerson(
            "Ava",
            new Position(1, 1),
            motherId: new PersonId(999),
            fatherId: new PersonId(998));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        var grave = Assert.Single(world.Graves);
        Assert.Null(grave.MotherName);
        Assert.Null(grave.FatherName);
    }

    [Fact]
    public void BuryingWithTheTechniqueFindsAParentsNameEvenWhenThatParentIsAlsoDeadButUnburied()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        var mother = world.AddPerson("Sela", new Position(0, 0));
        mother.IsAlive = false;
        var deceased = world.AddPerson("Ava", new Position(1, 1), motherId: mother.Id);
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        var grave = Assert.Single(world.Graves);
        Assert.Equal("Sela", grave.MotherName);
    }

    [Fact]
    public void BuryingWithoutTheTechniqueLeavesCauseOfDeathAndParentNamesNullEvenWhenRecorded()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        var mother = world.AddPerson("Sela", new Position(0, 0));
        var father = world.AddPerson("Doran", new Position(0, 0));
        var deceased = world.AddPerson("Ava", new Position(1, 1), motherId: mother.Id, fatherId: father.Id);
        deceased.IsAlive = false;
        deceased.CauseOfDeath = DeathCause.Hunger;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        var grave = Assert.Single(world.Graves);
        Assert.Null(grave.CauseOfDeath);
        Assert.Null(grave.MotherName);
        Assert.Null(grave.FatherName);
    }

    [Fact]
    public void BuryingWithAMissingDeathTickFallsBackToTheCurrentTickForAgeCalculation()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        world.Clock.Advance(WorldState.TicksPerYear * 2);
        var deceased = world.AddPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        var grave = Assert.Single(world.Graves);
        Assert.Equal(0, grave.AgeAtDeath);
    }

    [Fact]
    public void BuryingRaisesGraveAddedAndTracksItInGraves()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        var deceased = world.AddPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;
        Grave? raised = null;
        world.GraveAdded += g => raised = g;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        var tracked = Assert.Single(world.Graves);
        Assert.Same(tracked, raised);
    }

    [Fact]
    public void BuryingIncreasesTheBurialSkill()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        var deceased = world.AddPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        Assert.Equal(1f, buryingPerson.Skills.Get(TestCatalogs.Burial));
    }

    [Fact]
    public void FiveBurialsDiscoverEfficientBurial()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));

        for (var i = 0; i < 4; i++)
        {
            var deceased = world.AddPerson($"Deceased{i}", new Position(1, 1));
            deceased.IsAlive = false;
            world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));
        }

        Assert.DoesNotContain(TestCatalogs.EfficientBurial, buryingPerson.KnownTechniques);

        var fifthDeceased = world.AddPerson("Deceased4", new Position(1, 1));
        fifthDeceased.IsAlive = false;
        world.Execute(new BuryCommand(buryingPerson.Id, fifthDeceased.Id));

        Assert.Equal(5f, buryingPerson.Skills.Get(TestCatalogs.Burial));
        Assert.Contains(TestCatalogs.EfficientBurial, buryingPerson.KnownTechniques);
    }

    [Fact]
    public void BuryingRequiresTheBuryingPersonToBeAlive()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        buryingPerson.IsAlive = false;
        var deceased = world.AddPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        Assert.Empty(world.Graves);
        Assert.False(deceased.IsBuried);
    }

    [Fact]
    public void BuryingRequiresTheDeceasedToActuallyBeDead()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        var stillAlive = world.AddPerson("Ava", new Position(1, 1));

        world.Execute(new BuryCommand(buryingPerson.Id, stillAlive.Id));

        Assert.Empty(world.Graves);
    }

    [Fact]
    public void BuryingAnAlreadyBuriedPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        var deceased = world.AddPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;
        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        Assert.Single(world.Graves);
        Assert.Equal(1f, buryingPerson.Skills.Get(TestCatalogs.Burial));
    }

    [Fact]
    public void BuryingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        var deceased = world.AddPerson("Ava", new Position(WorldState.MaxInteractionDistance, 0));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        Assert.Single(world.Graves);
    }

    [Fact]
    public void BuryingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        var deceased = world.AddPerson("Ava", new Position(WorldState.MaxInteractionDistance + 1, 0));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson.Id, deceased.Id));

        Assert.Empty(world.Graves);
        Assert.False(deceased.IsBuried);
    }

    [Fact]
    public void BuryingWithUnknownPersonIdsDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.AddPerson("Bran", new Position(0, 0));
        var deceased = world.AddPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(new PersonId(999), deceased.Id));
        world.Execute(new BuryCommand(buryingPerson.Id, new PersonId(999)));

        Assert.Empty(world.Graves);
    }
}
