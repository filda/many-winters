using ManyWinters.Core.Population;

namespace ManyWinters.Tests.Population;

public class LifeCycleTests
{
    // The same boundaries LifeStages used to hardcode (docs/todo/fauna-plan.md, step 0c),
    // now one instance among possibly many species.
    private static readonly LifeCycle Standard = new(WeaningAgeYears: 1, AdultAgeYears: 4, ElderAgeYears: 7, MaxLifespanYears: 10);

    [Fact]
    public void ANewbornIsAnInfant()
    {
        Assert.Equal(LifeStage.Infant, Standard.StageFor(0));
    }

    [Fact]
    public void WeaningAgeEndsInfancy()
    {
        Assert.Equal(LifeStage.Child, Standard.StageFor(Standard.WeaningAgeYears));
    }

    [Fact]
    public void TheYearBeforeAdulthoodIsStillChildhood()
    {
        Assert.Equal(LifeStage.Child, Standard.StageFor(Standard.AdultAgeYears - 1));
    }

    [Fact]
    public void AdultAgeStartsAdulthood()
    {
        Assert.Equal(LifeStage.Adult, Standard.StageFor(Standard.AdultAgeYears));
    }

    [Fact]
    public void TheYearBeforeOldAgeIsStillAdulthood()
    {
        Assert.Equal(LifeStage.Adult, Standard.StageFor(Standard.ElderAgeYears - 1));
    }

    [Fact]
    public void ElderAgeStartsOldAge()
    {
        Assert.Equal(LifeStage.Elder, Standard.StageFor(Standard.ElderAgeYears));
    }

    [Fact]
    public void NobodyAgesOutOfTheLastStage()
    {
        Assert.Equal(LifeStage.Elder, Standard.StageFor(Standard.ElderAgeYears * 100));
    }
}
