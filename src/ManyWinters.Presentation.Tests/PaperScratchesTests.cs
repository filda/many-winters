using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

// The scratched hatching under a page's stains: strokes drawn by hand, laid as a tile that has to
// meet itself edge to edge without a seam.
public class PaperScratchesTests
{
    private const int Salt = 12345;

    // The patch of a tile the coverage and pressure are measured over.
    private const int Size = 120;

    public static TheoryData<int, bool> EveryKindOfPage() => new()
    {
        { 5, true },
        { 5, false },
        { 6, true },
        { 6, false },
        { 7, true },
        { 7, false },
    };

    // Any seam would show as a straight line through every panel bigger than the tile.
    [Theory]
    [MemberData(nameof(EveryKindOfPage))]
    public void TheTileMeetsItselfWithoutASeam(int spacing, bool rising)
    {
        for (var i = 0; i < PaperScratches.Tile; i += 7)
        {
            for (var j = 0; j < 40; j++)
            {
                Assert.Equal(PaperScratches.InkAt(i, j, spacing, rising, Salt), PaperScratches.InkAt(i + PaperScratches.Tile, j, spacing, rising, Salt));
                Assert.Equal(PaperScratches.InkAt(j, i, spacing, rising, Salt), PaperScratches.InkAt(j, i + PaperScratches.Tile, spacing, rising, Salt));
            }
        }
    }

    // Faint enough to write over, and still hatching rather than a blank page or a filled one.
    [Theory]
    [MemberData(nameof(EveryKindOfPage))]
    public void ThePageIsHatchedButMostlyBare(int spacing, bool rising)
    {
        var inked = Patch(spacing, rising).Count(ink => ink > 0f);

        Assert.InRange(inked / (float)(Size * Size), 0.08f, 0.35f);
    }

    // A ruled line presses evenly and never lifts; a hand does both.
    [Theory]
    [MemberData(nameof(EveryKindOfPage))]
    public void TheStrokesVaryInPressure(int spacing, bool rising)
    {
        var pressures = Patch(spacing, rising).Where(ink => ink > 0f).Distinct().Count();

        Assert.True(pressures > 5);
        Assert.All(Patch(spacing, rising), ink => Assert.InRange(ink, 0f, 1f));
    }

    // Only the crossing course is pressed this lightly: it has to be there, and it has to stay
    // the lesser of the two, or the page turns from hatched to woven.
    [Theory]
    [MemberData(nameof(EveryKindOfPage))]
    public void ALighterCourseCrossesTheFirstInPlaces(int spacing, bool rising)
    {
        var inked = Patch(spacing, rising).Where(ink => ink > 0f).ToArray();
        var crossing = inked.Count(ink => ink < 0.5f);

        Assert.InRange(crossing / (float)inked.Length, 0.02f, 0.5f);
    }

    [Fact]
    public void TheStrokesLeanTheWayThePageSays()
    {
        Assert.NotEqual(Patch(6, rising: true), Patch(6, rising: false));
    }

    // The same page has to look the same every time it is opened.
    [Fact]
    public void TheSameSaltScratchesTheSamePage()
    {
        Assert.Equal(PaperScratches.Rgba(6, true, Salt), PaperScratches.Rgba(6, true, Salt));
        Assert.NotEqual(PaperScratches.Rgba(6, true, Salt), PaperScratches.Rgba(6, true, Salt + 1));
    }

    // The bytes the texture is built from: white ink carrying the stroke's pressure as alpha.
    [Fact]
    public void TheTileCarriesThePressureAsAlphaOnWhite()
    {
        var data = PaperScratches.Rgba(6, true, Salt);

        Assert.Equal(PaperScratches.Tile * PaperScratches.Tile * 4, data.Length);
        for (var y = 0; y < 30; y++)
        {
            for (var x = 0; x < 30; x++)
            {
                var index = ((y * PaperScratches.Tile) + x) * 4;
                Assert.Equal([255, 255, 255], data[index..(index + 3)]);
                Assert.Equal((byte)(PaperScratches.InkAt(x, y, 6, true, Salt) * 255f), data[index + 3]);
            }
        }
    }

    private static float[] Patch(int spacing, bool rising) =>
        [.. Enumerable.Range(0, Size * Size).Select(i => PaperScratches.InkAt(i % Size, i / Size, spacing, rising, Salt))];
}
