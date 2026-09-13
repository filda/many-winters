using ManyWinters.Core.Continuity;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Continuity;

public class EpitaphTests
{
    // Enough different deaths to draw every variant of every sentence at least once.
    private const int ManySeeds = 200;

    private static Person Dead(string name, Sex sex, DeathCause cause = DeathCause.Hunger, int idSeed = 1) =>
        new()
        {
            Id = TestIds.Person(idSeed),
            Name = name,
            BirthTick = -600,
            IsAlive = false,
            DeathTick = 1000,
            CauseOfDeath = cause,
            Mother = Person.Unknown,
            Father = Person.Unknown,
            Sex = sex,
        };

    private static BandEnding Ending(
        BandFate fate = BandFate.Ended,
        Person? lastToDie = null,
        int wintersSeen = 9,
        Season season = Season.Winter,
        int survivors = 0,
        int born = 4,
        int graves = 11,
        int markedGraves = 4,
        int unburied = 1,
        BandFate? sideThatEndedFirst = null,
        int wintersKeptAfterwards = 0) =>
        new()
        {
            Fate = fate,
            BandName = "Liska's people",
            LastToDie = lastToDie ?? Dead("Odo", Sex.Male),
            EndingTick = 1000,
            SeasonOfEnding = season,
            WintersSeen = wintersSeen,
            Survivors = survivors,
            Born = born,
            Graves = graves,
            MarkedGraves = markedGraves,
            Unburied = unburied,
            SideThatEndedFirst = sideThatEndedFirst,
            WintersKeptAfterwards = wintersKeptAfterwards,
        };

    private static IEnumerable<Inscription> OverManyDeaths(Func<Person, BandEnding> ending, Sex sex = Sex.Male, DeathCause cause = DeathCause.Hunger) =>
        Enumerable.Range(1, ManySeeds).Select(seed => Epitaph.Write(ending(Dead("Odo", sex, cause, seed))));

    private static IEnumerable<string> AllText(Inscription inscription) => inscription.Lines.Prepend(inscription.Title);

    // What every carved sentence has to look like, whichever variant was drawn: a sentence.
    // An empty phrase slotted into a template leaves a double space or a dangling comma, so
    // this is also what catches a variant that says nothing.
    private static void AssertReadsAsASentence(string line)
    {
        Assert.False(string.IsNullOrWhiteSpace(line));
        Assert.True(char.IsUpper(line[0]), $"Does not start with a capital: '{line}'");
        Assert.EndsWith(".", line);
        Assert.DoesNotContain("  ", line);
        Assert.DoesNotContain(" .", line);
        Assert.DoesNotContain(" ,", line);
        Assert.DoesNotContain(" ;", line);
        Assert.DoesNotContain(",.", line);
    }

    [Fact]
    public void ALivingBandHasNoEpitaph()
    {
        var error = Assert.Throws<ArgumentException>(() => Epitaph.Write(Ending(fate: BandFate.Living)));

        Assert.Contains("living band", error.Message);
    }

    [Fact]
    public void TheSameEndingReadsTheSameEveryTime()
    {
        var ending = Ending();

        var first = Epitaph.Write(ending);
        var second = Epitaph.Write(ending);

        Assert.Equal(first.Title, second.Title);
        Assert.Equal(first.Lines, second.Lines);
    }

    [Fact]
    public void EverySentenceOfAnEndedBandReadsAsOne()
    {
        foreach (var inscription in OverManyDeaths(last => Ending(lastToDie: last, sideThatEndedFirst: BandFate.SpearSideEnded, wintersKeptAfterwards: 6)))
        {
            Assert.All(AllText(inscription), AssertReadsAsASentence);
        }
    }

    [Fact]
    public void EverySentenceOfAnEndedLineReadsAsOne()
    {
        foreach (var inscription in OverManyDeaths(last => Ending(fate: BandFate.SpearSideEnded, lastToDie: last, survivors: 3)))
        {
            Assert.All(AllText(inscription), AssertReadsAsASentence);
        }

        foreach (var inscription in OverManyDeaths(last => Ending(fate: BandFate.SpindleSideEnded, lastToDie: last, survivors: 3), Sex.Female, DeathCause.OldAge))
        {
            Assert.All(AllText(inscription), AssertReadsAsASentence);
        }
    }

