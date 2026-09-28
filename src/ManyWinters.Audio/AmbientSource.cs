namespace ManyWinters.Audio;

// The seasonal bed: birds, an insect trill, an occasional corvid caw and rustle, over a hush that
// stands in for winter's emptiness. Unlike every finite model here this streams, so an event that
// starts near the end of one Read call has to keep playing into the next - the pool below is
// nothing but that carried-over state, one already-rendered buffer and a cursor per voice.
public sealed class AmbientSource : ISampleSource
{
    // A handful of calls per ten seconds at full density, not per second - a dawn chorus is
    // occasional events over a quiet bed, not a wall of birdsong.
    private const float BirdRatePerSecondAtFullDensity = 0.35f;
    private const float InsectRatePerSecondAtFullDensity = 1.1f;
    private const float CorvidRatePerSecondAtFullDensity = 0.09f;
    private const float RustleRatePerSecondAtFullDensity = 0.3f;

    // Scales each event type's own internal peak-0.9 render down before it joins the pool, so a
    // handful overlapping still leaves headroom - AmbientSource is a stream and cannot normalise
    // globally the way a finite model does.
    private const float BirdMixGain = 1.15f;
    private const float InsectMixGain = 0.85f;
    private const float CorvidMixGain = 1.1f;
    private const float RustleMixGain = 0.8f;

    private const int MaxActiveVoices = 16;

    private const float InsectPitchMinHz = 4000.0f;
    private const float InsectPitchRangeHz = 2500.0f;
    private const float InsectDurationMinSeconds = 0.4f;
    private const float InsectDurationRangeSeconds = 1.1f;

    private const float CorvidSizeMin = 0.15f;
    private const float CorvidSizeRange = 0.75f;

    // Pink, not white: the hush is a low rumble, and pink noise's tilt towards low frequency is
    // what gives the lowpass over it something to shape rather than shaving a flat spectrum down
    // to nearly nothing.
    private const float HushLowpassHz = 400.0f;
    private const float HushLowpassQ = 0.707f;
    private const float HushGain = 0.09f;
    private const float HushLfoRateHz = 0.06f;
    private const float HushLfoMinimum = 0.7f;
    private const float HushLfoMaximum = 1.3f;

    // Hush is the one parameter events don't merely gate: it is heard continuously, so a step
    // change in it has to be smoothed the way WindSource smooths its gain, or turning up winter
    // clicks.
    private const float HushSmoothingSeconds = 0.05f;

    // A few recognisable birds rather than a stranger on every call: the same voice recurring is
    // what the phrase-level variation in BirdCall relies on for identity.
    private static readonly BirdVoice[] Birds =
    [
        new BirdVoice(PitchHz: 2200.0f, Brightness: 0.55f),
        new BirdVoice(PitchHz: 3100.0f, Brightness: 0.9f),
        new BirdVoice(PitchHz: 1750.0f, Brightness: 0.35f),
        new BirdVoice(PitchHz: 2600.0f, Brightness: 0.7f),
    ];

    // Dry and leafy: a lot of small grains, little wash, a body damped almost to nothing - what
    // twisted cord and dry leaves already are in GranularModel's own reasoning, at a low intensity
    // and a long gesture so it reads as background rustle rather than a footstep.
    private static readonly GranularSurface RustleSurface = new(
        GrainsPerSecond: 90.0f,
        GrainHardness: 0.2f,
        ResonanceHz: 650.0f,
        ResonanceDamping: 0.9f,
        NoiseWash: 0.25f);

    private static readonly GranularGesture RustleGesture = new(AttackSeconds: 0.2f, T60Seconds: 1.6f, Intensity: 0.3f);

    private readonly Rng _rng;
    private readonly PinkNoise _hushNoise;
    private readonly Biquad _hushFilter;
    private readonly RandomWalkLfo _hushLfo;
    private readonly List<ActiveVoice> _active = [];

    private readonly float _hushSmoothingCoefficient;
    private float _hushLevel;

    private AmbientParameters _parameters;

    public AmbientSource(int sampleRate, int seed)
    {
        SampleRate = sampleRate;

        // One Rng feeds every draw this source makes - scheduling, event content and the hush -
        // so the same seed replays the same bed exactly, the same rule Rng.cs itself documents.
        _rng = new Rng(seed);
        _hushNoise = new PinkNoise(_rng);
        _hushFilter = new Biquad(sampleRate, BiquadShape.LowPass, HushLowpassHz, HushLowpassQ);
        _hushLfo = new RandomWalkLfo(_rng, sampleRate, HushLfoRateHz, HushLfoMinimum, HushLfoMaximum);

        _hushSmoothingCoefficient = 1.0f - MathF.Exp(-1.0f / (HushSmoothingSeconds * sampleRate));
    }

