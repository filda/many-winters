using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class GridDistanceFieldTests
{
    [Fact]
    public void FlaggedCellsAreAtDistanceZero()
    {
        var targets = new bool[3, 3];
        targets[1, 1] = true;

        var distance = GridDistanceField.DistanceToNearestTrue(targets);

        Assert.Equal(0f, distance[1, 1]);
    }

    [Fact]
    public void OrthogonalNeighboursAreOneCellAway()
    {
        var targets = new bool[3, 3];
        targets[1, 1] = true;

        var distance = GridDistanceField.DistanceToNearestTrue(targets);

        Assert.Equal(1f, distance[0, 1]);
        Assert.Equal(1f, distance[2, 1]);
        Assert.Equal(1f, distance[1, 0]);
        Assert.Equal(1f, distance[1, 2]);
    }

    [Fact]
    public void DiagonalNeighboursAreRootTwoAway()
    {
        var targets = new bool[3, 3];
        targets[1, 1] = true;

        var distance = GridDistanceField.DistanceToNearestTrue(targets);

        Assert.Equal(MathF.Sqrt(2f), distance[0, 0], 5);
        Assert.Equal(MathF.Sqrt(2f), distance[2, 2], 5);
        Assert.Equal(MathF.Sqrt(2f), distance[0, 2], 5);
        Assert.Equal(MathF.Sqrt(2f), distance[2, 0], 5);
    }

    [Fact]
    public void DistanceGrowsAlongAStraightLineAwayFromTheOnlyTarget()
    {
        var targets = new bool[1, 6];
        targets[0, 0] = true;

        var distance = GridDistanceField.DistanceToNearestTrue(targets);

        for (var x = 0; x < 6; x++)
        {
            Assert.Equal(x, distance[0, x]);
        }
    }

    [Fact]
    public void EveryCellTakesTheNearestOfSeveralTargets()
    {
        var targets = new bool[1, 7];
        targets[0, 0] = true;
        targets[0, 6] = true;

        var distance = GridDistanceField.DistanceToNearestTrue(targets);

        Assert.Equal(2f, distance[0, 2]);
        Assert.Equal(3f, distance[0, 3]);
        Assert.Equal(2f, distance[0, 4]);
    }

    [Fact]
    public void BackwardSweepReachesCellsAboveAndLeftOfTheTarget()
    {
        var targets = new bool[4, 4];
        targets[3, 3] = true;

        var distance = GridDistanceField.DistanceToNearestTrue(targets);

        Assert.Equal(3f, distance[0, 3]);
        Assert.Equal(3f, distance[3, 0]);
        Assert.Equal(3f * MathF.Sqrt(2f), distance[0, 0], 5);
    }

    [Fact]
    public void ChamferStaysCloseToEuclideanDistance()
    {
        var targets = new bool[20, 20];
        targets[0, 0] = true;

        var distance = GridDistanceField.DistanceToNearestTrue(targets);

        // (10, 5) is 11.18 cells away as the crow flies; a 1 / sqrt2 chamfer overestimates
        // such off-axis distances by a few percent at most.
        var euclidean = MathF.Sqrt((10f * 10f) + (5f * 5f));
        Assert.InRange(distance[5, 10], euclidean, euclidean * 1.09f);
    }

    [Fact]
    public void WithNoTargetsEveryCellIsUnreachable()
    {
        var distance = GridDistanceField.DistanceToNearestTrue(new bool[3, 3]);

        foreach (var value in distance)
        {
            Assert.Equal(GridDistanceField.Unreachable, value);
        }
    }
}
