using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// A female conceives on a per-tick roll rather than a pair bond, so most tests here pin
// ConceptionChancePerTick at 1 (a roll that would otherwise conceive) and prove one condition
// blocks it, rather than searching for a seed/tick that happens to roll true.
public class WorldStateBreedingTests
{
    private const long TicksPerYear = 300;
    private const long AdultAgeTicks = 3 * TicksPerYear;
    private const long ElderAgeTicks = 7 * TicksPerYear;

    // Loose enough that ordinary hunger drift across a handful of ticks never trips it by
    // accident - the fed/not-fed tests below set Hunger explicitly instead.
    private const float LooseSatietyThreshold = 1000f;

    [Fact]
    public void AFemaleWithAMateAtHomeConceivesWhenEveryConditionHolds()
    {
        var world = NewWorld();
        var home = NewHome(new Position(0, 0));
        var mother = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), home, AdultAgeTicks, Sex.Female);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(1, 0), home, AdultAgeTicks, Sex.Male);

        world.Advance(1);

        Assert.Equal(1, mother.PregnantSinceTick);
    }

    [Fact]
    public void AMaleNeverConceivesEvenWithEveryOtherConditionSatisfied()
    {
        var world = NewWorld();
        var home = NewHome(new Position(0, 0));
        var subject = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), home, AdultAgeTicks, Sex.Male);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(1, 0), home, AdultAgeTicks, Sex.Male);

        world.Advance(1);

        Assert.Null(subject.PregnantSinceTick);
    }

    [Fact]
    public void AnElderFemaleDoesNotConceive()
    {
        var world = NewWorld();
        var home = NewHome(new Position(0, 0));
        var mother = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), home, ElderAgeTicks, Sex.Female);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(1, 0), home, AdultAgeTicks, Sex.Male);

        world.Advance(1);

        Assert.Null(mother.PregnantSinceTick);
    }

    [Fact]
    public void AnAlreadyPregnantFemaleDoesNotRollAgain()
    {
        var world = NewWorld(gestationTicks: 1000);
        var home = NewHome(new Position(0, 0));
        var mother = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), home, AdultAgeTicks, Sex.Female);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(1, 0), home, AdultAgeTicks, Sex.Male);

        world.Advance(1);
        Assert.Equal(1, mother.PregnantSinceTick);

        // Still pregnant, mate still there, chance still 1 - a fresh roll would "conceive" again
        // if the pregnancy guard were missing, changing PregnantSinceTick to a later tick.
        world.Advance(10);

        Assert.Equal(1, mother.PregnantSinceTick);
    }

    [Fact]
    public void ANursingMotherDoesNotConceive()
    {
        var world = NewWorld();
        var home = NewHome(new Position(0, 0));
        var mother = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), home, AdultAgeTicks, Sex.Female);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(1, 0), home, AdultAgeTicks, Sex.Male);
        // A fawn at her side, well within the weaning age - NursingInfantOf(mother) reads this.
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), home, initialAgeTicks: 0, sex: Sex.Female, mother: mother);

        world.Advance(1);

        Assert.Null(mother.PregnantSinceTick);
    }

    [Fact]
    public void AHungryFemaleDoesNotConceive()
    {
        var world = NewWorld(satietyHungerBelow: 40f);
        var home = NewHome(new Position(0, 0));
        var mother = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), home, AdultAgeTicks, Sex.Female);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(1, 0), home, AdultAgeTicks, Sex.Male);
        mother.Needs.Hunger = 40f;

        world.Advance(1);

        Assert.Null(mother.PregnantSinceTick);
    }

    [Fact]
    public void NobodyConceivesOutsideTheBreedingClimate()
    {
        var world = NewWorld();
        var home = NewHome(new Position(0, 0));

        // Nobody exists yet while the world walks past Spring (Mild) into Summer (Hot), so
        // nothing can conceive during the season this test isn't about.
        world.Advance(TicksPerYear / 4);
        Assert.Equal(Season.Summer, world.CurrentSeason);

        var mother = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), home, AdultAgeTicks, Sex.Female);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(1, 0), home, AdultAgeTicks, Sex.Male);

        world.Advance(1);

        Assert.Null(mother.PregnantSinceTick);
    }

    [Fact]
    public void AFemaleWithNoMaleAtHomeDoesNotConceive()
    {
        var world = NewWorld();
        var home = NewHome(new Position(0, 0));
        var mother = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), home, AdultAgeTicks, Sex.Female);

        world.Advance(1);

        Assert.Null(mother.PregnantSinceTick);
    }

    [Fact]
    public void AMaleInADifferentHomeRangeDoesNotCount()
    {
        var world = NewWorld();
        var home = NewHome(new Position(0, 0));
        var otherHome = NewHome(new Position(1000, 1000));
        var mother = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0, 0), home, AdultAgeTicks, Sex.Female);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(1000, 1000), otherHome, AdultAgeTicks, Sex.Male);

        world.Advance(1);

        Assert.Null(mother.PregnantSinceTick);
    }

    [Fact]
    public void BirthHappensExactlyAtGestationWithMotherHomeSexAndInnateTechniquesSet()
    {
        var world = NewWorld(gestationTicks: 10);
        var home = NewHome(new Position(2, 3));
        var mother = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(2, 3), home, AdultAgeTicks, Sex.Female);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(3, 3), home, AdultAgeTicks, Sex.Male);

        world.Advance(1);
        Assert.Equal(1, mother.PregnantSinceTick);

        // Gestation is 10 ticks from conception (tick 1): still carrying one tick short of due.
        world.Advance(9);
        Assert.Equal(2, world.Animals.Count);

        // The tick birth is due.
        world.Advance(1);

        Assert.Equal(3, world.Animals.Count);
        var fawn = world.Animals[2];
        Assert.Same(mother, fawn.Mother);
        Assert.Same(home, fawn.Home);
        Assert.Equal(11, fawn.BirthTick);
        Assert.Equal(Creature.SexOf(fawn.Id), fawn.Sex);
        Assert.Contains(TestCatalogs.BasicEating, fawn.KnownTechniques);
        Assert.Contains(TestCatalogs.BasicForaging, fawn.KnownTechniques);
        Assert.Null(mother.PregnantSinceTick);
    }

    [Fact]
    public void TheConceptionRollIsDeterministicAcrossTwoIdenticalWorlds()
    {
        var motherId = new CreatureId(new Guid(1, 0, 0, new byte[8]));
        var maleId = new CreatureId(new Guid(2, 0, 0, new byte[8]));

        (bool Conceived, long? PregnantSinceTick) RunOnce()
        {
            var world = TestCatalogs.CreateWorldWithDeer();
            var home = NewHome(new Position(0, 0));
            world.Execute(new SpawnAnimalCommand(motherId, TestCatalogs.DeerSpeciesId, new Position(0, 0), home, Sex.Female, -AdultAgeTicks));
            world.Execute(new SpawnAnimalCommand(maleId, TestCatalogs.DeerSpeciesId, new Position(1, 0), home, Sex.Male, -AdultAgeTicks));

            // A full year at the shipped deer's (low) conception chance: enough rolls across
            // both Mild seasons that two identical worlds either both conceive or both don't -
            // the point is the two runs agreeing, not forcing a particular outcome.
            world.Advance(TicksPerYear);

            var restored = world.Animals[0];
            return (restored.PregnantSinceTick is not null, restored.PregnantSinceTick);
        }

        var first = RunOnce();
        var second = RunOnce();

        Assert.Equal(first, second);
    }

    private static WorldState NewWorld(long gestationTicks = 150, float conceptionChancePerTick = 1f, float satietyHungerBelow = LooseSatietyThreshold, Climate climate = Climate.Mild) =>
        new(TestCatalogs.CreateConfigurationWithDeerBreeding(new SpeciesDefinition.BreedingDefinition(climate, gestationTicks, conceptionChancePerTick, satietyHungerBelow)));

    private static HomeRange NewHome(Position anchor) => new(anchor) { Radius = 15f, DriftMetresPerSeason = 0f };
}
