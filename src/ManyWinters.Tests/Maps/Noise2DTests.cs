using ManyWinters.Core.Maps;

namespace ManyWinters.Tests.Maps;

public class Noise2DTests
{
    // MapLoader's own biome-field seed, so the golden values below pin the exact field the
    // default map is generated from rather than an arbitrary one.
    private const int Seed = 7;

    // Coordinates spread across several lattice cells, including negative ones (this world's
    // origin sits in the middle of the terrain, not at a corner) and a pair far from it.
    private static readonly (double X, double Y)[] Samples =
    [
        (0.25, 0.75), (0.5, 0.5), (12.8, -7.35), (-45.2, 91.7), (1000.4, -1000.6),
    ];

    private static IEnumerable<(double X, double Y)> Grid()
    {
        for (var i = -60; i <= 60; i++)
        {
            for (var j = -60; j <= 60; j++)
            {
                yield return (i * 0.37, j * 0.41);
            }
        }
    }

    [Fact]
    public void ValueAtKeepsItsExactShapeForAGivenSeed()
    {
        // The default map's whole layout (biome bands, where anything grows at all) is a
        // function of this field, so a change in the noise silently reshapes everyone's world.
        // Pinned to twelve decimals rather than exactly: the gradients come out of Math.Cos/Sin,
        // whose last bit isn't guaranteed identical across platforms, and no plausible change to
        // the algorithm hides that far down.
        var noise = new Noise2D(Seed);

        var expected = new[]
        {
            0.7009210586547852, 0.625, 0.5676990632139496, 0.24604624698769473, 0.19860695497333114,
        };

        for (var i = 0; i < Samples.Length; i++)
        {
            Assert.Equal(expected[i], noise.ValueAt(Samples[i].X, Samples[i].Y), 12);
        }
    }

    [Fact]
    public void FbmKeepsItsExactShapeForAGivenSeed()
    {
        var noise = new Noise2D(Seed);

        // Three octaves at MapLoader's own biome frequency - the call it actually makes.
        var expected = new[]
        {
            0.5041346322766164, 0.5027549569280356, 0.45207486096549915, 0.6367791933572347,
            0.44496435914702115,
        };

        for (var i = 0; i < Samples.Length; i++)
        {
            Assert.Equal(expected[i], noise.Fbm(Samples[i].X, Samples[i].Y, 3, 1.0 / 220.0), 12);
        }
    }

    [Fact]
    public void TheSameSeedProducesTheSameFieldTwice()
    {
        var first = new Noise2D(Seed);
        var second = new Noise2D(Seed);

        Assert.All(Grid(), p => Assert.Equal(first.ValueAt(p.X, p.Y), second.ValueAt(p.X, p.Y)));
    }

    [Fact]
    public void DifferentSeedsProduceDifferentFields()
    {
        // MapLoader runs two independent fields (density and biome) side by side and expects
        // them to disagree - one field used twice would tie "does anything grow here" to
        // "what grows here", which is exactly the correlation the two seeds exist to avoid.
        var first = new Noise2D(Seed);
        var second = new Noise2D(Seed + 1);

        Assert.Contains(Grid(), p => Math.Abs(first.ValueAt(p.X, p.Y) - second.ValueAt(p.X, p.Y)) > 0.1);
    }

    [Fact]
    public void ValueAtStaysWithinTheUnitInterval()
    {
        var noise = new Noise2D(Seed);

        Assert.All(Grid(), p =>
        {
            var value = noise.ValueAt(p.X, p.Y);
            Assert.InRange(value, 0.0, 1.0);
        });
    }

    [Fact]
    public void ValueAtUsesTheWholeRangeInsteadOfBunchingInTheMiddle()
    {
        // Perlin's 2D bound (1/sqrt(2)) is what the raw value gets divided by before the
        // remap - normalizing by anything larger would leave every sample stuck near 0.5,
        // and biome bands drawn over such a field would never reach their outer thresholds.
        var noise = new Noise2D(Seed);
        var values = Grid().Select(p => noise.ValueAt(p.X, p.Y)).ToList();

        Assert.True(values.Min() < 0.1, $"Lowest sample was {values.Min()}, expected the field to reach near 0.");
        Assert.True(values.Max() > 0.9, $"Highest sample was {values.Max()}, expected the field to reach near 1.");
    }

    [Fact]
    public void ValueAtNeverSaturatesAgainstTheClamp()
    {
        // The clamp is a safety net for floating-point drift at the bound, not a working part
        // of the remap: a field that actually hits 0 or 1 means the normalization is wrong.
        var noise = new Noise2D(Seed);

        Assert.All(Grid(), p =>
        {
            var value = noise.ValueAt(p.X, p.Y);
            Assert.True(value is > 0.0 and < 1.0, $"Sample at ({p.X}, {p.Y}) saturated at {value}.");
        });
    }

