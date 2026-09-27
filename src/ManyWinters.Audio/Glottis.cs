namespace ManyWinters.Audio;

// The glottal source from Pink Trombone by Neil Thapen (MIT licence), source at
// https://github.com/IMAGINARY/pink-trombone - see THIRD-PARTY-NOTICES.md for the full notice.
// Each glottal cycle is one Liljencrants-Fant waveform: a physical model of the flow through the
// vocal folds, parameterised by Rd (upstream's derivation from a single "tenseness" number, kept
// verbatim below because the geometry only makes sense read against it) rather than a hand-drawn
// pulse shape. It replaces the previous model's Rosenberg pulse for the same reason VocalTract
// replaces parallel formants: this is where the tract's own energy actually comes from, so a
// physical source and a physical tube agree about their spectral tilt instead of one being tuned
// to sound right in front of the other.
//
// Upstream re-derives its LF coefficients every waveform, driven by continuous vibrato and
// tenseness wobble from Perlin noise seeded off the wall clock, because it is scrubbed live by a
// mouse. Nothing here needs that: VoiceModel already varies pitch and voicing once per syllable
// (see its own comment on declining "in steps rather than swept"), so what a throat needs *within*
// a syllable is irregularity from one glottal pulse to the next, not a slow drifting wobble - and
// that irregularity has to come from the render's own seeded Rng to stay reproducible, not from
// a noise function seeded off the clock. RoughnessJitter below is that per-pulse irregularity;
// SetSource's tenseness and frequency are otherwise held fixed for as long as VoiceModel says to.
public sealed class Glottis
{
    // Rosenberg's pulse in the previous model needed "half destroys pitch, a throat needs about a
    // sixth" recorded directly in its own jitter constants; the same throat-vs-noise boundary
    // applies to an LF pulse's period, so RoughnessJitter is expected in that same rough range.
    // Shimmer: how much each pulse's height varies from the last. Held at a constant it is a
    // texture the voice always has; scaled by roughness it becomes part of the same dial as the
    // period jitter, which is how a real rough voice behaves - the folds that fail to close at
    // the same moment twice also fail to close as hard twice.
    private const float PulseAmplitudeJitterMin = 0.08f;
    private const float PulseAmplitudeJitterScale = 2.0f;

    // Diplophonia, and the part plain jitter cannot give. A voice past a certain roughness does
    // not just wobble around its pitch, it slips into period doubling: every other cycle weaker
    // and shorter, because the fold pair has stopped behaving as one oscillator. This is the
    // creak in an old voice and the growl in a strained one. Jitter is symmetric noise around a
    // period; this is not symmetric, and that asymmetry is what the ear hears as dirt.
    // Drift, and the reason the first version was heard as autotune. Roughness jitters each
    // period independently, which is fast noise around a mean that never itself moves - so the
    // pitch is locked to the number it was given, and a pitch held exactly and then stepped at
    // the syllable boundary is precisely what a tuner does to a voice. Nobody holds a pitch.
    // Upstream carries this as a wobble driven by noise seeded off the wall clock; that seeding
    // is what had to go for reproducibility, not the wobble itself.
    private const float DriftRateHz = 0.8f;
    private const float DriftDepth = 0.035f;

    // A little regular vibrato under the drift. Kept small: a pronounced one is singing.
    private const float VibratoRateHz = 5.4f;
    private const float VibratoDepth = 0.006f;

    private const float DiplophoniaThreshold = 0.07f;
    private const float DiplophoniaStrength = 2.2f;
    private const float DiplophoniaPeriodShare = 0.5f;

    private readonly int _sampleRate;
    private readonly Rng _rng;
    private readonly Biquad _aspirationFilter;
    private readonly WhiteNoise _aspirationNoise;

    private float _frequencyHz = 140.0f;
    private float _tenseness = 0.6f;
    private float _roughnessJitter;

    private float _waveformLength;
    private float _timeInWaveform;
    private float _loudness;
    private float _pulseAmplitude = 1.0f;
    private bool _alternatePulse;

    private readonly RandomWalkLfo _drift;
    private float _driftValue;
    private float _vibratoPhase;

    // The LF waveform's own shape parameters, re-derived once per glottal cycle in BeginPeriod -
    // see its comment for what each one is.
    private float _alpha;
    private float _e0;
    private float _epsilon;
    private float _shift;
    private float _delta;
    private float _te;
    private float _omega;

    public Glottis(int sampleRate, Rng rng)
    {
        _sampleRate = sampleRate;
        _rng = rng;

        // Upstream's own aspiration source: white noise through a bandpass centred low in the
        // breath range. It runs through the same formant tube as the voiced pulses (see how
        // VoiceModel feeds this Step's whole output into VocalTract), which is what makes it read
        // as breath *in this throat* rather than as hiss laid over the top of it.
        _aspirationFilter = new Biquad(sampleRate, BiquadShape.BandPass, frequency: 500.0f, q: 0.5f);
        _aspirationNoise = new WhiteNoise(rng);
        _drift = new RandomWalkLfo(rng, sampleRate, DriftRateHz, -1.0f, 1.0f);

        BeginPeriod();
    }

    // Frequency and tenseness are declined in steps between syllables by VoiceModel, not swept -
    // this only takes effect from the next glottal pulse onward, which is itself inaudible at
    // syllable-rate changes and is exactly the granularity a real larynx changes pitch at anyway.
    // roughnessJitter is the fraction each new pulse's period is allowed to jitter by.
    public void SetSource(float frequencyHz, float tenseness, float roughnessJitter)
    {
        _frequencyHz = frequencyHz;
        _tenseness = Math.Clamp(tenseness, 0.0f, 1.0f);
        _roughnessJitter = roughnessJitter;
    }

