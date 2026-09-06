using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class BoxBlurTests
{
    private static float[,] Grid(int size, Func<int, int, float> value)
    {
        var grid = new float[size, size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                grid[y, x] = value(x, y);
            }
        }

        return grid;
    }

    [Fact]
    public void AFlatGridStaysFlat()
    {
        // Every window averages the same value, edges included - so a constant field is a
        // fixed point of the blur. Anything else means the edge clamp is losing mass.
        var blurred = BoxBlur.Blur(Grid(7, (_, _) => 0.5f), radius: 2);

        for (var y = 0; y < 7; y++)
        {
            for (var x = 0; x < 7; x++)
            {
                Assert.Equal(0.5f, blurred[y, x], 5);
            }
        }
    }

    [Fact]
    public void ARadiusOfZeroLeavesTheGridAlone()
    {
        var source = Grid(4, (x, y) => (x * 4) + y);

        var blurred = BoxBlur.Blur(source, radius: 0);

        Assert.Equal(source, blurred);
    }

    [Fact]
    public void ASinglePointSpreadsOverTheWholeWindowInBothDirections()
    {
        // One lit texel in the middle of a 5x5 grid with radius 1: separable means it becomes
        // a 3x3 block, each cell 1/9 of the original - the horizontal pass spreads it across
        // three columns, the vertical across three rows.
        var source = new float[5, 5];
        source[2, 2] = 9f;

        var blurred = BoxBlur.Blur(source, radius: 1);

        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 3; x++)
            {
                Assert.Equal(1f, blurred[y, x], 5);
            }
        }

        Assert.Equal(0f, blurred[0, 0], 5);
        Assert.Equal(0f, blurred[2, 0], 5);
        Assert.Equal(0f, blurred[4, 4], 5);
    }

    [Fact]
    public void SamplingPastTheEdgeClampsInsteadOfWrapping()
    {
        // A bright left column must not bleed into the right-hand edge. Wrapping (which the
        // fog texture's sampler has disabled) would fold the far side of the map into this one.
        var source = Grid(5, (x, _) => x == 0 ? 1f : 0f);

        var blurred = BoxBlur.Blur(source, radius: 1);

        Assert.Equal(0f, blurred[2, 4], 5);
        Assert.True(blurred[2, 0] > blurred[2, 1]);
    }

    [Fact]
    public void ClampingWeightsTheEdgeTowardTheEdgeCellItself()
    {
        // At x = 0 the window's two off-grid samples clamp back onto column 0, so the corner
        // keeps three parts of its own value out of the window's three - it does not fade
        // toward an imaginary zero outside the grid.
        var source = Grid(3, (x, _) => x == 0 ? 3f : 0f);

        var blurred = BoxBlur.Blur(source, radius: 1);

        Assert.Equal(2f, blurred[1, 0], 5);
    }

    [Fact]
    public void TotalBrightnessIsPreservedAwayFromTheEdges()
    {
        var source = new float[9, 9];
        source[4, 4] = 1f;

        var blurred = BoxBlur.Blur(source, radius: 2);

        var total = 0f;
        foreach (var value in blurred)
        {
            total += value;
        }

        Assert.Equal(1f, total, 4);
    }
}
