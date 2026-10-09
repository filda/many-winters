using Godot;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

public class WaterSurfaceTests
{
    // Inside distances on a 6x6 grid: two bodies of water, one 2x2 in the top-left corner and
    // one on the right whose top vertex joins the rest only diagonally. Positive inside,
    // negative outside.
    private static readonly float[,] TwoBodies =
    {
        { 1.5f, 0.75f, -1f, -3f, 0.25f, -0.5f },
        { 0.5f, 1.25f, -1f, -3f, -1f, 0.5f },
        { -1f, -1f, -2f, -3f, -1f, 1.75f },
        { -3f, -3f, -3f, -3f, -2f, -1f },
        { -5f, -5f, -5f, -5f, -5f, -5f },
        { -5f, -5f, -5f, -5f, -5f, -5f },
    };

    [Fact]
    public void EachBodyLiesAtItsOwnLowestGround()
    {
        var levels = WaterSurface.AreaLevels(TwoBodies, Ground, reachCells: 1);

        // Left body: ground over (0,0) (0,1) (1,0) (1,1) is 10, 13, 7.5, 11.
        Assert.Equal(7.5f, levels[0, 0]);
        Assert.Equal(7.5f, levels[1, 1]);

        // Right body: (0,4) (1,5) (2,5) is 22, 25, 25.
        Assert.Equal(22f, levels[1, 5]);
        Assert.Equal(22f, levels[2, 5]);
    }

    [Fact]
    public void ADiagonalStepJoinsTheSameBody()
    {
        // (0,4) touches (1,5) only diagonally; apart, (1,5) and (2,5) would lie at 25.
        var levels = WaterSurface.AreaLevels(TwoBodies, Ground, reachCells: 1);

        Assert.Equal(22f, levels[0, 4]);
        Assert.Equal(22f, levels[2, 5]);
    }

    [Fact]
    public void VerticesWithinReachOutsideTakeTheNearbyBodysLevel()
    {
        var levels = WaterSurface.AreaLevels(TwoBodies, Ground, reachCells: 1);

        Assert.Equal(7.5f, levels[2, 0]);
        Assert.Equal(22f, levels[3, 5]);
        Assert.True(float.IsNaN(levels[4, 2]));
        Assert.True(float.IsNaN(levels[3, 2]));
    }

    [Fact]
    public void AWiderReachCarriesTheLevelFurtherOut()
    {
        var levels = WaterSurface.AreaLevels(TwoBodies, Ground, reachCells: 2);

        Assert.Equal(7.5f, levels[3, 1]);
        Assert.True(float.IsNaN(levels[5, 2]));
    }

    [Fact]
    public void NoWaterLeavesEveryLevelUnset()
    {
        var dry = new float[3, 3];
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                dry[row, col] = -0.5f - row - col;
            }
        }

        var levels = WaterSurface.AreaLevels(dry, Ground, reachCells: 2);

        Assert.True(float.IsNaN(levels[1, 1]));
        Assert.True(float.IsNaN(levels[0, 2]));
    }

    [Fact]
    public void ARiverNeverRisesTowardItsMouth()
    {
        Assert.Equal([9f, 7.5f, 7.5f, 6.25f, 4f], WaterSurface.Descending([9f, 7.5f, 8.25f, 6.25f, 4f]));
    }

    [Fact]
    public void ARiverDrawnAgainstItsFlowStillRunsDownhillToItsLowerEnd()
    {
        Assert.Equal([3f, 5.5f, 7.75f, 7.75f, 10f], WaterSurface.Descending([3f, 5.5f, 8.25f, 7.75f, 10f]));
    }

    [Fact]
    public void AnEmptyRiverHasNoProfile()
    {
        Assert.Empty(WaterSurface.Descending([]));
    }

    [Fact]
    public void UnderWaterTheBedDeepensToItsFullDepthOverTheShelf()
    {
        Assert.Equal(12.25f, WaterSurface.Ceiling(12.25f, 0f, 5f), 4);
        Assert.Equal(12.25f - (0.5f * 1.2f / 3f), WaterSurface.Ceiling(12.25f, 1.2f, 5f), 4);
        Assert.Equal(11.75f, WaterSurface.Ceiling(12.25f, 3f, 5f), 4);
        Assert.Equal(11.75f, WaterSurface.Ceiling(12.25f, 4.4f, 5f), 4);
    }

    [Fact]
    public void BesideTheWaterTheBankRisesAtHalfTheDistance()
    {
        Assert.Equal(12.25f + 0.65f, WaterSurface.Ceiling(12.25f, -1.3f, 5f), 4);
        Assert.Equal(12.25f + 2.15f, WaterSurface.Ceiling(12.25f, -4.3f, 5f), 4);
    }

    [Fact]
    public void BeyondReachTheWaterSetsNoLimit()
    {
        Assert.Equal(float.PositiveInfinity, WaterSurface.Ceiling(12.25f, -5f, 5f));
        Assert.Equal(float.PositiveInfinity, WaterSurface.Ceiling(12.25f, -7.5f, 5f));
    }

    [Fact]
    public void TheFingerprintNamesEveryTuningValue()
    {
        Assert.Equal(string.Join('|', 0.5f, 3f, 0.5f), WaterSurface.Fingerprint);
    }

    [Fact]
    public void APointBesideASegmentMeasuresToItsFootThere()
    {
        // From (2, 1) toward (10, 7), a 3-4-5 direction 10 long; (8, 1) projects 4.8 along it,
        // 3.6 off it.
        var (distance, along) = WaterSurface.ToSegment(new Vector2(8f, 1f), new Vector2(2f, 1f), new Vector2(10f, 7f));

        Assert.Equal(3.6f, distance, 4);
        Assert.Equal(0.48f, along, 4);
    }

    [Fact]
    public void APointPastAnEndMeasuresToThatEnd()
    {
        var (distance, along) = WaterSurface.ToSegment(new Vector2(13f, 11f), new Vector2(2f, 1f), new Vector2(10f, 7f));

        Assert.Equal(5f, distance, 4);
        Assert.Equal(1f, along);

        var (before, beforeAlong) = WaterSurface.ToSegment(new Vector2(-1f, -3f), new Vector2(2f, 1f), new Vector2(10f, 7f));
        Assert.Equal(5f, before, 4);
        Assert.Equal(0f, beforeAlong);
    }

    [Fact]
    public void ADegenerateSegmentMeasuresToItsPoint()
    {
        var (distance, along) = WaterSurface.ToSegment(new Vector2(5f, 5f), new Vector2(2f, 1f), new Vector2(2f, 1f));

        Assert.Equal(5f, distance, 4);
        Assert.Equal(0f, along);
    }

    private static float Ground(int row, int col) => 10f + (row * -2.5f) + (col * 3f) + (row * col * 0.5f);
}
