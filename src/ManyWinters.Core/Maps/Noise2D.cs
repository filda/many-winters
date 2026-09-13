namespace ManyWinters.Core.Maps;

// Coherent 2D gradient (Perlin) noise: a smooth field instead of independent randomness per
// point, so biome regions get organic, spatially-correlated shapes. Gradient noise rather than
// value noise because value noise's extrema sit on lattice points and read as a faint
// grid-aligned "waffle"; gradient noise's fall anywhere inside a cell.
public sealed class Noise2D
{
    // Perlin's bound for 2D noise from unit gradients is |value| <= 1/sqrt(2); dividing by it
    // uses the full [0, 1] range instead of the middle third.
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

        // Stryker disable once Equality: a final i = 0 pass can only draw j = 0 and swap
        // source[0] with itself, so > 0 and >= 0 produce the same permutation
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

    // Fractal Brownian motion: octaves at doubling frequency and (by default) halving amplitude.
    // Stays in [0, 1] because every octave is and the weights are normalized by their sum.
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

    // The lattice point's unit gradient dotted with the offset (dx, dy) to the sampled point.
    // Bitwise AND, not modulo, so negative coordinates (the origin is mid-terrain) index the
    // permutation table correctly.
    private double DotGradient(int x, int y, double dx, double dy)
    {
        var h = _permutation[(_permutation[x & 255] + y) & 255];
        var (gx, gy) = Gradients[h % Gradients.Length];
        return (gx * dx) + (gy * dy);
    }

    // Eight unit vectors 45 degrees apart - plenty for how coarsely this is sampled.
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

    // Perlin's quintic fade (6t^5 - 15t^4 + 10t^3): second-derivative-continuous, unlike
    // smoothstep, which shows a crease at cell boundaries where neighbouring gradients disagree.
    private static double Fade(double t) => t * t * t * ((t * ((t * 6.0) - 15.0)) + 10.0);
}
