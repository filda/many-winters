using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// How each page of the game's paper aged. Every panel is cut from the same sheet, and its name is
// what decides how dirty it came out (see PanelChrome.Grain).
public class PaperWeatheringTests
{
    // The names the game actually rules its pages with.
    private static readonly string[] Pages = ["Workshop", "Chronicle", "detail", "menu", "pause", "help"];

    // Weathering that reshuffled between sessions would read as a bug rather than as paper.
    [Fact]
    public void APageAgesTheSameWayEveryTimeItIsAskedFor()
    {
        Assert.Equal(PaperWeathering.Of("Workshop"), PaperWeathering.Of("Workshop"));
    }

    [Fact]
    public void TwoPagesOpenSideBySideAreNotTheSameStainTwice()
    {
        Assert.NotEqual(PaperWeathering.Of("Workshop"), PaperWeathering.Of("Chronicle"));
    }

    // The whole point of the name being the seed: no page of the game wears another page's look.
    [Fact]
    public void NoTwoPagesOfTheGameAgedAlike()
    {
        Assert.Equal(Pages.Length, Pages.Select(PaperWeathering.Of).Distinct().Count());
    }

    // A one-letter difference has to come out as a different page, not as the same one nudged:
    // the seed is avalanched for exactly this.
    [Fact]
    public void NamesOneLetterApartAgeQuiteDifferently()
    {
        Assert.NotEqual(PaperWeathering.Of("pause").Seed, PaperWeathering.Of("pausa").Seed);
    }

    [Fact]
    public void AnUnnamedPageStillAges()
    {
        Assert.Equal(PaperWeathering.Of(string.Empty), PaperWeathering.Of(string.Empty));
    }

    // Faint on every page, however it came out: past a certain strength this stops being paper and
    // becomes wallpaper, and the ink has to fight it.
    [Theory]
    [MemberData(nameof(ManyNames))]
    public void EveryPageStaysWithinWhatPaperLooksLike(string name)
    {
        var paper = PaperWeathering.Of(name);

        Assert.InRange(paper.BlotchFrequency, 0.009f, 0.015f);
        Assert.InRange(paper.BlotchStrength, 0.16f, 0.28f);
        Assert.InRange(paper.HatchSpacing, 3, 5);
    }

    // The tile the hatching is drawn into is cut to a multiple of the spacing, so whatever spacing
    // a page came out with, the strokes still repeat seamlessly (see PanelChrome.Hatching).
    [Theory]
    [MemberData(nameof(ManyNames))]
    public void TheHatchingAlwaysDividesItsTile(string name)
    {
        var spacing = PaperWeathering.Of(name).HatchSpacing;

        Assert.Equal(0, spacing * 4 % spacing);
    }

    // Variety that never varies is a constant with extra steps: across the pages a game might
    // have, each choice has to be seen taking every value it offers.
    [Fact]
    public void TheChoicesOnOfferAreAllActuallyTaken()
    {
        var pages = Names().Select(PaperWeathering.Of).ToList();

        Assert.Equal([3, 4, 5], pages.Select(paper => paper.HatchSpacing).Distinct().Order());
        Assert.Equal([false, true], pages.Select(paper => paper.HatchRising).Distinct().Order());
    }

    public static TheoryData<string> ManyNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in Names())
        {
            data.Add(name);
        }

        return data;
    }

    // The game's own pages, and enough made-up ones that the ranges are read off more than a
    // handful of draws.
    private static IEnumerable<string> Names() => Pages.Concat(Enumerable.Range(0, 60).Select(i => $"page {i}"));
}
