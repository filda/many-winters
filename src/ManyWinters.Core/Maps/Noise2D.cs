namespace ManyWinters.Core.Maps;

// Coherent 2D value noise: a smooth, continuous field instead of independent randomness
// per sampled point - lets things like "which biome grows here" have organic,
// spatially-correlated shapes (soft blobs blending into each other) rather than
// picking a center point and drawing an explicit circle around it. Not true gradient
// (Perlin) noise - a seeded lattice of scalar values, smoothstep-interpolated between
// the four grid corners around each sampled point - simpler to get right and plenty
// smooth enough for a decision like "what grows here", which doesn't need Perlin's
// finer surface-detail guarantees.
public sealed class Noise2D
{
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
        var tx = Smoothstep(x - x0);
        var ty = Smoothstep(y - y0);

        var v00 = LatticeValue(x0, y0);
        var v10 = LatticeValue(x0 + 1, y0);
        var v01 = LatticeValue(x0, y0 + 1);
        var v11 = LatticeValue(x0 + 1, y0 + 1);

        var top = v00 + ((v10 - v00) * tx);
        var bottom = v01 + ((v11 - v01) * tx);
        return top + ((bottom - top) * ty);
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

    // A pseudo-random scalar in [0, 1) for an integer lattice point, stable across calls -
    // bitwise AND (not modulo) so negative coordinates (this world's origin sits in the
    // middle of the terrain, not a corner) still index the permutation table correctly.
    private double LatticeValue(int x, int y)
    {
        var h = _permutation[(_permutation[x & 255] + y) & 255];
        return h / 255.0;
    }

    private static double Smoothstep(double t) => t * t * (3.0 - (2.0 * t));
}
