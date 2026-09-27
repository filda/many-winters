namespace ManyWinters.Audio;

// A bounded random walk used as a slow modulation source - wind's centre frequency and gain. It
// ramps linearly towards a target drawn uniformly from the range, and draws a fresh target on
// arrival, so the rate is literally "range traversed per second" and the per-sample step is
// exactly bounded by it. Smoothing towards a target redrawn every sample would satisfy the same
// bound while averaging away to the midpoint: the bound would hold and the modulation would not
// exist. At these rates (well under 1 Hz) the corner where one ramp meets the next is far below
// anything audible as a click.
public sealed class RandomWalkLfo(Rng rng, int sampleRate, float rateHz, float minimum, float maximum)
{
    private float _value = (minimum + maximum) / 2.0f;
    private float _target = (minimum + maximum) / 2.0f;

    public float RateHz { get; set; } = rateHz;

    public float Next()
    {
        var step = (maximum - minimum) * RateHz / sampleRate;
        var remaining = _target - _value;

        if (MathF.Abs(remaining) <= step)
        {
            _value = _target;
            _target = minimum + (rng.NextFloat() * (maximum - minimum));
            return _value;
        }

        _value += MathF.Sign(remaining) * step;
        return _value;
    }
}
