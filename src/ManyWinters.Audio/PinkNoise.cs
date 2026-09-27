namespace ManyWinters.Audio;

// Paul Kellet's economy pink noise filter: three one-pole stages whose coefficients are chosen so
// their sum approximates a -3 dB/octave slope, which is equal energy per octave. Cheap enough to
// run per sample, and accurate to within about a decibel up to 4 kHz - above that the three poles
// run out and the slope flattens, which no impact or wind sound here reaches into.
public sealed class PinkNoise(Rng rng)
{
    // The three stages together triple the RMS of their input. Dividing it back out makes pink
    // and white interchangeable at the same nominal level, which is what the models mixing them
    // assume; measured, not derived.
    private const float LevelMatch = 0.3366f;

    private readonly WhiteNoise _white = new(rng);
    private float _b0;
    private float _b1;
    private float _b2;

    public float Next()
    {
        var white = _white.Next();

        _b0 = (0.99765f * _b0) + (white * 0.0990460f);
        _b1 = (0.96300f * _b1) + (white * 0.2965164f);
        _b2 = (0.57000f * _b2) + (white * 1.0526913f);

        return (_b0 + _b1 + _b2 + (white * 0.1848f)) * LevelMatch;
    }
}
