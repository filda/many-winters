using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;
using static ManyWinters.Tests.TestSupport.InscriptionAssertions;

namespace ManyWinters.Tests.Continuity;

public class PrologueTests
{
    private const int ManySeeds = 200;

    private static Person Eldest(string name = "Liska", Sex sex = Sex.Female, int idSeed = 1) =>
        new()
        {
            Id = TestIds.Person(idSeed),
            Name = name,
            BirthTick = -2700,
            Mother = Person.Unknown,
            Father = Person.Unknown,
            Sex = sex,
        };

    private static BandArrival Arrival(
        Person? eldest = null,
        Season season = Season.Spring,
        int people = 15,
        int men = 6,
        int women = 6,
        int children = 3,
        int eldestWinters = 9,
        bool knowsAnything = false) =>
        new()
        {
            BandName = "Liska's people",
            Season = season,
            ArrivalTick = 0,
            People = people,
            Men = men,
            Women = women,
            Children = children,
            Eldest = eldest ?? Eldest(),
            EldestWinters = eldestWinters,
            KnowsAnything = knowsAnything,
        };

    private static IEnumerable<Inscription> OverManyBands(Func<Person, BandArrival> arrival, Sex sex = Sex.Female) =>
        Enumerable.Range(1, ManySeeds).Select(seed => Prologue.Write(arrival(Eldest(sex: sex, idSeed: seed))));

    [Fact]
    public void TheShippedBandsPrologueIsWrittenInFull()
    {
        var inscription = Prologue.Write(Arrival());

        Assert.Equal("Liska's people come to the land", inscription.Title);
        Assert.Equal(
            [
                "They came in the spring, fifteen in all: six men, six women and three children.",
                "The eldest among them was Liska, who had seen nine winters; they were her people.",
                "None of them knew how to feed themselves here.",
                "Their graves, if any are dug, will say the rest.",
            ],
            inscription.Lines);
    }

    [Fact]
    public void TheSameBandArrivesWithTheSameWords()
    {
        var arrival = Arrival();

        var first = Prologue.Write(arrival);
        var second = Prologue.Write(arrival);

        Assert.Equal(first.Title, second.Title);
        Assert.Equal(first.Lines, second.Lines);
    }

    [Fact]
    public void EverySentenceReadsAsOneWhateverIsDrawn()
    {
        foreach (var knows in new[] { false, true })
        {
            foreach (var inscription in OverManyBands(eldest => Arrival(eldest, knowsAnything: knows)))
            {
                AssertReadsAsAnInscription(inscription);
            }

            foreach (var inscription in OverManyBands(eldest => Arrival(eldest, knowsAnything: knows, eldestWinters: 0), Sex.Male))
            {
                AssertReadsAsAnInscription(inscription);
            }
        }
    }

    [Fact]
    public void EveryTitleIsDrawn()
    {
        var titles = OverManyBands(eldest => Arrival(eldest)).Select(inscription => inscription.Title).Distinct().Order().ToList();

        Assert.Equal(["Here begin Liska's people", "Liska's people come to the land", "The coming of Liska's people"], titles);
    }

    [Fact]
    public void EveryWayOfCountingThemIsDrawn()
    {
        var lines = OverManyBands(eldest => Arrival(eldest, season: Season.Autumn)).Select(inscription => inscription.Lines[0]).Distinct().Order().ToList();

        Assert.Equal(
            [
                "Fifteen of them came to this land in the autumn: six men, six women and three children.",
                "In the autumn, fifteen came to this land: six men, six women and three children.",
                "They came in the autumn, fifteen in all: six men, six women and three children.",
            ],
            lines);
    }

    [Theory]
    [InlineData(1, 0, 0, "one man")]
    [InlineData(0, 1, 0, "one woman")]
    [InlineData(0, 0, 1, "one child")]
    [InlineData(2, 0, 0, "two men")]
    [InlineData(0, 2, 0, "two women")]
    [InlineData(0, 0, 2, "two children")]
    [InlineData(2, 3, 0, "two men and three women")]
    [InlineData(2, 0, 4, "two men and four children")]
    [InlineData(0, 3, 4, "three women and four children")]
    [InlineData(1, 1, 1, "one man, one woman and one child")]
    public void GroupsNobodyIsInAreLeftOut(int men, int women, int children, string expected)
    {
        var inscription = Prologue.Write(Arrival(men: men, women: women, children: children, people: men + women + children));

        Assert.EndsWith($": {expected}.", inscription.Lines[0]);
    }

    [Fact]
    public void TheEldestIsNamedWithTheirWintersAndTheirSex()
    {
        var her = OverManyBands(eldest => Arrival(eldest)).Select(inscription => inscription.Lines[1]).Distinct().Order().ToList();
        var his = Prologue.Write(Arrival(eldest: Eldest("Bran", Sex.Male))).Lines[1];

        Assert.Equal(
            [
                "Liska, who had seen nine winters, was the eldest among them, and the band took her name.",
                "The eldest among them was Liska, who had seen nine winters; they were her people.",
            ],
            her);
        Assert.Contains("Bran, who had seen nine winters", his);
        Assert.Contains(" his ", his);
        Assert.DoesNotContain(" her ", his);
    }

    [Theory]
    [InlineData(0, "who had not yet seen a winter")]
    [InlineData(1, "who had seen one winter")]
    [InlineData(2, "who had seen two winters")]
    [InlineData(23, "who had seen twenty-three winters")]
    public void TheEldestsWintersAreWrittenOut(int winters, string expected)
    {
        var inscription = Prologue.Write(Arrival(eldestWinters: winters));

        Assert.Contains(expected, inscription.Lines[1]);
    }

    [Fact]
    public void ABandThatKnowsNothingIsToldSo()
    {
        var ignorant = OverManyBands(eldest => Arrival(eldest)).Select(inscription => inscription.Lines[2]).Distinct().Order().ToList();
        var knowing = OverManyBands(eldest => Arrival(eldest, knowsAnything: true)).Select(inscription => inscription.Lines[2]).Distinct().Order().ToList();

        Assert.Equal(
            ["None of them knew how to feed themselves here.", "They knew nothing of this land, and nothing of how to live in it."],
            ignorant);
        Assert.Equal(
            ["Some of them knew a little; none of them knew enough.", "They brought a little knowledge with them, and would need more."],
            knowing);
    }

    [Fact]
    public void EveryClosingLineIsDrawn()
    {
        var closings = OverManyBands(eldest => Arrival(eldest)).Select(inscription => inscription.Lines[^1]).Distinct().Order().ToList();

        Assert.Equal(
            [
                "Their graves, if any are dug, will say the rest.",
                "They will learn only what somebody teaches them.",
                "What they do not learn, they will not live to pass on.",
            ],
            closings);
    }
}
