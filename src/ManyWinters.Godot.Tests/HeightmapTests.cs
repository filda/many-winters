using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// A deliberately lopsided 3x3 grid at 20m spacing: no row or column repeats, so a swapped
// index or a mirrored axis cannot come out right by accident. Rows count along +Z, columns
// along +X, and the grid is centred on the origin - so the map runs -20 to +20 both ways.
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
        // above the lowest ground, not above the sea.
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
        // The grid rises by 10 across a row and by 30 down a column, so mixing the two axes
        // up gives a visibly different answer rather than a subtly wrong one.
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
        // Deliberately in the *second* cell along X and part-way down Z: a point on a row or
        // column boundary leaves one of the two blends weighted zero, so the other row's own
        // weight never gets checked at all.
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
        // Fixed values rather than a comparison against `RawAt + BumpAt`, which would restate
        // the implementation and hold just as well if the two were subtracted. Both points sit
        // off the noise's own lattice, where a bump of exactly zero hides the sign entirely -
        // which is what every corner of this grid happens to be.
        Assert.Equal(expected, NewMap().FineVertexAt(row, col), 4);
    }

    [Fact]
    public void TheWalkableHeightAgreesWithTheMeshVertexUnderneathIt()
    {
        // The whole reason HeightAt interpolates the fine vertices rather than the formula
        // behind them: asked for exactly a vertex's position, it has to answer that vertex, or
        // people float above and sink into ground drawn flat beneath them.
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
        // Water tracks the raw elevation so a river does not come out choppy; anything
        // standing on the ground tracks the bumped one.
        var map = NewMap();

        Assert.NotEqual(map.RawAt(3f, -7f), map.HeightAt(3f, -7f));
    }

    [Fact]
    public void TheBumpRollsBothUpAndDownRatherThanOnlyLiftingTheGround()
    {
        // Fbm's own range is 0 to 1; without remapping it first, every point would only ever
        // be nudged upward and the whole terrain would rise by half the amplitude.
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
        // Sign alone says nothing about scale: a bump scaled down fourfold still rolls both
        // ways, it just stops being visible. Two metres of amplitude should actually produce
        // metre-scale variation.
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
        // Worth stating rather than tripping over: the noise passes through zero at whole
        // multiples of its wavelength, so a test sampling only there measures nothing at all.
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
        // A cached mesh is served on this, so it has to be stable within a build and carry
        // each piece of tuning rather than being an opaque constant.
        var fingerprint = Heightmap.ShapeFingerprint;

        Assert.Equal(fingerprint, Heightmap.ShapeFingerprint);
        Assert.Equal(5, fingerprint.Split('|').Length);
        Assert.DoesNotContain(fingerprint.Split('|'), part => part.Length == 0);
    }
}
