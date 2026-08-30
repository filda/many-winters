using ManyWinters.Core.Population;

namespace ManyWinters.Tests.Population;

public class CarryCapacityTests
{
    [Fact]
    public void ANewbornCarriesOnlyAFractionOfTheAdultBaseline()
    {
        Assert.Equal(10f, CarryCapacity.BaseWeightFor(ageInYears: 0, maxLifespanYears: 10));
    }

    [Fact]
    public void CapacityGrowsLinearlyBetweenBirthAndAdulthood()
    {
        Assert.Equal(20f, CarryCapacity.BaseWeightFor(ageInYears: 1, maxLifespanYears: 10));
    }

    [Fact]
    public void CapacityReachesTheAdultBaselineAtAdultAge()
    {
        Assert.Equal(CarryCapacity.AdultBaseWeight, CarryCapacity.BaseWeightFor(ageInYears: 4, maxLifespanYears: 10));
    }

    [Fact]
    public void CapacityStaysAtTheAdultBaselineThroughThePrimeYears()
    {
        Assert.Equal(CarryCapacity.AdultBaseWeight, CarryCapacity.BaseWeightFor(ageInYears: 6, maxLifespanYears: 10));
    }

    [Fact]
    public void CapacityHasNotYetDeclinedRightAtTheStartOfOldAge()
    {
        Assert.Equal(CarryCapacity.AdultBaseWeight, CarryCapacity.BaseWeightFor(ageInYears: 7, maxLifespanYears: 10));
    }

    [Fact]
    public void CapacityDeclinesGraduallyThroughOldAge()
    {
        var atElderStart = CarryCapacity.BaseWeightFor(ageInYears: 7, maxLifespanYears: 10);
        var midway = CarryCapacity.BaseWeightFor(ageInYears: 8, maxLifespanYears: 10);
        var atMaxLifespan = CarryCapacity.BaseWeightFor(ageInYears: 10, maxLifespanYears: 10);

        Assert.True(midway < atElderStart);
        Assert.True(atMaxLifespan < midway);
        Assert.Equal(CarryCapacity.AdultBaseWeight * 0.85f, atMaxLifespan);
    }

    [Fact]
    public void CapacityNeverDeclinesBelowTheElderFloorEvenPastMaxLifespan()
    {
        Assert.Equal(
            CarryCapacity.AdultBaseWeight * 0.85f,
            CarryCapacity.BaseWeightFor(ageInYears: 50, maxLifespanYears: 10));
    }
}