    public float Step()
    {
        // Stepped every sample so its rate means what it says, but only read when a period
        // begins - a pitch can only change where a cycle does.
        _driftValue = _drift.Next();
        _vibratoPhase += VibratoRateHz / _sampleRate;
        _vibratoPhase -= MathF.Floor(_vibratoPhase);

        _timeInWaveform += 1.0f / _sampleRate;
        if (_timeInWaveform > _waveformLength)
        {
            _timeInWaveform -= _waveformLength;
            BeginPeriod();
        }

        var phase = _timeInWaveform / _waveformLength;
        var source = NormalizedLfWaveform(phase) * _loudness * _pulseAmplitude;

        // Breath is loudest during the open phase of each cycle, not evenly across it - upstream's
        // own noise modulator - and (1 - sqrt(tenseness)) is where VoiceIdentity.Breath actually
        // acts: SetSource's tenseness already carries 1 - Breath, so a breathier voice both leaks
        // more aspiration here and, through _loudness below, buzzes less.
        var voiced = 0.1f + (0.2f * MathF.Max(0.0f, MathF.Sin(2.0f * MathF.PI * phase)));
        var noiseModulator = (_tenseness * voiced) + ((1.0f - _tenseness) * 0.3f);
        var aspiration = (1.0f - MathF.Sqrt(_tenseness)) * noiseModulator * _aspirationFilter.Process(_aspirationNoise.Next());

        return source + aspiration;
    }

    // Re-derives the LF waveform for the cycle about to start, with a fresh jittered period and
    // pulse amplitude - this is where roughness actually happens, one glottal pulse at a time.
    private void BeginPeriod()
    {
        var wander = 1.0f
            + (DriftDepth * _driftValue)
            + (VibratoDepth * MathF.Sin(2.0f * MathF.PI * _vibratoPhase));
        var jitteredFrequency = MathF.Max(_frequencyHz * wander * Jitter(_rng, _roughnessJitter), 10.0f);
        _waveformLength = 1.0f / jitteredFrequency;
        _pulseAmplitude = Jitter(_rng, PulseAmplitudeJitterMin + (PulseAmplitudeJitterScale * _roughnessJitter));
        _loudness = MathF.Pow(_tenseness, 0.25f);

        _alternatePulse = !_alternatePulse;
        var doubling = MathF.Max(0.0f, _roughnessJitter - DiplophoniaThreshold) * DiplophoniaStrength;
        if (_alternatePulse && doubling > 0.0f)
        {
            _pulseAmplitude *= 1.0f - doubling;
            _waveformLength *= 1.0f - (DiplophoniaPeriodShare * doubling);
        }

        // Rd is Fant's single-number summary of a glottal pulse's shape, from fully lax (2.7, a
        // breathy, rounded flow) to fully tense (0.5, an abrupt, buzzy one); upstream's own map
        // from the [0,1] tenseness this port exposes. Ra, Rk and Rg are the three classic LF
        // timing ratios Fant derives from Rd (opening, symmetry and speed), and everything below
        // them is upstream's own closed-form solve for the two exponential/sinusoid segments that
        // meet those ratios and return to exactly -1 at the point of closure - reproduced
        // verbatim because it is an algebraic derivation, not a tunable constant.
        var rd = Math.Clamp(3.0f * (1.0f - _tenseness), 0.5f, 2.7f);
        var ra = -0.01f + (0.048f * rd);
        var rk = 0.224f + (0.118f * rd);
        var rg = (rk / 4.0f) * (0.5f + (1.2f * rk)) / ((0.11f * rd) - (ra * (0.5f + (1.2f * rk))));

        var ta = ra;
        var tp = 1.0f / (2.0f * rg);
        var te = tp + (tp * rk);

        var epsilon = 1.0f / ta;
        var shift = MathF.Exp(-epsilon * (1.0f - te));
        var delta = 1.0f - shift;

        var rhsIntegral = (((1.0f / epsilon) * (shift - 1.0f)) + ((1.0f - te) * shift)) / delta;
        var totalUpperIntegral = -(rhsIntegral - ((te - tp) / 2.0f));

        var omega = MathF.PI / tp;
        var s = MathF.Sin(omega * te);
        var y = -MathF.PI * s * totalUpperIntegral / (tp * 2.0f);
        var alpha = MathF.Log(y) / ((tp / 2.0f) - te);
        var e0 = -1.0f / (s * MathF.Exp(alpha * te));

        _alpha = alpha;
        _e0 = e0;
        _epsilon = epsilon;
        _shift = shift;
        _delta = delta;
        _te = te;
        _omega = omega;
    }

    // The LF waveform itself: an exponentially-growing sinusoid up to the point of glottal closure
    // (te), then an exponential return-to-zero standing in for the abrupt closing flow. Verbatim
    // upstream, against Fant's original derivation.
    private float NormalizedLfWaveform(float t)
    {
        if (t > _te)
        {
            return (-MathF.Exp(-_epsilon * (t - _te)) + _shift) / _delta;
        }

        return _e0 * MathF.Exp(_alpha * t) * MathF.Sin(_omega * t);
    }

    private static float Jitter(Rng rng, float fraction) => 1.0f + ((rng.NextFloat() * 2.0f * fraction) - fraction);
}