    [Fact]
    public void EveryWayOfSayingHowManyWintersReadsAsASentence()
    {
        foreach (var winters in new[] { 0, 1, 2, 30 })
        {
            foreach (var inscription in OverManyDeaths(last => Ending(lastToDie: last, wintersSeen: winters)))
            {
                Assert.All(AllText(inscription), AssertReadsAsASentence);
            }
        }
    }

    [Fact]
    public void EveryWayOfSayingHowManyWereBornReadsAsASentence()
    {
        foreach (var born in new[] { 0, 1, 7 })
        {
            foreach (var inscription in OverManyDeaths(last => Ending(lastToDie: last, born: born)))
            {
                Assert.All(AllText(inscription), AssertReadsAsASentence);
            }
        }
    }

    [Fact]
    public void AnEndedBandIsWrittenInFull()
    {
        var inscription = Epitaph.Write(Ending(sideThatEndedFirst: BandFate.SpearSideEnded, wintersKeptAfterwards: 6));

        Assert.Equal("Here ends the line of Liska's people.", inscription.Title);
        Assert.Equal(
            [
                "Nine winters Liska's people endured.",
                "For six winters after the last man died, the women kept the fire.",
                "In the winter, Odo, the last of them, starved.",
                "Four children were born to them.",
                "Eleven lie in the ground; four of the graves bear a name.",
                "Odo lies where he fell.",
                "So much the stones still tell.",
            ],
            inscription.Lines);
    }

    [Fact]
    public void ASpearSideEndingIsWrittenInFull()
    {
        var inscription = Epitaph.Write(Ending(fate: BandFate.SpearSideEnded, survivors: 3));

        Assert.Equal("The spear side of Liska's people is ended.", inscription.Title);
        Assert.Equal(
            [
                "The last man of Liska's people is dead.",
                "In the winter, Odo died hungry.",
                "Three women are left. No child will be born to them.",
                "The line has ended on the spear side.",
            ],
            inscription.Lines);
    }

    [Fact]
    public void ASpindleSideEndingSpeaksOfWomenAndMen()
    {
        var inscription = Epitaph.Write(Ending(fate: BandFate.SpindleSideEnded, lastToDie: Dead("Sela", Sex.Female, DeathCause.OldAge), survivors: 3));

        Assert.Contains("spindle side", inscription.Title);
        Assert.Contains(inscription.Lines, line => line.Contains("woman") && line.Contains("Liska's people"));
        Assert.Contains("Three men are left. None of them will father a child.", inscription.Lines);
        Assert.Contains(inscription.Lines, line => line.Contains("spindle side"));
    }

    [Fact]
    public void ASingleSurvivorIsSpokenOfInTheSingular()
    {
        var women = Epitaph.Write(Ending(fate: BandFate.SpearSideEnded, survivors: 1));
        var men = Epitaph.Write(Ending(fate: BandFate.SpindleSideEnded, lastToDie: Dead("Sela", Sex.Female), survivors: 1));

        Assert.Contains("One woman is left; no child will be born to her.", women.Lines);
        Assert.Contains("One man is left; he will father no child.", men.Lines);
    }

    // A line nobody was ever on (see BandEnding.LastToDie) has no death to tell of.
    [Fact]
    public void ALineWithNoLastDeathSkipsTheDeath()
    {
        var inscription = Epitaph.Write(Ending(fate: BandFate.SpearSideEnded, survivors: 2) with { LastToDie = null });

        Assert.Equal(3, inscription.Lines.Count);
        Assert.All(AllText(inscription), AssertReadsAsASentence);
        Assert.DoesNotContain(inscription.Lines, line => line.Contains("Odo"));
    }

    [Fact]
    public void TheLastDeathIsToldByItsCauseAndSeason()
    {
        foreach (var inscription in OverManyDeaths(last => Ending(lastToDie: last, season: Season.Autumn)))
        {
            var death = Assert.Single(inscription.Lines, line => line.Contains("Odo") && line.Contains("autumn"));
            Assert.Contains("last of them", death);
            Assert.Contains("hungry", death.Replace("starved", "hungry").Replace("nothing to eat", "hungry"));
        }

        foreach (var inscription in OverManyDeaths(last => Ending(lastToDie: last, season: Season.Spring), Sex.Female, DeathCause.OldAge))
        {
            var death = Assert.Single(inscription.Lines, line => line.Contains("Odo") && line.Contains("spring"));
            Assert.True(death.Contains("old") || death.Contains("years"), death);
            Assert.DoesNotContain("hungry", death);
            Assert.DoesNotContain("starved", death);
        }
    }

