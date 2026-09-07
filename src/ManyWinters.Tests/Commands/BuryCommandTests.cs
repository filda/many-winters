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
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        var deceased = world.SpawnPerson("Ava", new Position(1, 1));
        deceased.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        deceased.IsAlive = false;
        deceased.DeathTick = world.Configuration.Rules.TicksPerYear * 3;

        world.Execute(new BuryCommand(buryingPerson, deceased));

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
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        var deceased = world.SpawnPerson("Ava", new Position(1, 1));
        deceased.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        deceased.IsAlive = false;
        deceased.DeathTick = world.Configuration.Rules.TicksPerYear * 3;

        world.Execute(new BuryCommand(buryingPerson, deceased));

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
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        var mother = world.SpawnPerson("Sela", new Position(0, 0));
        var father = world.SpawnPerson("Doran", new Position(0, 0));
        var deceased = world.SpawnPerson("Ava", new Position(1, 1), mother: mother, father: father);
        deceased.IsAlive = false;
        deceased.CauseOfDeath = DeathCause.OldAge;

        world.Execute(new BuryCommand(buryingPerson, deceased));

        var grave = Assert.Single(world.Graves);
        Assert.Equal(DeathCause.OldAge, grave.CauseOfDeath);
        Assert.Equal("Sela", grave.MotherName);
        Assert.Equal("Doran", grave.FatherName);
    }

    [Fact]
    public void BuryingWithTheTechniqueNamesUnrememberedParentsAsUnknown()
    {
        // No null to check: a person nobody remembers the parents of has Person.Unknown for
        // both (see Person.Mother), and that is what the grave records.
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        var deceased = world.SpawnPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson, deceased));

        var grave = Assert.Single(world.Graves);
        Assert.Null(grave.CauseOfDeath);
        Assert.Equal(Person.Unknown.Name, grave.MotherName);
        Assert.Equal(Person.Unknown.Name, grave.FatherName);
    }

    [Fact]
    public void BuryingWithTheTechniqueNamesAForebearParentWhoWasNeverInTheWorldsPeople()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        var forebear = world.SpawnForebear("Orla");
        var deceased = world.SpawnPerson("Ava", new Position(1, 1), mother: forebear);
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson, deceased));

        var grave = Assert.Single(world.Graves);
        Assert.Equal("Orla", grave.MotherName);
    }

    [Fact]
    public void BuryingWithTheTechniqueFindsAParentsNameEvenWhenThatParentIsAlsoDeadButUnburied()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        var mother = world.SpawnPerson("Sela", new Position(0, 0));
        mother.IsAlive = false;
        var deceased = world.SpawnPerson("Ava", new Position(1, 1), mother: mother);
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson, deceased));

        var grave = Assert.Single(world.Graves);
        Assert.Equal("Sela", grave.MotherName);
    }

    [Fact]
    public void BuryingWithoutTheTechniqueLeavesCauseOfDeathAndParentNamesNullEvenWhenRecorded()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        var mother = world.SpawnPerson("Sela", new Position(0, 0));
        var father = world.SpawnPerson("Doran", new Position(0, 0));
        var deceased = world.SpawnPerson("Ava", new Position(1, 1), mother: mother, father: father);
        deceased.IsAlive = false;
        deceased.CauseOfDeath = DeathCause.Hunger;

        world.Execute(new BuryCommand(buryingPerson, deceased));

        var grave = Assert.Single(world.Graves);
        Assert.Null(grave.CauseOfDeath);
        Assert.Null(grave.MotherName);
        Assert.Null(grave.FatherName);
    }

    [Fact]
    public void BuryingWithAMissingDeathTickFallsBackToTheCurrentTickForAgeCalculation()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        buryingPerson.KnownTechniques.Add(TestCatalogs.EfficientBurial);
        world.Clock.Advance(world.Configuration.Rules.TicksPerYear * 2);
        var deceased = world.SpawnPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson, deceased));

        var grave = Assert.Single(world.Graves);
        Assert.Equal(0, grave.AgeAtDeath);
    }

    [Fact]
    public void BuryingRaisesGraveAddedAndTracksItInGraves()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        var deceased = world.SpawnPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;
        Grave? raised = null;
        world.GraveAdded += g => raised = g;

        world.Execute(new BuryCommand(buryingPerson, deceased));

        var tracked = Assert.Single(world.Graves);
        Assert.Same(tracked, raised);
    }

    [Fact]
    public void BuryingIncreasesTheBurialSkill()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        var deceased = world.SpawnPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson, deceased));

        Assert.Equal(1f, buryingPerson.Skills.Get(TestCatalogs.Burial));
    }

    [Fact]
    public void FiveBurialsDiscoverEfficientBurial()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));

        for (var i = 0; i < 4; i++)
        {
            var deceased = world.SpawnPerson($"Deceased{i}", new Position(1, 1));
            deceased.IsAlive = false;
            world.Execute(new BuryCommand(buryingPerson, deceased));
        }

        Assert.DoesNotContain(TestCatalogs.EfficientBurial, buryingPerson.KnownTechniques);

        var fifthDeceased = world.SpawnPerson("Deceased4", new Position(1, 1));
        fifthDeceased.IsAlive = false;
        world.Execute(new BuryCommand(buryingPerson, fifthDeceased));

        // Five burials, not five levels - practice has diminishing returns (see
        // Skills.Increase), and the threshold is written as five burials' worth of it.
        Assert.Equal(2.553f, buryingPerson.Skills.Get(TestCatalogs.Burial), 3);
        Assert.Contains(TestCatalogs.EfficientBurial, buryingPerson.KnownTechniques);
    }

    [Fact]
    public void BuryingRequiresTheBuryingPersonToBeAlive()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        buryingPerson.IsAlive = false;
        var deceased = world.SpawnPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson, deceased));

        Assert.Empty(world.Graves);
        Assert.False(deceased.IsBuried);
    }

    [Fact]
    public void BuryingRequiresTheDeceasedToActuallyBeDead()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        var stillAlive = world.SpawnPerson("Ava", new Position(1, 1));

        world.Execute(new BuryCommand(buryingPerson, stillAlive));

        Assert.Empty(world.Graves);
    }

    [Fact]
    public void BuryingAnAlreadyBuriedPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        var deceased = world.SpawnPerson("Ava", new Position(1, 1));
        deceased.IsAlive = false;
        world.Execute(new BuryCommand(buryingPerson, deceased));

        world.Execute(new BuryCommand(buryingPerson, deceased));

        Assert.Single(world.Graves);
        Assert.Equal(1f, buryingPerson.Skills.Get(TestCatalogs.Burial));
    }

    [Fact]
    public void BuryingAtExactlyTheMaxInteractionDistanceStillWorks()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        var deceased = world.SpawnPerson("Ava", new Position(world.Configuration.Rules.MaxInteractionDistance, 0));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson, deceased));

        Assert.Single(world.Graves);
    }

    [Fact]
    public void BuryingBeyondTheMaxInteractionDistanceDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var buryingPerson = world.SpawnPerson("Bran", new Position(0, 0));
        var deceased = world.SpawnPerson("Ava", new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0));
        deceased.IsAlive = false;

        world.Execute(new BuryCommand(buryingPerson, deceased));

        Assert.Empty(world.Graves);
        Assert.False(deceased.IsBuried);
    }
}
