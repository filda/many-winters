using ManyWinters.Core.Population;

namespace ManyWinters.Tests.Population;

public class CarryCapacityTests
{
    // The same ages LifeCycleTests' Standard uses (docs/todo/fauna-plan.md, step 0c): the two
    // curves turn at the same points, on purpose (see CarryCapacity's own doc comment).
    private static readonly LifeCycle Standard = new(WeaningAgeYears: 1, AdultAgeYears: 4, ElderAgeYears: 7, MaxLifespanYears: 10);

    [Fact]
    public void ANewbornCarriesOnlyAFractionOfTheAdultBaseline()
    {
        Assert.Equal(10f, CarryCapacity.BaseWeightFor(ageInYears: 0, Standard));
    }

    [Fact]
    public void CapacityGrowsLinearlyBetweenBirthAndAdulthood()
    {
        Assert.Equal(20f, CarryCapacity.BaseWeightFor(ageInYears: 1, Standard));
    }

    [Fact]
    public void CapacityReachesTheAdultBaselineAtAdultAge()
    {
        Assert.Equal(CarryCapacity.AdultBaseWeight, CarryCapacity.BaseWeightFor(ageInYears: 4, Standard));
    }

    [Fact]
    public void CapacityStaysAtTheAdultBaselineThroughThePrimeYears()
    {
        Assert.Equal(CarryCapacity.AdultBaseWeight, CarryCapacity.BaseWeightFor(ageInYears: 6, Standard));
    }

    [Fact]
    public void CapacityHasNotYetDeclinedRightAtTheStartOfOldAge()
    {
        Assert.Equal(CarryCapacity.AdultBaseWeight, CarryCapacity.BaseWeightFor(ageInYears: 7, Standard));
    }

    [Fact]
    public void CapacityDeclinesGraduallyThroughOldAge()
    {
        var atElderStart = CarryCapacity.BaseWeightFor(ageInYears: 7, Standard);
        var midway = CarryCapacity.BaseWeightFor(ageInYears: 8, Standard);
        var atMaxLifespan = CarryCapacity.BaseWeightFor(ageInYears: 10, Standard);

        Assert.True(midway < atElderStart);
        Assert.True(atMaxLifespan < midway);
        Assert.Equal(CarryCapacity.AdultBaseWeight * 0.85f, atMaxLifespan);
    }

    [Fact]
    public void CapacityNeverDeclinesBelowTheElderFloorEvenPastMaxLifespan()
    {
        Assert.Equal(
            CarryCapacity.AdultBaseWeight * 0.85f,
            CarryCapacity.BaseWeightFor(ageInYears: 50, Standard));
    }
}
