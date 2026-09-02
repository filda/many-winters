namespace ManyWinters.Core.Maps;

// Coherent 2D gradient (Perlin) noise: a smooth, continuous field instead of independent
// randomness per sampled point - lets things like "which biome grows here" or "how the
// ground rolls" have organic, spatially-correlated shapes rather than picking a center
// point and drawing an explicit circle around it. A seeded lattice of unit gradient
// vectors (not scalar values - that would be the simpler "value noise", which this
// started as) dotted against the offset to each sampled point and blended with Perlin's
// own quintic fade curve between the four surrounding grid corners. The extra step over
// value noise matters here: value noise's peaks/valleys sit exactly on lattice points, which
// reads as a faint grid-aligned "waffle" bias once you know to look for it; gradient
// noise's extrema fall at arbitrary points inside a cell instead, which is what actually
// looks natural.
public sealed class Noise2D
{
    // Perlin's own bound for 2D noise built from unit gradients is |value| <= 1/sqrt(2) -
    // dividing by it before remapping to [0, 1] uses the full output range instead of only
    // ever landing in the middle third of it.
    private const double MaxAmplitude = 0.7071067811865476;

    private static readonly (double X, double Y)[] Gradients = BuildGradients();

    private readonly int[] _permutation;

    public Noise2D(int seed)
    {
        var rng = new Random(seed);
        var source = new int[256];
        for (var i = 0; i < 256; i++)
        {
            source[i] = i;
        }

        for (var i = 255; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (source[i], source[j]) = (source[j], source[i]);
        }

        _permutation = new int[512];
        for (var i = 0; i < 512; i++)
        {
            _permutation[i] = source[i & 255];
        }
    }

    // A single octave, in [0, 1].
    public double ValueAt(double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var xf = x - x0;
        var yf = y - y0;

        var d00 = DotGradient(x0, y0, xf, yf);
        var d10 = DotGradient(x0 + 1, y0, xf - 1, yf);
        var d01 = DotGradient(x0, y0 + 1, xf, yf - 1);
        var d11 = DotGradient(x0 + 1, y0 + 1, xf - 1, yf - 1);

        var u = Fade(xf);
        var v = Fade(yf);

        var top = d00 + ((d10 - d00) * u);
        var bottom = d01 + ((d11 - d01) * u);
        var raw = top + ((bottom - top) * v);

        var normalized = Math.Clamp(raw / MaxAmplitude, -1.0, 1.0);
        return (normalized + 1.0) / 2.0;
    }

    // Fractal Brownian motion: several octaves at doubling frequency and (by default)
    // halving amplitude, summed together - large-scale shape comes from the low
    // frequencies, finer local variation layers on top from the higher ones, so a region
    // reads as organic rather than one uniform blob size. Stays in [0, 1] (every octave
    // already is, and the weights are normalized by their own sum).
    public double Fbm(double x, double y, int octaves, double frequency, double persistence = 0.5)
    {
        var total = 0.0;
        var amplitude = 1.0;
        var maxAmplitude = 0.0;
        var freq = frequency;
        for (var i = 0; i < octaves; i++)
        {
            total += ValueAt(x * freq, y * freq) * amplitude;
            maxAmplitude += amplitude;
            amplitude *= persistence;
            freq *= 2.0;
        }

        return total / maxAmplitude;
    }

    // The unit gradient assigned to an integer lattice point, dotted with (dx, dy) - the
    // offset from that same corner to the sampled point. Bitwise AND (not modulo) so
    // negative coordinates (this world's origin sits in the middle of the terrain, not a
    // corner) still index the permutation table correctly.
    private double DotGradient(int x, int y, double dx, double dy)
    {
        var h = _permutation[(_permutation[x & 255] + y) & 255];
        var (gx, gy) = Gradients[h % Gradients.Length];
        return (gx * dx) + (gy * dy);
    }

    // Eight unit vectors, 45 degrees apart - plenty for how coarsely this gets sampled
    // (biome bands, a terrain bump), without the bookkeeping of Perlin's original
    // hand-picked 3D-edge-midpoint set (this is 2D noise, that set doesn't apply here).
    private static (double X, double Y)[] BuildGradients()
    {
        var gradients = new (double X, double Y)[8];
        for (var i = 0; i < 8; i++)
        {
            var angle = i * Math.PI / 4.0;
            gradients[i] = (Math.Cos(angle), Math.Sin(angle));
        }

        return gradients;
    }

    // Perlin's own quintic fade (6t^5 - 15t^4 + 10t^3) - second-derivative-continuous,
    // unlike the cheaper 3t^2-2t^3 smoothstep, which matters more for gradient noise than
    // it did for value noise: a visible facet/crease can show up right at cell boundaries
    // otherwise, precisely where two cells' independently-oriented gradients disagree most.
    private static double Fade(double t) => t * t * t * ((t * ((t * 6.0) - 15.0)) + 10.0);
}