    public int SampleRate { get; }

    public void Set(AmbientParameters parameters)
    {
        _parameters = parameters;
    }

    public void Read(Span<float> buffer)
    {
        var birdProbability = BirdRatePerSecondAtFullDensity * _parameters.BirdDensity / SampleRate;
        var insectProbability = InsectRatePerSecondAtFullDensity * _parameters.InsectDensity / SampleRate;
        var corvidProbability = CorvidRatePerSecondAtFullDensity * _parameters.CorvidDensity / SampleRate;
        var rustleProbability = RustleRatePerSecondAtFullDensity * _parameters.RustleDensity / SampleRate;

        for (var i = 0; i < buffer.Length; i++)
        {
            // Every check draws from the master Rng whether or not it fires, so the sequence
            // consumed per output sample - and therefore the whole render - never depends on how
            // Read is chunked, which is what the buffer-boundary continuity test rests on.
            if (_rng.NextFloat() < birdProbability)
            {
                TryStart(StartBird(_rng, SampleRate));
            }

            if (_rng.NextFloat() < insectProbability)
            {
                TryStart(StartInsect(_rng, SampleRate));
            }

            if (_rng.NextFloat() < corvidProbability)
            {
                TryStart(StartCorvid(_rng, SampleRate));
            }

            if (_rng.NextFloat() < rustleProbability)
            {
                TryStart(StartRustle(_rng, SampleRate));
            }

            var sample = AdvancePool();
            sample += Hush();

            // Soft, not clamped. A hard clamp on a stream is not a guard, it is distortion:
            // measured over thirty seconds of a busy spring bed it squared off 0.089 % of the
            // samples, which is heard as an occasional tick. tanh is the identity for anything
            // quiet and rounds off the coincidental pile-ups instead of squaring them.
            buffer[i] = MathF.Tanh(sample);
        }
    }

    private static float[] StartBird(Rng rng, int sampleRate)
    {
        var voice = Birds[(int)(rng.NextFloat() * Birds.Length)];
        var samples = BirdCall.Render(voice, sampleRate, NextSeed(rng));
        return Scale(samples, BirdMixGain);
    }

    private static float[] StartInsect(Rng rng, int sampleRate)
    {
        var pitchHz = InsectPitchMinHz + (rng.NextFloat() * InsectPitchRangeHz);
        var seconds = InsectDurationMinSeconds + (rng.NextFloat() * InsectDurationRangeSeconds);
        var samples = InsectTrill.Render(pitchHz, seconds, sampleRate, NextSeed(rng));
        return Scale(samples, InsectMixGain);
    }

    private static float[] StartCorvid(Rng rng, int sampleRate)
    {
        var size = CorvidSizeMin + (rng.NextFloat() * CorvidSizeRange);
        var samples = CorvidCaw.Render(size, sampleRate, NextSeed(rng));
        return Scale(samples, CorvidMixGain);
    }

    private static float[] StartRustle(Rng rng, int sampleRate)
    {
        var samples = GranularModel.Render(RustleSurface, RustleGesture, sampleRate, NextSeed(rng));
        return Scale(samples, RustleMixGain);
    }

    private static int NextSeed(Rng rng) => (int)(rng.NextFloat() * int.MaxValue);

    private static float[] Scale(float[] samples, float gain)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= gain;
        }

        return samples;
    }

    private void TryStart(float[] samples)
    {
        if (_active.Count < MaxActiveVoices)
        {
            _active.Add(new ActiveVoice(samples));
        }
    }

    private float AdvancePool()
    {
        var sample = 0.0f;
        for (var v = _active.Count - 1; v >= 0; v--)
        {
            var voice = _active[v];
            sample += voice.Samples[voice.Cursor];
            voice.Cursor++;

            if (voice.Cursor >= voice.Samples.Length)
            {
                _active.RemoveAt(v);
            }
        }

        return sample;
    }

    private float Hush()
    {
        _hushLevel += (_parameters.Hush - _hushLevel) * _hushSmoothingCoefficient;

        var noise = _hushFilter.Process(_hushNoise.Next());
        return noise * HushGain * _hushLevel * _hushLfo.Next();
    }

    private sealed class ActiveVoice(float[] samples)
    {
        public float[] Samples { get; } = samples;

        public int Cursor { get; set; }
    }
}