    [Fact]
    public void ValueAtIsExactlyOneHalfOnEveryLatticePoint()
    {
        // Gradient noise (unlike the value noise this started as) has all four corner dot
        // products fall to zero on a lattice point, so the field crosses its midpoint there.
        // This is the property that keeps the extrema at arbitrary points inside a cell
        // instead of grid-aligned on the lattice, which is what would read as a waffle bias.
        var noise = new Noise2D(Seed);

        for (var x = -5; x <= 5; x++)
        {
            for (var y = -5; y <= 5; y++)
            {
                Assert.Equal(0.5, noise.ValueAt(x, y), 12);
            }
        }
    }

    [Fact]
    public void ValueAtChangesGraduallyBetweenNeighbouringPoints()
    {
        // Coherence is the whole point of this over independent per-point randomness: a walk
        // across several cells - crossing cell boundaries, where two cells' independently
        // oriented gradients disagree most - must not jump.
        var noise = new Noise2D(Seed);
        var previous = noise.ValueAt(-20, 0.3);

        for (var i = 1; i <= 8000; i++)
        {
            var value = noise.ValueAt(-20 + (i * 0.005), 0.3);
            Assert.True(
                Math.Abs(value - previous) < 0.02,
                $"Field jumped by {Math.Abs(value - previous)} over a 0.005 step near x = {-20 + (i * 0.005)}.");
            previous = value;
        }
    }

    [Fact]
    public void ValueAtVariesAlongBothAxes()
    {
        // A degenerate gradient set (every lattice point handed the same vector) still looks
        // like noise along one axis while being perfectly constant along the other.
        var noise = new Noise2D(Seed);

        var alongX = Enumerable.Range(0, 50).Select(i => noise.ValueAt(i * 0.63, 0.2)).Distinct().Count();
        var alongY = Enumerable.Range(0, 50).Select(i => noise.ValueAt(0.2, i * 0.63)).Distinct().Count();

        Assert.True(alongX > 1, "The field is constant along X.");
        Assert.True(alongY > 1, "The field is constant along Y.");
    }

    [Fact]
    public void FbmSumsItsOctavesAtDoublingFrequencyAndDecayingAmplitude()
    {
        var noise = new Noise2D(Seed);
        const double frequency = 1.0 / 140.0;
        const double persistence = 0.4;
        const double x = 31.5;
        const double y = -12.25;

        var expected =
            ((noise.ValueAt(x * frequency, y * frequency) * 1.0)
                + (noise.ValueAt(x * frequency * 2, y * frequency * 2) * persistence)
                + (noise.ValueAt(x * frequency * 4, y * frequency * 4) * persistence * persistence))
            / (1.0 + persistence + (persistence * persistence));

        Assert.Equal(expected, noise.Fbm(x, y, 3, frequency, persistence), 12);
    }

    [Fact]
    public void FbmWithASingleOctaveIsJustOneSampleAtThatFrequency()
    {
        var noise = new Noise2D(Seed);
        const double frequency = 1.0 / 220.0;

        Assert.Equal(noise.ValueAt(31.5 * frequency, -12.25 * frequency), noise.Fbm(31.5, -12.25, 1, frequency), 12);
    }

    [Fact]
    public void FbmHalvesTheAmplitudePerOctaveUnlessToldOtherwise()
    {
        var noise = new Noise2D(Seed);

        Assert.Equal(noise.Fbm(31.5, -12.25, 4, 0.01, 0.5), noise.Fbm(31.5, -12.25, 4, 0.01), 12);
    }

    [Fact]
    public void FbmPersistenceChangesHowMuchTheFinerOctavesCount()
    {
        var noise = new Noise2D(Seed);

        Assert.NotEqual(noise.Fbm(31.5, -12.25, 4, 0.01, 0.2), noise.Fbm(31.5, -12.25, 4, 0.01, 0.9), 12);
    }

    [Fact]
    public void FbmStaysWithinTheUnitInterval()
    {
        // Every octave is already in [0, 1] and the weights are normalized by their own sum,
        // so the sum can't escape the range the biome bands are drawn over.
        var noise = new Noise2D(Seed);

        Assert.All(Grid(), p => Assert.InRange(noise.Fbm(p.X * 20, p.Y * 20, 3, 1.0 / 140.0), 0.0, 1.0));
    }
}
