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
            Assert.InRange(spot.Lift, 0f, 1f);
        }
    }

    [Fact]
    public void LiftVariesBetweenSpots()
    {
        var lifts = Scatter().Select(spot => spot.Lift).ToList();

        Assert.True(lifts.Max() - lifts.Min() > 0.5f);
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

    [Fact]
    public void MinGapIsAFixedShareOfBothCloudsSizesCombined()
    {
        // Exact, not just "greater than either" - the gap is what caps how densely the cover
        // can pack, so the factor and the sum are both load-bearing.
        Assert.Equal(5.4f, CloudSpotScatter.MinGap(9f, 18f), 5);
        Assert.Equal(7.2f, CloudSpotScatter.MinGap(18f, 18f), 5);
    }

    [Fact]
    public void GenerateStopsExactlyAtTheTargetCount()
    {
        // 200m x 200m at an 11m mean spacing targets 330 spots and rejection sampling reaches
        // it here. Asserted exactly rather than as a range: the loop's two bounds (attempts
        // used up, target reached) are the only thing deciding when scattering stops.
        Assert.Equal(330, Scatter().Count);
    }

    [Fact]
    public void ALayoutForAGivenSeedIsFixedDownToEachSpotsOwnNumbers()
    {
        // Characterization: the scatter is a deterministic seeded generator whose output the
        // presentation layer places verbatim, so every arithmetic step between the seed and a
        // spot's fields is behaviour, not an implementation detail. Regenerate these numbers
        // deliberately if the generator is meant to change; don't relax them.
        var spots = Scatter();

        AssertSpot(spots[0], x: 74.251144f, z: 32.18773f, size: 12.448984f, texture: 0, roll: 0.34839553f, lift: 0.67616946f);
        AssertSpot(spots[1], x: 90.79375f, z: 68.71333f, size: 9.381816f, texture: 2, roll: 0.43212456f, lift: 0.9323158f);
        AssertSpot(spots[2], x: -77.74025f, z: 76.39087f, size: 13.003348f, texture: 1, roll: 0.7170906f, lift: 0.33534238f);
    }

    [Theory]
    [InlineData(0f, 0f, 7, 0.38902715f)]
    [InlineData(13.5f, -4.25f, 7, 0.29719213f)]
    [InlineData(-31f, 62f, 3, 0.17521806f)]
    [InlineData(5f, 5f, 0, 0.061756227f)]
    public void ClumpyRollsSpatialGrainIsAFixedFunctionOfPositionAndSeed(float x, float z, int seed, float expected)
    {
        // independent: 0 leaves only the spatial grain, so this pins the value noise and its
        // hash - the part that decides which patches of cover tear open together - rather than
        // the blend with the per-spot draw.
        Assert.Equal(expected, CloudSpotScatter.ClumpyRoll(x, z, independent: 0f, seed: seed), 6);
    }

    [Fact]
    public void ClumpyRollBlendsTheIndependentDrawInAtAFixedWeight()
    {
        // Same position and seed, two different independent draws: the difference between the
        // rolls is the independent share, so this pins the weight without restating the grain.
        var low = CloudSpotScatter.ClumpyRoll(0f, 0f, independent: 0f, seed: 7);
        var high = CloudSpotScatter.ClumpyRoll(0f, 0f, independent: 1f, seed: 7);

        Assert.Equal(0.4f, high - low, 5);
    }

    private static void AssertSpot(CloudSpot spot, float x, float z, float size, int texture, float roll, float lift)
    {
        Assert.Equal(x, spot.X, 4);
        Assert.Equal(z, spot.Z, 4);
        Assert.Equal(size, spot.Size, 4);
        Assert.Equal(texture, spot.TextureIndex);
        Assert.Equal(roll, spot.Roll, 6);
        Assert.Equal(lift, spot.Lift, 6);
    }

    [Fact]
    public void ScatteringStopsWhenTheAttemptBudgetRunsOutOnAMapThatCannotHoldTheTarget()
    {
        // A 40m map asked for 400 spots that a 5.4m-plus gap can never fit: the target is
        // unreachable, so the attempt budget is what ends the run. Exact, because that budget
        // and the way it is counted down are then the only things deciding the answer.
        var spots = CloudSpotScatter.Generate(halfExtentMeters: 20f, meanSpacingMeters: 2f, minSize: 9f, maxSize: 18f, textureCount: 3, seed: 7);

        Assert.Equal(53, spots.Count);
    }
}
