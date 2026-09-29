using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class CloudSpotScatterTests
{
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
    public void NoTwoSpotsAreCloserThanAFifthOfTheirCombinedSizeAndTheTightestPairSitsAtThatGap()
    {
        // The gap is a fixed share of both sizes combined. On a saturated map some pair lands
        // right at it, so the tightest pair pins the share from both sides.
        var spots = Scatter();
        var tightest = float.MaxValue;

        for (var i = 0; i < spots.Count; i++)
        {
            for (var j = i + 1; j < spots.Count; j++)
            {
                tightest = MathF.Min(tightest, Distance(spots[i], spots[j]) / (spots[i].Size + spots[j].Size));
            }
        }

        Assert.InRange(tightest, 0.2f, 0.21f);
    }

    [Fact]
    public void FillsTheMapToRoughlyTheRequestedDensity()
    {
        // 200m x 200m at an 11m mean spacing asks for ~330 spots; rejection sampling should
        // land near that, not leave the map half empty.
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
    public void NeighbouringSpotsRollMoreAlikeThanDistantOnes()
    {
        // Rolls carry a shared spatial grain, so neighbours differ mostly by their own draw
        // while distant spots differ by the grain as well - that shared part is what makes
        // whole patches of cover drop out together.
        var spots = Scatter();
        var near = new List<float>();
        var far = new List<float>();

        for (var i = 0; i < spots.Count; i++)
        {
            for (var j = i + 1; j < spots.Count; j++)
            {
                var distance = Distance(spots[i], spots[j]);
                var difference = MathF.Abs(spots[i].Roll - spots[j].Roll);
                if (distance < 8f)
                {
                    near.Add(difference);
                }
                else if (distance > 60f)
                {
                    far.Add(difference);
                }
            }
        }

        // Independent rolls would put both averages at the same ~1/3; the grain pulls the near
        // one down to about 0.7 of the far one.
        Assert.True(near.Count > 100);
        Assert.True(near.Average() < 0.8f * far.Average(), $"near {near.Average()} vs far {far.Average()}");
    }

    [Fact]
    public void LayoutIsNotAGrid()
    {
        // A jittered grid keeps nearest-neighbour distances tightly clustered; a Poisson-disc
        // scatter varies them.
        var spots = Scatter();
        var nearest = new List<float>();
        for (var i = 0; i < spots.Count; i++)
        {
            var best = float.MaxValue;
            for (var j = 0; j < spots.Count; j++)
            {
                if (i == j) continue;
                best = MathF.Min(best, Distance(spots[i], spots[j]));
            }

            nearest.Add(best);
        }

        Assert.True(nearest.Max() - nearest.Min() > 4f);
    }

    [Fact]
    public void GenerateStopsExactlyAtTheTargetCount()
    {
        // The 330 target is reachable here, so it is the bound that stops the loop rather than
        // the attempt budget. Exact, because those two bounds alone decide the count.
        Assert.Equal(330, Scatter().Count);
    }

    [Fact]
    public void ALayoutForAGivenSeedIsFixedDownToEachSpotsOwnNumbers()
    {
        // Characterization: the presentation layer places these numbers verbatim, so every step
        // from seed to spot is behaviour. Regenerate deliberately if the generator changes;
        // don't relax.
        var spots = Scatter();

        AssertSpot(spots[0], x: 74.251144f, z: 32.18773f, size: 12.448984f, texture: 0, roll: 0.34839553f, lift: 0.67616946f);
        AssertSpot(spots[1], x: 90.79375f, z: 68.71333f, size: 9.381816f, texture: 2, roll: 0.43212456f, lift: 0.9323158f);
        AssertSpot(spots[2], x: -77.74025f, z: 76.39087f, size: 13.003348f, texture: 1, roll: 0.7170906f, lift: 0.33534238f);
    }

    [Fact]
    public void ScatteringStopsWhenTheAttemptBudgetRunsOutOnAMapThatCannotHoldTheTarget()
    {
        // A 40m map cannot hold 400 spots at a 5.4m-plus gap, so the attempt budget ends the
        // run. Exact, because that budget alone then decides the count.
        var spots = CloudSpotScatter.Generate(
            20f,
            2f,
            9f,
            18f,
            3,
            7,
            0.2f,
            6,
            22f,
            0.6f
            );

        Assert.Equal(53, spots.Count);
    }

    private static IReadOnlyList<CloudSpot> Scatter(int seed = 7) =>
        CloudSpotScatter.Generate(
            100f,
            11f,
            9f,
            18f,
            3,
            seed,
            0.2f,
            6,
            22f,
            0.6f
            );

    private static float Distance(CloudSpot a, CloudSpot b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
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
}