    [Fact]
    public void EveryWayOfDyingIsDrawnAcrossManyDeaths()
    {
        var hungry = OverManyDeaths(last => Ending(lastToDie: last)).SelectMany(inscription => inscription.Lines).ToList();
        var old = OverManyDeaths(last => Ending(lastToDie: last), cause: DeathCause.OldAge).SelectMany(inscription => inscription.Lines).ToList();

        Assert.Contains(hungry, line => line.Contains("starved"));
        Assert.Contains(hungry, line => line.Contains("died hungry"));
        Assert.Contains(hungry, line => line.Contains("died with nothing to eat"));
        Assert.Contains(old, line => line.Contains("died old"));
        Assert.Contains(old, line => line.Contains("died full of years"));
        Assert.Contains(old, line => line.Contains("died of nothing but years"));
    }

    [Fact]
    public void AWomanIsSpokenOfAsShe()
    {
        var inscription = Epitaph.Write(Ending(lastToDie: Dead("Sela", Sex.Female, DeathCause.OldAge)));

        Assert.Contains("Sela lies where she fell.", inscription.Lines);
        var women = OverManyDeaths(last => Ending(lastToDie: last), Sex.Female, DeathCause.OldAge).SelectMany(AllText).ToList();
        Assert.All(women, line =>
        {
            AssertReadsAsASentence(line);
            Assert.DoesNotContain(" he ", line);
            Assert.DoesNotContain(" his ", line);
        });
        Assert.Contains(women, line => line.Contains(" she "));
    }

    [Fact]
    public void WintersAreCountedInWords()
    {
        var none = OverManyDeaths(last => Ending(lastToDie: last, wintersSeen: 0)).Select(inscription => inscription.Lines[0]).Distinct().ToList();
        var one = OverManyDeaths(last => Ending(lastToDie: last, wintersSeen: 1)).Select(inscription => inscription.Lines[0]).Distinct().ToList();
        var many = OverManyDeaths(last => Ending(lastToDie: last, wintersSeen: 23)).Select(inscription => inscription.Lines[0]).Distinct().ToList();

        Assert.Equal(3, none.Count);
        Assert.All(none, line => Assert.Contains("winter", line.Replace("snow", "winter")));
        Assert.Equal(3, one.Count);
        Assert.All(one, line => Assert.Contains("One winter", line.Replace("a single winter", "One winter")));
        Assert.Equal(3, many.Count);
        Assert.All(many, line => Assert.Contains("twenty-three winters", line.ToLowerInvariant()));
    }

    [Fact]
    public void TheWintersKeptAfterwardsAreOnlyMentionedWhenThereWereAny()
    {
        var none = Epitaph.Write(Ending(sideThatEndedFirst: BandFate.SpearSideEnded, wintersKeptAfterwards: 0));
        var one = OverManyDeaths(last => Ending(lastToDie: last, sideThatEndedFirst: BandFate.SpindleSideEnded, wintersKeptAfterwards: 1)).ToList();
        var six = OverManyDeaths(last => Ending(lastToDie: last, sideThatEndedFirst: BandFate.SpearSideEnded, wintersKeptAfterwards: 6)).ToList();

        Assert.Equal(6, none.Lines.Count);
        Assert.All(one, inscription => Assert.Contains(inscription.Lines, line => line.Contains("one winter ") && line.Contains("last woman") && line.Contains("men")));
        Assert.All(six, inscription => Assert.Contains(inscription.Lines, line => line.Contains("six winters") && line.Contains("last man") && line.Contains("women")));
        Assert.Equal(2, one.Select(inscription => inscription.Lines[1]).Distinct().Count());
    }

    [Fact]
    public void ChildrenAreCountedInWords()
    {
        var none = OverManyDeaths(last => Ending(lastToDie: last, born: 0)).Select(inscription => inscription.Lines[2]).Distinct().ToList();
        var one = OverManyDeaths(last => Ending(lastToDie: last, born: 1)).Select(inscription => inscription.Lines[2]).Distinct().ToList();
        var many = OverManyDeaths(last => Ending(lastToDie: last, born: 12)).Select(inscription => inscription.Lines[2]).Distinct().ToList();

        Assert.Equal(2, none.Count);
        Assert.All(none, line => Assert.Contains("child", line));
        Assert.Equal(2, one.Count);
        Assert.All(one, line => Assert.Contains("child was born", line));
        Assert.Equal(2, many.Count);
        Assert.All(many, line => Assert.Contains("twelve children", line.ToLowerInvariant()));
    }

