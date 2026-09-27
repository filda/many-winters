namespace ManyWinters.Audio;

// Linear attack into exponential decay. Every impact sound is percussive, so this is the one
// shape every mode and burst in the library needs, not a general ADSR.
public sealed class Envelope
{
    private readonly int _attackSamples;
    private readonly float _decayPerSample;

    private int _sampleIndex;
    private float _level = 1.0f;

    public Envelope(int sampleRate, float attackSeconds, float t60Seconds)
    {
        _attackSamples = (int)(attackSeconds * sampleRate);

        // ln(0.001) is exactly -60 dB in natural log terms; a per-sample multiplier raised to
        // (t60Seconds * sampleRate) samples must land there for T60 to mean what its name says.
        var decaySamples = MathF.Max(t60Seconds * sampleRate, 1.0f);
        _decayPerSample = MathF.Exp(MathF.Log(0.001f) / decaySamples);
    }

    public float Next()
    {
        if (_sampleIndex < _attackSamples)
        {
            var value = (_sampleIndex + 1.0f) / _attackSamples;
            _sampleIndex++;
            return value;
        }

        if (_sampleIndex == _attackSamples)
        {
            _sampleIndex++;
            return _level;
        }

        _level *= _decayPerSample;
        return _level;
    }
}
