using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Population;

public class CarryCapacityTests
{
    private static long AgeTicksFor(long ageInYears) => SimulationRules.Default.TicksPerYear * ageInYears;

    private static float MaxCarryWeightAt(long ageInYears)
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: AgeTicksFor(ageInYears));
        return world.MaxCarryWeightFor(person);
    }

    [Fact]
    public void ANewbornCarriesOnlyAFractionOfTheAdultBaseline()
    {
        Assert.Equal(10f, MaxCarryWeightAt(ageInYears: 0));
    }

    [Fact]
    public void CapacityGrowsLinearlyBetweenBirthAndAdulthood()
    {
        Assert.Equal(20f, MaxCarryWeightAt(ageInYears: 1));
    }

    [Fact]
    public void CapacityReachesTheAdultBaselineAtAdultAge()
    {
        Assert.Equal(50f, MaxCarryWeightAt(ageInYears: TestCatalogs.AdultAgeYears));
    }

    [Fact]
    public void CapacityStaysAtTheAdultBaselineThroughThePrimeYears()
    {
        Assert.Equal(50f, MaxCarryWeightAt(ageInYears: 6));
    }

    [Fact]
    public void CapacityHasNotYetDeclinedRightAtTheStartOfOldAge()
    {
        Assert.Equal(50f, MaxCarryWeightAt(ageInYears: TestCatalogs.ElderAgeYears));
    }

    [Fact]
    public void CapacityDeclinesGraduallyThroughOldAge()
    {
        var atElderStart = MaxCarryWeightAt(ageInYears: TestCatalogs.ElderAgeYears);
        var midway = MaxCarryWeightAt(ageInYears: 8);
        var atMaxLifespan = MaxCarryWeightAt(ageInYears: 10);

        Assert.True(midway < atElderStart);
        Assert.True(atMaxLifespan < midway);
        Assert.Equal(50f * 0.85f, atMaxLifespan);
    }

    [Fact]
    public void CapacityNeverDeclinesBelowTheElderFloorEvenPastMaxLifespan()
    {
        Assert.Equal(50f * 0.85f, MaxCarryWeightAt(ageInYears: 50));
    }
}