    [Theory]
    [InlineData(0, 0, "None of them was laid in the ground.")]
    [InlineData(1, 0, "One lies in the ground, and no grave bears a name.")]
    [InlineData(1, 1, "One lies in the ground, and the grave bears a name.")]
    [InlineData(5, 0, "Five lie in the ground, and no grave bears a name.")]
    [InlineData(5, 5, "Five lie in the ground, and every grave bears a name.")]
    [InlineData(5, 1, "Five lie in the ground; one of the graves bears a name.")]
    [InlineData(5, 3, "Five lie in the ground; three of the graves bear a name.")]
    public void TheGravesAreCountedExactly(int graves, int marked, string expected)
    {
        var inscription = Epitaph.Write(Ending(graves: graves, markedGraves: marked));

        Assert.Contains(expected, inscription.Lines);
    }

    [Fact]
    public void TheUnburiedAreCounted()
    {
        var one = Epitaph.Write(Ending(unburied: 1));
        var several = Epitaph.Write(Ending(unburied: 3));

        Assert.Contains("Odo lies where he fell.", one.Lines);
        Assert.Contains("Three lie unburied where they fell, Odo among them.", several.Lines);
    }

    [Fact]
    public void WithNoNamedGraveNobodyIsLeftToSayWhoTheyWere()
    {
        var nameless = OverManyDeaths(last => Ending(lastToDie: last, markedGraves: 0)).Select(inscription => inscription.Lines[^1]).Distinct().ToList();
        var remembered = OverManyDeaths(last => Ending(lastToDie: last, markedGraves: 4)).Select(inscription => inscription.Lines[^1]).Distinct().ToList();

        Assert.Equal(2, nameless.Count);
        Assert.Contains("Nobody is left who could say who they were.", nameless);
        Assert.Contains("Their names went into the ground with them.", nameless);
        Assert.Equal(2, remembered.Count);
        Assert.Contains("This much the graves remember.", remembered);
        Assert.Contains("So much the stones still tell.", remembered);
    }

    // "Their names went into the ground with them" needs a ground somebody was put into.
    [Fact]
    public void WithNoGraveAtAllTheNamesDidNotGoIntoTheGround()
    {
        var closing = OverManyDeaths(last => Ending(lastToDie: last, graves: 0, markedGraves: 0, unburied: 11)).Select(inscription => inscription.Lines[^1]).Distinct().ToList();

        Assert.Equal(["Nobody is left who could say who they were."], closing);
    }

    [Fact]
    public void EveryTitleAndOpeningLineOfAnEndedLineIsDrawn()
    {
        var spear = OverManyDeaths(last => Ending(fate: BandFate.SpearSideEnded, lastToDie: last, survivors: 2)).ToList();

        Assert.Equal(
            ["No man is left to Liska's people.", "The spear side of Liska's people is ended."],
            spear.Select(inscription => inscription.Title).Distinct().Order().ToList());
        Assert.Equal(
            ["No man remains among Liska's people.", "The last man of Liska's people is dead."],
            spear.Select(inscription => inscription.Lines[0]).Distinct().Order().ToList());
        Assert.Equal(
            ["On the spear side the line is ended.", "The line has ended on the spear side."],
            spear.Select(inscription => inscription.Lines[^1]).Distinct().Order().ToList());
    }

    [Fact]
    public void EveryTitleOfAnEndedBandIsDrawn()
    {
        var titles = OverManyDeaths(last => Ending(lastToDie: last)).Select(inscription => inscription.Title).Distinct().Order().ToList();

        Assert.Equal(["Here ends the line of Liska's people.", "Liska's people are no more.", "The last of Liska's people."], titles);
    }

    [Fact]
    public void EveryWayOfTellingTheLastDeathIsDrawn()
    {
        var deaths = OverManyDeaths(last => Ending(lastToDie: last, born: 0, markedGraves: 0)).Select(inscription => inscription.Lines[1]).Distinct().ToList();

        Assert.Contains(deaths, line => line.StartsWith("In the winter, Odo, the last of them,", StringComparison.Ordinal));
        Assert.Contains(deaths, line => line.StartsWith("Odo was the last of them. In the winter he ", StringComparison.Ordinal));
        Assert.Contains(deaths, line => line.StartsWith("The last of them was Odo, who ", StringComparison.Ordinal));

        // Three shapes times three ways of starving.
        Assert.Equal(9, deaths.Count);
    }
}
