using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

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

    private static Heightmap NewMap() => new(Heights, gridSize: 3, cellSizeMeters: 20f);

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
}
