namespace ManyWinters.Audio;

// Farnell's wind: white noise through a bandpass whose centre and gain follow slow random-walk
// LFOs, plus a narrower band an octave up that fades in on gusts for the whistle.
public sealed class WindSource : ISampleSource
{
    // Coefficients are recomputed once per block rather than per sample: at LFO rates well under
    // 1 Hz the difference is inaudible, and per-sample trig would matter for cost, not sound.
    private const int ControlBlock = 64;
    private const float MainQ = 1.2f;
    private const float WhistleQ = 6.0f;
    private const float ToneQ = 0.707f;

    // A bandpassed noise floor sits well below the gain that drives it; this brings a Strength
    // 1.0 breeze up to the plan's target of roughly 0.2 RMS while leaving headroom under the clamp.
    private const float OutputGain = 1.7f;

    private readonly WhiteNoise _noise;
    private readonly RandomWalkLfo _centreLfo;
    private readonly RandomWalkLfo _gainLfo;
    private readonly Biquad _mainFilter;
    private readonly Biquad _whistleFilter;

    // A two-pole bandpass leaves broad skirts, so the noise above the band survives and the
    // result hisses at every strength - it read as a gale before a storm even at the calm end of
    // the sweep. Brightness has to follow strength the way loudness does: a breeze is dark.
    private readonly Biquad _toneFilter;
    private readonly float _gainSmoothingCoefficient;
    private readonly float _centreSmoothingCoefficient;

    private WindParameters _parameters;
    private int _samplesUntilNextBlock;
    private float _targetGain;
    private float _whistleMix;
    private float _currentGain;
    private float _smoothedCentre;

    public WindSource(int sampleRate, int seed)
    {
        SampleRate = sampleRate;

        // One Rng feeds the noise and both LFOs: determinism from the seed has to cover the
        // whole source, not just the parts that look "random" from the outside.
        var rng = new Rng(seed);
        _noise = new WhiteNoise(rng);

        // A moderate breeze so a source that is never configured still makes sense.
        _parameters = new WindParameters(0.4f, 0.3f);

        var blockRate = sampleRate / (float)ControlBlock;
        var lfoSampleRate = Math.Max(1, (int)MathF.Round(blockRate));
        var initialRate = LfoRateHz(_parameters.Gustiness);
        _centreLfo = new RandomWalkLfo(rng, lfoSampleRate, initialRate, 0.0f, 1.0f);
        _gainLfo = new RandomWalkLfo(rng, lfoSampleRate, initialRate, 0.0f, 1.0f);

        // Both LFOs start at their range's midpoint, so the initial block matches that exactly
        // rather than needing a fade-in from an arbitrary starting value.
        var initialCentre = CentreHz(_parameters.Strength, 0.5f);
        _mainFilter = new Biquad(sampleRate, BiquadShape.BandPass, initialCentre, MainQ);
        _whistleFilter = new Biquad(sampleRate, BiquadShape.BandPass, initialCentre * 2.0f, WhistleQ);
        _toneFilter = new Biquad(sampleRate, BiquadShape.LowPass, ToneCutoffHz(_parameters.Strength), ToneQ);

        _smoothedCentre = initialCentre;
        _targetGain = GainTarget(_parameters.Strength, 0.5f);
        _currentGain = _targetGain;
        _whistleMix = 0.0f;

        // One-pole time constant for about 50 ms, at the rate each smoother actually runs at:
        // per sample for gain, per control block for the centre frequency.
        _gainSmoothingCoefficient = 1.0f - MathF.Exp(-1.0f / (0.05f * sampleRate));
        _centreSmoothingCoefficient = 1.0f - MathF.Exp(-1.0f / (0.05f * blockRate));

        _samplesUntilNextBlock = ControlBlock;
    }

    public int SampleRate { get; }

    public void Set(WindParameters parameters)
    {
        _parameters = parameters;

        var rate = LfoRateHz(parameters.Gustiness);
        _centreLfo.RateHz = rate;
        _gainLfo.RateHz = rate;
    }

    public void Read(Span<float> buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
        {
            if (_samplesUntilNextBlock == 0)
            {
                AdvanceBlock();
                _samplesUntilNextBlock = ControlBlock;
            }

            _samplesUntilNextBlock--;

            _currentGain += (_targetGain - _currentGain) * _gainSmoothingCoefficient;

            var noise = _noise.Next();
            var main = _mainFilter.Process(noise);
            var whistle = _whistleFilter.Process(noise);
            var banded = (main * (1.0f - (0.35f * _whistleMix))) + (whistle * 0.6f * _whistleMix);
            var mixed = _toneFilter.Process(banded) * _currentGain * OutputGain;

            // Last-resort guard; the gain mapping is tuned to stay well inside this on its own.
            buffer[i] = Math.Clamp(mixed, -1.0f, 1.0f);
        }
    }

    private void AdvanceBlock()
    {
        var uCentre = _centreLfo.Next();
        var uGain = _gainLfo.Next();

        var targetCentre = CentreHz(_parameters.Strength, uCentre);
        _smoothedCentre += (targetCentre - _smoothedCentre) * _centreSmoothingCoefficient;

        _mainFilter.Retune(_smoothedCentre, MainQ);
        _whistleFilter.Retune(_smoothedCentre * 2.0f, WhistleQ);
        _toneFilter.Retune(ToneCutoffHz(_parameters.Strength), ToneQ);

        _targetGain = GainTarget(_parameters.Strength, uGain);

        // Rides the top half of a gust and is silent in calm air, per the plan's whistle mapping.
        _whistleMix = _parameters.Gustiness * MathF.Max(0.0f, uGain - 0.5f) * 2.0f;
    }

    // Calm stays low and dark, a storm sweeps the whole 300-1200 Hz range: strength scales the
    // LFO's range rather than shifting it, so a calm breeze cannot wander into storm territory.
    private static float CentreHz(float strength, float u) => 300.0f + (900.0f * strength * u);

    // The floor was high enough that "calm" was still a steady rush. A breeze has to be able to
    // drop to almost nothing between gusts.
    private static float GainTarget(float strength, float u) => (0.06f + (0.94f * strength)) * (0.5f + (0.5f * u));

    // Kept clear of the whistle band at strength 0 (600 Hz there), so a calm wind still has its
    // top octave rather than being filtered into a rumble.
    private static float ToneCutoffHz(float strength) => 800.0f + (5200.0f * strength);

    private static float LfoRateHz(float gustiness) => 0.1f + (0.4f * gustiness);
}
