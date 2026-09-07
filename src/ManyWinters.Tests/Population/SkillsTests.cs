using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Population;

namespace ManyWinters.Tests.Population;

public class SkillsTests
{
    private static readonly SkillTypeId Foraging = new("foraging");
    private static readonly SkillTypeId Woodcutting = new("woodcutting");

    [Fact]
    public void ASkillNobodyHasPracticedIsZero()
    {
        Assert.Equal(0f, new Skills().Get(Foraging));
    }

    [Fact]
    public void TheFirstPracticeIsWorthItsFullValue()
    {
        // Nothing has been learned yet, so there is nothing for the curve to discount against.
        var skills = new Skills();

        skills.Increase(Foraging, 1f);

        Assert.Equal(1f, skills.Get(Foraging));
    }

    [Fact]
    public void EachFurtherPracticeTeachesLessThanTheOneBeforeIt()
    {
        // The whole point of the curve: repetition is cheap, so the tenth repetition must not
        // be worth as much as the second. Compared as three steps of the same size rather than
        // against fixed numbers, so this stays true of any curve that actually diminishes.
        var skills = new Skills();

        skills.Increase(Foraging, 1f);
        var first = skills.Get(Foraging);
        skills.Increase(Foraging, 1f);
        var second = skills.Get(Foraging) - first;
        skills.Increase(Foraging, 1f);
        var third = skills.Get(Foraging) - first - second;

        Assert.True(second < first, "the second practice should teach less than the first");
        Assert.True(third < second, "the third should teach less than the second");
        Assert.True(third > 0f, "but practice should never stop teaching anything at all");
    }

    [Fact]
    public void ABiggerPracticeIsStillWorthMoreThanASmallerOneAtTheSameLevel()
    {
        // The curve discounts by how much is already known, not by how much is being done -
        // otherwise a proper day's work and a token effort would be worth the same.
        var small = new Skills();
        var large = new Skills();

        small.Increase(Foraging, 0.25f);
        large.Increase(Foraging, 1f);

        Assert.True(large.Get(Foraging) > small.Get(Foraging));
    }

    [Fact]
    public void PracticeAtOneSkillLeavesEveryOtherSkillWhereItWas()
    {
        var skills = new Skills();

        skills.Increase(Foraging, 1f);

        Assert.Equal(0f, skills.Get(Woodcutting));
    }

    [Fact]
    public void FivePracticesLandOnTheLevelTheDiscoveryThresholdsAreWrittenAgainst()
    {
        // 1 + 1/2 + 1/2.5 + 1/2.9 + 1/3.2448. Asserted as a number rather than against
        // LevelAfter, which would only prove the curve agrees with itself - every command's
        // discovery threshold is five practices' worth of exactly this.
        var skills = new Skills();
        for (var practice = 0; practice < 5; practice++)
        {
            skills.Increase(Foraging, 1f);
        }

        Assert.Equal(2.553f, skills.Get(Foraging), 3);
    }

    [Fact]
    public void LevelAfterAgreesWithActuallyPracticingThatManyTimes()
    {
        // Discovery thresholds are compared against a level built up one practice at a time,
        // so "five tries' worth" has to be the same number to the last bit, not merely close -
        // a hair too high and the fifth try would miss the threshold it is meant to reach.
        var skills = new Skills();
        for (var practice = 0; practice < 7; practice++)
        {
            skills.Increase(Foraging, 1f);
        }

        Assert.Equal(skills.Get(Foraging), Skills.LevelAfter(7));
    }

    [Fact]
    public void LevelAfterNoPracticeAtAllIsNothing()
    {
        Assert.Equal(0f, Skills.LevelAfter(0));
    }

    [Fact]
    public void RestoringASavedLevelPutsItBackExactlyRatherThanRunningItThroughTheCurve()
    {
        // What a save holds is a level that already has the curve baked into it. Treating it
        // as practice on load (Increase(20) from nothing) would be re-deriving a number that
        // was already derived.
        var skills = new Skills();

        skills.Restore(Foraging, 20f);

        Assert.Equal(20f, skills.Get(Foraging));
    }

    [Fact]
    public void RestoringReplacesWhateverWasThereRatherThanAddingToIt()
    {
        var skills = new Skills();
        skills.Increase(Foraging, 1f);

        skills.Restore(Foraging, 3f);

        Assert.Equal(3f, skills.Get(Foraging));
    }

    [Fact]
    public void LevelsListEverySkillPracticed()
    {
        var skills = new Skills();

        skills.Increase(Foraging, 1f);
        skills.Increase(Woodcutting, 1f);

        Assert.Equal([Foraging, Woodcutting], skills.Levels.Keys);
    }
}
