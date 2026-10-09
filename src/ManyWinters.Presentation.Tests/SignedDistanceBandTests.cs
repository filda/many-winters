using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

// A 7x7 grid whose inside is the vertices with col >= 4 - an edge between columns 3 and 4.
// The exact distance is a lopsided function of both indices, so a value taken from the wrong
// vertex, or a row and column swapped, cannot match by accident.
public class SignedDistanceBandTests
{
    private const int Size = 7;
    private const float Far = 4.5f;

    [Fact]
    public void VerticesBesideTheEdgeTakeTheExactDistance()
    {
        var distances = Build(bandCells: 1);

        Assert.Equal(Exact(1, 3), distances[1, 3], 5);
        Assert.Equal(Exact(6, 4), distances[6, 4], 5);
        Assert.Equal(Exact(0, 3), distances[0, 3], 5);
    }

    [Fact]
    public void VerticesBeyondTheBandTakeFarWithTheirSign()
    {
        var distances = Build(bandCells: 1);

        Assert.Equal(-Far, distances[2, 2]);
        Assert.Equal(-Far, distances[5, 0]);
        Assert.Equal(Far, distances[1, 5]);
    }

    [Fact]
    public void AWiderBandReachesThatManyCellsEachWay()
    {
        var distances = Build(bandCells: 2);

        Assert.Equal(Exact(3, 2), distances[3, 2], 5);
        Assert.Equal(Exact(4, 5), distances[4, 5], 5);
        Assert.Equal(-Far, distances[3, 1]);
        Assert.Equal(Far, distances[4, 6]);
    }

    [Fact]
    public void ADiagonalNeighbourAcrossTheEdgeCountsToo()
    {
        // Only (3, 3) is inside: (2, 2) and (4, 4) touch it diagonally, (1, 1) does not.
        var distances = SignedDistanceBand.Build(Size, (row, col) => row == 3 && col == 3, Exact, 1, Far);

        Assert.Equal(Exact(2, 2), distances[2, 2], 5);
        Assert.Equal(Exact(4, 4), distances[4, 4], 5);
        Assert.Equal(-Far, distances[1, 1]);
        Assert.Equal(-Far, distances[5, 1]);
    }

    [Fact]
    public void AnExactDistanceIsClampedToFar()
    {
        var distances = SignedDistanceBand.Build(Size, (_, col) => col >= 4, (_, col) => col >= 4 ? 7.25 : -6.5, 1, Far);

        Assert.Equal(Far, distances[2, 4]);
        Assert.Equal(-Far, distances[2, 3]);
    }

    [Fact]
    public void ATriangleTouchesTheFeatureWhenAnyCornerIsInside()
    {
        Assert.True(SignedDistanceBand.Touches(0.75f, -1.5f, -2.25f, 0f));
        Assert.True(SignedDistanceBand.Touches(-1.5f, 0.75f, -2.25f, 0f));
        Assert.True(SignedDistanceBand.Touches(-1.5f, -2.25f, 0.75f, 0f));
        Assert.False(SignedDistanceBand.Touches(-0.5f, -1.5f, -2.25f, 0f));
        Assert.False(SignedDistanceBand.Touches(0f, -1.5f, 0f, 0f));
    }

    [Fact]
    public void ATriangleTouchesTheFeatureWhenACornerIsWithinReachOutside()
    {
        Assert.True(SignedDistanceBand.Touches(-1.25f, -3.5f, -2.75f, 1.5f));
        Assert.True(SignedDistanceBand.Touches(-3.5f, -2.75f, -1.25f, 1.5f));
        Assert.False(SignedDistanceBand.Touches(-1.75f, -3.5f, -2.75f, 1.5f));
    }

    private static float[,] Build(int bandCells) => SignedDistanceBand.Build(Size, (_, col) => col >= 4, Exact, bandCells, Far);

    private static double Exact(int row, int col) => (col - 3.5) + (row * 0.125);
}
