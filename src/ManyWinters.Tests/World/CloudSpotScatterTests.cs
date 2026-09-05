using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class CloudSpotScatterTests
{
    private static IReadOnlyList<CloudSpot> Scatter(int seed = 7) =>
        CloudSpotScatter.Generate(halfExtentMeters: 100f, meanSpacingMeters: 11f, minSize: 9f, maxSize: 18f, textureCount: 3, seed: seed);

    [Fact]
    public void EverySpotLiesInsideTheMap()
    {
        foreach (var spot in Scatter())
        {
            Assert.InRange(spot.X, -100f, 100f);
            Assert.InRange(spot.Z, -100f, 100f);
        }
    }

    [Fact]
    public void SizesTextureIndicesAndRollsStayInTheirRanges()
    {
        foreach (var spot in Scatter())
        {
            Assert.InRange(spot.Size, 9f, 18f);
            Assert.InRange(spot.TextureIndex, 0, 2);
            Assert.InRange(spot.Roll, 0f, 1f);
        }
    }

    [Fact]
    public void NoTwoSpotsAreCloserThanTheirSizeDerivedGap()
    {
        var spots = Scatter();

        for (var i = 0; i < spots.Count; i++)
        {
            for (var j = i + 1; j < spots.Count; j++)
            {
                var gap = CloudSpotScatter.MinGap(spots[i].Size, spots[j].Size);
                var dx = spots[i].X - spots[j].X;
                var dz = spots[i].Z - spots[j].Z;
                Assert.True(MathF.Sqrt((dx * dx) + (dz * dz)) >= gap, $"spots {i} and {j} overlap");
            }
        }
    }

    [Fact]
    public void FillsTheMapToRoughlyTheRequestedDensity()
    {
        // 200m x 200m at an 11m mean spacing asks for ~330 spots; rejection sampling won't
        // hit that exactly, but it should land in the same neighbourhood rather than
        // leaving the map half empty.
        var count = Scatter().Count;

        Assert.InRange(count, 200, 331);
    }

    [Fact]
    public void SameSeedGivesTheSameLayoutAndDifferentSeedsDoNot()
    {
        var a = Scatter(seed: 3);
        var b = Scatter(seed: 3);
        var c = Scatter(seed: 4);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void ClumpyRollStaysInRangeAndVariesWithPosition()
    {
        var rolls = new List<float>();
        for (var x = -100f; x <= 100f; x += 5f)
        {
            var roll = CloudSpotScatter.ClumpyRoll(x, 0f, independent: 0.5f, seed: 7);
            Assert.InRange(roll, 0f, 1f);
            rolls.Add(roll);
        }

        Assert.True(rolls.Max() - rolls.Min() > 0.2f);
    }

    [Fact]
    public void ClumpyRollChangesSmoothlyBetweenNeighbours()
    {
        // Two spots a metre apart share most of their spatial grain, so with the same
        // independent chance their rolls stay close - that shared part is what makes whole
        // patches of cover drop out together.
        var a = CloudSpotScatter.ClumpyRoll(10f, 10f, independent: 0.5f, seed: 7);
        var b = CloudSpotScatter.ClumpyRoll(11f, 10f, independent: 0.5f, seed: 7);

        Assert.True(MathF.Abs(a - b) < 0.1f);
    }

    [Fact]
    public void LayoutIsNotAGrid()
    {
        // On a jittered grid every spot stays inside its own cell, so nearest-neighbour
        // distances cluster tightly; a Poisson-disc scatter spreads them out. Check that the
        // spacing to the nearest neighbour genuinely varies across the layout.
        var spots = Scatter();
        var nearest = new List<float>();
        for (var i = 0; i < spots.Count; i++)
        {
            var best = float.MaxValue;
            for (var j = 0; j < spots.Count; j++)
            {
                if (i == j) continue;
                var dx = spots[i].X - spots[j].X;
                var dz = spots[i].Z - spots[j].Z;
                best = MathF.Min(best, MathF.Sqrt((dx * dx) + (dz * dz)));
            }

            nearest.Add(best);
        }

        Assert.True(nearest.Max() - nearest.Min() > 4f);
    }
}
