namespace ManyWinters.Audio;

public enum Waveform
{
    Sine,
    Sawtooth,
}

// Phase-accumulated oscillator with a linear frequency glide. Phase accumulation (rather than
// MathF.Sin(2*pi*frequency*t)) is what lets the frequency change between samples without a
// discontinuity in the waveform itself.
public sealed class Oscillator
{
    private readonly int _sampleRate;
    private readonly Waveform _waveform;

    private float _frequency;
    private float _glideStart;
    private float _glideTarget;
    private int _glideSamplesRemaining;
    private int _glideSamplesTotal;
    private float _phase;

    public Oscillator(int sampleRate, Waveform waveform, float frequency)
    {
        _sampleRate = sampleRate;
        _waveform = waveform;
        _frequency = frequency;
    }

    public void GlideTo(float targetFrequency, float seconds)
    {
        _glideStart = _frequency;
        _glideTarget = targetFrequency;
        _glideSamplesTotal = Math.Max((int)(seconds * _sampleRate), 1);
        _glideSamplesRemaining = _glideSamplesTotal;
    }

    public float Next()
    {
        if (_glideSamplesRemaining > 0)
        {
            // Counted down first: on the last sample of the ramp the progress is exactly 1, so
            // the glide finishes on the target rather than one step short of it forever.
            _glideSamplesRemaining--;
            var progress = 1.0f - (_glideSamplesRemaining / (float)_glideSamplesTotal);
            _frequency = _glideStart + ((_glideTarget - _glideStart) * progress);
        }

        var value = _waveform switch
        {
            Waveform.Sine => MathF.Sin(2.0f * MathF.PI * _phase),
            // Sawtooth from the raw [0,1) phase, remapped to [-1,1): a ramp down would need the
            // phase counted the other way for no audible difference.
            Waveform.Sawtooth => (2.0f * _phase) - 1.0f,
            _ => throw new ArgumentOutOfRangeException(),
        };

        _phase += _frequency / _sampleRate;
        _phase -= MathF.Floor(_phase);

        return value;
    }
}
