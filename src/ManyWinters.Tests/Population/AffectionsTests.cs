using ManyWinters.Core.Population;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Population;

public class AffectionsTests
{
    private const float Max = 100f;

    [Fact]
    public void TwoPeopleWhoHaveNeverMetMeanNothingToEachOther()
    {
        var affections = new Affections();

        Assert.Equal(0f, affections.Between(TestIds.Person(1), TestIds.Person(2)));
    }

    // The whole reason a pair is stored under a sorted key: two callers asking about the same
    // two people must not be able to reach two entries that then drift apart.
    [Fact]
    public void ABondReadsTheSameFromEitherSide()
    {
        var affections = new Affections();
        affections.Set(TestIds.Person(1), TestIds.Person(2), 30f);

        Assert.Equal(30f, affections.Between(TestIds.Person(2), TestIds.Person(1)));
    }

    [Fact]
    public void SettingABondFromTheOtherSideOverwritesTheSameOne()
    {
        var affections = new Affections();
        affections.Set(TestIds.Person(1), TestIds.Person(2), 30f);

        affections.Set(TestIds.Person(2), TestIds.Person(1), 70f);

        Assert.Equal(70f, affections.Between(TestIds.Person(1), TestIds.Person(2)));
        Assert.Single(affections.All);
    }

    [Fact]
    public void NobodyHasABondWithThemselves()
    {
        var affections = new Affections();

        Assert.Throws<ArgumentException>(() => affections.Set(TestIds.Person(1), TestIds.Person(1), 30f));
    }

    [Fact]
    public void ChangeGrowsABond()
    {
        var affections = new Affections();

        affections.Change(TestIds.Person(1), TestIds.Person(2), 12f, Max);
        affections.Change(TestIds.Person(1), TestIds.Person(2), 8f, Max);

        Assert.Equal(20f, affections.Between(TestIds.Person(1), TestIds.Person(2)));
    }

    [Fact]
    public void ABondNeverGrowsPastTheCeiling()
    {
        var affections = new Affections();
        affections.Set(TestIds.Person(1), TestIds.Person(2), Max);

        affections.Change(TestIds.Person(1), TestIds.Person(2), 50f, Max);

        Assert.Equal(Max, affections.Between(TestIds.Person(1), TestIds.Person(2)));
    }

    [Fact]
    public void ABondThatFadesToNothingIsForgottenRatherThanKeptAtZero()
    {
        var affections = new Affections();
        affections.Set(TestIds.Person(1), TestIds.Person(2), 5f);

        affections.Change(TestIds.Person(1), TestIds.Person(2), -5f, Max);

        Assert.Equal(0f, affections.Between(TestIds.Person(1), TestIds.Person(2)));
        Assert.Empty(affections.All);
    }

    [Fact]
    public void FadingPastNothingStopsAtNothingRatherThanGoingNegative()
    {
        var affections = new Affections();
        affections.Set(TestIds.Person(1), TestIds.Person(2), 5f);

        affections.Change(TestIds.Person(1), TestIds.Person(2), -50f, Max);

        Assert.Equal(0f, affections.Between(TestIds.Person(1), TestIds.Person(2)));
    }

    // Two people who have never been near each other must not leave an entry behind just
    // because the world checked on them - otherwise every pair on the map ends up stored.
    [Fact]
    public void FadingAPairThatNeverHadABondRecordsNothing()
    {
        var affections = new Affections();

        affections.Change(TestIds.Person(1), TestIds.Person(2), -1f, Max);

        Assert.Empty(affections.All);
    }

    [Fact]
    public void ForListsEverybodyThisPersonIsBondedTo()
    {
        var affections = new Affections();
        affections.Set(TestIds.Person(1), TestIds.Person(2), 10f);
        affections.Set(TestIds.Person(1), TestIds.Person(3), 30f);
        affections.Set(TestIds.Person(2), TestIds.Person(3), 20f);

        var bonds = affections.For(TestIds.Person(1)).ToList();

        Assert.Equal(2, bonds.Count);
        Assert.DoesNotContain(bonds, bond => bond.Other == TestIds.Person(1));
    }

    [Fact]
    public void ForPutsTheStrongestBondFirst()
    {
        var affections = new Affections();
        affections.Set(TestIds.Person(1), TestIds.Person(2), 10f);
        affections.Set(TestIds.Person(1), TestIds.Person(3), 30f);
        affections.Set(TestIds.Person(1), TestIds.Person(4), 20f);

        var bonds = affections.For(TestIds.Person(1)).ToList();

        Assert.Equal([TestIds.Person(3), TestIds.Person(4), TestIds.Person(2)], bonds.Select(bond => bond.Other));
    }

    [Fact]
    public void ForFindsTheBondWhicheverSideOfThePairThisPersonIsStoredOn()
    {
        var affections = new Affections();
        affections.Set(TestIds.Person(1), TestIds.Person(2), 10f);

        Assert.Equal(TestIds.Person(1), Assert.Single(affections.For(TestIds.Person(2))).Other);
        Assert.Equal(TestIds.Person(2), Assert.Single(affections.For(TestIds.Person(1))).Other);
    }

    [Fact]
    public void SomebodyWithNoBondsHasAnEmptyList()
    {
        var affections = new Affections();
        affections.Set(TestIds.Person(1), TestIds.Person(2), 10f);

        Assert.Empty(affections.For(TestIds.Person(3)));
    }

    [Fact]
    public void AllListsEachPairExactlyOnce()
    {
        var affections = new Affections();
        affections.Set(TestIds.Person(1), TestIds.Person(2), 10f);
        affections.Set(TestIds.Person(2), TestIds.Person(3), 20f);

        Assert.Equal(2, affections.All.Count());
    }
}
