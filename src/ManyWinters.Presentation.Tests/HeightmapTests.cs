using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

// A lopsided 3x3 grid at 20m spacing: no row or column repeats, so a swapped index or mirrored
// axis cannot come out right by accident. Rows run along +Z, columns along +X, centred on the
// origin, so the map spans -20 to +20 both ways.
public class HeightmapTests
{
    private static readonly float[][] Heights =
    [
        [10f, 20f, 30f],
        [40f, 50f, 60f],
        [70f, 80f, 90f],
    ];

    [Fact]
    public void TheMapSpansHalfItsWidthEachWayFromTheOrigin()
    {
        // Three samples 20m apart is 40m across, so 20m in each direction.
        Assert.Equal(20f, NewMap().HalfExtentMeters, 5);
    }

    [Fact]
    public void TheLowestAndHighestSamplesAreFound()
    {
        var map = NewMap();

        Assert.Equal(10f, map.MinHeight, 5);
        Assert.Equal(90f, map.MaxHeight, 5);
    }

    [Fact]
    public void TheGroundIsShiftedSoItsLowestPointSitsAtZero()
    {
        // Real elevations are hundreds of metres above sea level; the world works in metres
        // above the lowest ground.
        Assert.Equal(0f, NewMap().RawAt(-20f, -20f), 4);
    }

    [Fact]
    public void EachSourceSampleIsReadBackExactlyAtItsOwnCorner()
    {
        var map = NewMap();

        // (x, z) of each grid vertex, against its own height minus the shift.
        Assert.Equal(0f, map.RawAt(-20f, -20f), 4);
        Assert.Equal(20f, map.RawAt(20f, -20f), 4);
        Assert.Equal(60f, map.RawAt(-20f, 20f), 4);
        Assert.Equal(80f, map.RawAt(20f, 20f), 4);
    }

    [Fact]
    public void ColumnsRunAlongXAndRowsAlongZNotTheOtherWayAround()
    {
        // Rows rise by 10 across, columns by 30 down, so mixing the axes up is visibly wrong.
        var map = NewMap();

        Assert.Equal(10f, map.RawAt(0f, -20f), 4);
        Assert.Equal(30f, map.RawAt(-20f, 0f), 4);
    }

    [Fact]
    public void BetweenSamplesTheHeightIsInterpolatedNotSnapped()
    {
        // Halfway between the -20 and 0 columns on the top row: between 0 and 10.
        Assert.Equal(5f, NewMap().RawAt(-10f, -20f), 4);
    }

    [Fact]
    public void APointInsideACellIsInterpolatedOnBothAxesAtOnce()
    {
        // In the second cell along X and part-way down Z: on a row or column boundary one blend
        // weight is zero and the other row's weight goes unchecked.
        Assert.Equal(38f, NewMap().RawAt(5f, -3f), 4);
    }

    [Fact]
    public void PastTheEdgeTheGroundFlattensRatherThanWrappingOrFalling()
    {
        var map = NewMap();

        Assert.Equal(map.RawAt(-20f, -20f), map.RawAt(-500f, -500f), 4);
        Assert.Equal(map.RawAt(20f, 20f), map.RawAt(500f, 500f), 4);
    }

    [Fact]
    public void TheFineGridSubdividesEverySourceCellByTheSameFactor()
    {
        var map = NewMap();

        // Two source cells across, each split ten ways, plus the closing vertex.
        Assert.Equal(21, map.FineGridSize);
        Assert.Equal(2f, map.FineCellSize, 5);
        Assert.Equal(map.HalfExtentMeters * 2f, (map.FineGridSize - 1) * map.FineCellSize, 4);
    }

    [Theory]
    [InlineData(3, 1, 9.515872f)]
    [InlineData(8, 6, 29.197924f)]
    public void AFineVertexIsItsElevationWithTheBumpAddedOnTop(int row, int col, float expected)
    {
        // Fixed values rather than `RawAt + BumpAt`, which would also hold if the two were
        // subtracted. Both points sit off the noise lattice, where the bump is exactly zero.
        Assert.Equal(expected, NewMap().FineVertexAt(row, col), 4);
    }

    [Fact]
    public void TheWalkableHeightAgreesWithTheMeshVertexUnderneathIt()
    {
        // HeightAt interpolates the fine vertices rather than the formula behind them: at a
        // vertex it must answer that vertex, or people float above or sink into the mesh.
        var map = NewMap();
        foreach (var (row, col) in new[] { (0, 0), (3, 7), (10, 10), (20, 20) })
        {
            var x = (col * map.FineCellSize) - map.HalfExtentMeters;
            var z = (row * map.FineCellSize) - map.HalfExtentMeters;

            Assert.Equal(map.FineVertexAt(row, col), map.HeightAt(x, z), 3);
        }
    }

    [Fact]
    public void TheWalkableHeightIncludesTheBumpAndTheRawHeightDoesNot()
    {
        // Water tracks the raw elevation so a river is not choppy; anything standing on the
        // ground tracks the bumped one.
        var map = NewMap();

        Assert.NotEqual(map.RawAt(3f, -7f), map.HeightAt(3f, -7f));
    }

