using ManyWinters.Core.Population;

namespace ManyWinters.Tests.Population;

public class LifeStagesTests
{
    [Fact]
    public void ANewbornIsAnInfant()
    {
        Assert.Equal(LifeStage.Infant, LifeStages.For(0));
    }

    [Fact]
    public void WeaningAgeEndsInfancy()
    {
        Assert.Equal(LifeStage.Child, LifeStages.For(LifeStages.WeaningAgeYears));
    }

    [Fact]
    public void TheYearBeforeAdulthoodIsStillChildhood()
    {
        Assert.Equal(LifeStage.Child, LifeStages.For(LifeStages.AdultAgeYears - 1));
    }

    [Fact]
    public void AdultAgeStartsAdulthood()
    {
        Assert.Equal(LifeStage.Adult, LifeStages.For(LifeStages.AdultAgeYears));
    }

    [Fact]
    public void TheYearBeforeOldAgeIsStillAdulthood()
    {
        Assert.Equal(LifeStage.Adult, LifeStages.For(LifeStages.ElderAgeYears - 1));
    }

    [Fact]
    public void ElderAgeStartsOldAge()
    {
        Assert.Equal(LifeStage.Elder, LifeStages.For(LifeStages.ElderAgeYears));
    }

    [Fact]
    public void NobodyAgesOutOfTheLastStage()
    {
        Assert.Equal(LifeStage.Elder, LifeStages.For(LifeStages.ElderAgeYears * 100));
    }

    // CarryCapacity's curve turns at these same ages (see its own doc comment) - if the two
    // ever drift apart, a person's stage and their strength stop describing the same body.
    [Fact]
    public void CarryCapacityReachesItsAdultBaselineExactlyWhenAdulthoodStarts()
    {
        var lastYearOfChildhood = CarryCapacity.BaseWeightFor(LifeStages.AdultAgeYears - 1, maxLifespanYears: 10);
        var firstYearOfAdulthood = CarryCapacity.BaseWeightFor(LifeStages.AdultAgeYears, maxLifespanYears: 10);

        Assert.True(lastYearOfChildhood < CarryCapacity.AdultBaseWeight);
        Assert.Equal(CarryCapacity.AdultBaseWeight, firstYearOfAdulthood);
    }

    [Fact]
    public void CarryCapacityStartsDecliningExactlyWhenOldAgeStarts()
    {
        var lastYearOfAdulthood = CarryCapacity.BaseWeightFor(LifeStages.ElderAgeYears - 1, maxLifespanYears: 10);
        var oneYearIntoOldAge = CarryCapacity.BaseWeightFor(LifeStages.ElderAgeYears + 1, maxLifespanYears: 10);

        Assert.Equal(CarryCapacity.AdultBaseWeight, lastYearOfAdulthood);
        Assert.True(oneYearIntoOldAge < CarryCapacity.AdultBaseWeight);
    }
}
