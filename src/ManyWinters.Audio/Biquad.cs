namespace ManyWinters.Audio;

public enum BiquadShape
{
    LowPass,
    BandPass,
    HighPass,
}

// RBJ cookbook biquad, direct form I. The coefficient derivations are the cookbook formulas
// verbatim, so they can be checked against that reference rather than re-derived.
//
// Retune keeps the delay line: wind sweeps its bandpass centre continuously, and a filter
// rebuilt from scratch every block would restart from silence and click on every boundary.
public sealed class Biquad
{
    private readonly int _sampleRate;
    private readonly BiquadShape _shape;

    private float _b0;
    private float _b1;
    private float _b2;
    private float _a1;
    private float _a2;

    private float _x1;
    private float _x2;
    private float _y1;
    private float _y2;

    public Biquad(int sampleRate, BiquadShape shape, float frequency, float q)
    {
        _sampleRate = sampleRate;
        _shape = shape;
        Retune(frequency, q);
    }

    public void Retune(float frequency, float q)
    {
        var w0 = 2.0f * MathF.PI * frequency / _sampleRate;
        var cosW0 = MathF.Cos(w0);
        var alpha = MathF.Sin(w0) / (2.0f * q);

        var (b0, b1, b2) = _shape switch
        {
            BiquadShape.LowPass => ((1.0f - cosW0) / 2.0f, 1.0f - cosW0, (1.0f - cosW0) / 2.0f),
            // Constant skirt gain (b1 = 0): the peak stays at 0 dB whatever Q is, which is what
            // the wind and impact models expect from "bandpass".
            BiquadShape.BandPass => (alpha, 0.0f, -alpha),
            BiquadShape.HighPass => ((1.0f + cosW0) / 2.0f, -(1.0f + cosW0), (1.0f + cosW0) / 2.0f),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), _shape, "Unknown filter shape."),
        };

        var a0 = 1.0f + alpha;

        // The cookbook coefficients come out unnormalised by a0; dividing here once keeps
        // Process a plain multiply-add with no division per sample.
        _b0 = b0 / a0;
        _b1 = b1 / a0;
        _b2 = b2 / a0;
        _a1 = -2.0f * cosW0 / a0;
        _a2 = (1.0f - alpha) / a0;
    }

    public float Process(float sample)
    {
        var output = (_b0 * sample) + (_b1 * _x1) + (_b2 * _x2) - (_a1 * _y1) - (_a2 * _y2);

        _x2 = _x1;
        _x1 = sample;
        _y2 = _y1;
        _y1 = output;

        return output;
    }
}
