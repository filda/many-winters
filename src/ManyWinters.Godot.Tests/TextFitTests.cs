using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// How large a title can be set and still fit on one line. A band is named after its oldest
// member, so the same epitaph is a different width every game.
public class TextFitTests
{
    // Ten pixels a point, so the arithmetic in each case is obvious.
    private static Func<int, float> TenPixelsPerPoint => size => size * 10f;

    [Fact]
    public void TextThatAlreadyFitsIsLeftAtTheLargestSize()
    {
        Assert.Equal(60, TextFit.LargestThatFits(TenPixelsPerPoint, maxSize: 60, minSize: 30, available: 1000f));
    }

    [Fact]
    public void TextTooWideIsSetAtTheLargestSizeThatFits()
    {
        Assert.Equal(45, TextFit.LargestThatFits(TenPixelsPerPoint, maxSize: 60, minSize: 30, available: 450f));
    }

    // Exactly filling the room is fitting: a line flush to the edge is what the fraction of the
    // screen was there to allow for.
    [Fact]
    public void TextExactlyAsWideAsTheRoomFits()
    {
        Assert.Equal(50, TextFit.LargestThatFits(TenPixelsPerPoint, maxSize: 50, minSize: 30, available: 500f));
    }

    // A title shrunk past reading is no longer a title, so the floor wins over the fit.
    [Fact]
    public void TextThatFitsAtNoSizeIsSetAtTheFloor()
    {
        Assert.Equal(30, TextFit.LargestThatFits(TenPixelsPerPoint, maxSize: 60, minSize: 30, available: 10f));
    }

    [Fact]
    public void TheFloorIsUsedWhenItIsTheOnlySizeThatFits()
    {
        Assert.Equal(30, TextFit.LargestThatFits(TenPixelsPerPoint, maxSize: 60, minSize: 30, available: 300f));
    }

    // Nothing below the floor is ever asked about, so no measurement is wasted on a size that
    // could not be used.
    [Fact]
    public void NoSizeBelowTheFloorIsMeasured()
    {
        var measured = new List<int>();

        TextFit.LargestThatFits(
            size =>
            {
                measured.Add(size);
                return size * 10f;
            },
            maxSize: 34,
            minSize: 30,
            available: 10f);

        Assert.Equal([34, 33, 32, 31], measured);
    }

    [Fact]
    public void AFloorEqualToTheLargestSizeMeasuresNothingAtAll()
    {
        var measured = 0;

        var size = TextFit.LargestThatFits(_ => { measured++; return 1f; }, maxSize: 30, minSize: 30, available: 1000f);

        Assert.Equal(30, size);
        Assert.Equal(0, measured);
    }

    // A real font's width per size is not quite proportional (hinting, kerning), so the search
    // walks down rather than solving for a size - and takes the first that fits even where a
    // larger one would have.
    [Fact]
    public void TheSearchStopsAtTheFirstSizeThatFitsWalkingDown()
    {
        var widths = new Dictionary<int, float> { [60] = 900f, [59] = 400f, [58] = 890f };

        Assert.Equal(59, TextFit.LargestThatFits(size => widths[size], maxSize: 60, minSize: 30, available: 500f));
    }
}