    [Fact]
    public void TheBumpRollsBothUpAndDownRatherThanOnlyLiftingTheGround()
    {
        // Fbm's range is 0 to 1; without remapping, the bump would only ever lift the ground.
        var above = 0;
        var below = 0;
        for (var x = -200f; x <= 200f; x += 3.7f)
        {
            if (Heightmap.BumpAt(x, x * 0.37f) > 0f)
            {
                above++;
            }
            else
            {
                below++;
            }
        }

        Assert.True(above > 0 && below > 0, $"{above} above, {below} below");
    }

    [Fact]
    public void TheBumpReachesAMeaningfulFractionOfItsAmplitude()
    {
        // Sign alone says nothing about scale: two metres of amplitude must produce metre-scale
        // variation.
        var largest = 0f;
        for (var x = -200f; x <= 200f; x += 0.31f)
        {
            largest = MathF.Max(largest, MathF.Abs(Heightmap.BumpAt(x, x * 0.37f)));
        }

        Assert.True(largest > 0.8f, $"largest bump was only {largest}");
        Assert.True(largest <= 2f, $"and it must stay inside the amplitude, was {largest}");
    }

    [Fact]
    public void TheBumpIsZeroOnItsOwnNoiseLattice()
    {
        // The noise passes through zero at whole multiples of its wavelength, so a test
        // sampling only there measures nothing.
        Assert.Equal(0f, Heightmap.BumpAt(-20f, -20f), 5);
        Assert.Equal(0f, Heightmap.BumpAt(10f, 0f), 5);
    }

    [Fact]
    public void TheBumpIsTheSameEveryRunSoTerrainDoesNotReshuffle()
    {
        Assert.Equal(Heightmap.BumpAt(13.5f, -7.25f), Heightmap.BumpAt(13.5f, -7.25f));
    }

    [Fact]
    public void TheShapeFingerprintNamesEveryNumberThatChangesTheGround()
    {
        // A cached mesh is keyed on this, so it must be stable and carry every piece of tuning.
        var fingerprint = Heightmap.ShapeFingerprint;

        Assert.Equal(fingerprint, Heightmap.ShapeFingerprint);
        Assert.Equal(5, fingerprint.Split('|').Length);
        Assert.DoesNotContain(fingerprint.Split('|'), part => part.Length == 0);
    }

    [Fact]
    public void ACeilingHoldsAFineVertexDownOnlyWhereItIsBelowTheGround()
    {
        var map = NewMap();
        var ceiling = new float[map.FineGridSize, map.FineGridSize];
        for (var row = 0; row < map.FineGridSize; row++)
        {
            for (var col = 0; col < map.FineGridSize; col++)
            {
                ceiling[row, col] = float.PositiveInfinity;
            }
        }

        ceiling[3, 1] = 4.25f;
        ceiling[8, 6] = 31.5f;
        var carved = map.WithCeiling(ceiling);

        Assert.Equal(4.25f, carved.FineVertexAt(3, 1), 4);
        Assert.Equal(29.197924f, carved.FineVertexAt(8, 6), 4);
        Assert.Equal(map.FineVertexAt(12, 5), carved.FineVertexAt(12, 5), 4);
    }

    [Fact]
    public void TheWalkableHeightFollowsTheCarvedVertices()
    {
        var map = NewMap();
        var ceiling = new float[map.FineGridSize, map.FineGridSize];
        for (var row = 0; row < map.FineGridSize; row++)
        {
            for (var col = 0; col < map.FineGridSize; col++)
            {
                ceiling[row, col] = -1.5f;
            }
        }

        Assert.Equal(-1.5f, map.WithCeiling(ceiling).HeightAt(3.7f, -6.1f), 4);
    }

    [Fact]
    public void ACarvedMapKeepsTheSourcesExtentAndElevationRange()
    {
        var map = NewMap();
        var carved = map.WithCeiling(new float[map.FineGridSize, map.FineGridSize]);

        Assert.Equal(map.HalfExtentMeters, carved.HalfExtentMeters);
        Assert.Equal(map.MinHeight, carved.MinHeight);
        Assert.Equal(map.MaxHeight, carved.MaxHeight);
        Assert.Equal(map.FineGridSize, carved.FineGridSize);
        Assert.Equal(map.RawAt(5f, -3f), carved.RawAt(5f, -3f));
    }

    [Fact]
    public void TheSlopeIsTheRiseOverRunOfTheRealElevation()
    {
        // The grid is a plane: 10 m per 20 m along X, 30 m per 20 m along Z, so the slope is
        // sqrt(0.5^2 + 1.5^2) wherever a step either way stays on the map - at the centre, the
        // steps land exactly on the edges.
        Assert.Equal(MathF.Sqrt(2.5f), NewMap().SlopeAt(0f, 0f), 4);
    }

    [Fact]
    public void TheSlopeEasesOffWhereTheStepRunsPastTheEdge()
    {
        // At x = 12 the step east lands at 32, clamped to the edge at 20 (60 m), and the step
        // west at -8 (46 m): 14 m over the 40 m the difference assumes.
        var slope = NewMap().SlopeAt(12f, 0f);

        Assert.Equal(MathF.Sqrt((0.35f * 0.35f) + (1.5f * 1.5f)), slope, 4);
    }

    private static Heightmap NewMap() => new(Heights, gridSize: 3, cellSizeMeters: 20f);
}
