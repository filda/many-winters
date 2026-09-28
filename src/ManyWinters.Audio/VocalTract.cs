namespace ManyWinters.Audio;

// A Kelly-Lochbaum digital waveguide of the vocal tract: 44 cylindrical sections modelled as a
// scattering junction network, plus a 28-section nasal branch. This is a C# port of the tract
// model from Pink Trombone by Neil Thapen (MIT licence), source at
// https://github.com/IMAGINARY/pink-trombone - see THIRD-PARTY-NOTICES.md for the full notice.
// It replaces a bank of parallel bandpass filters: formants, radiation and the source spectrum
// itself all fall out of one physical structure instead of being modelled separately, and a
// tongue moving through this shape is what makes vowel transitions travel through the same space
// a real mouth's do rather than glide in a straight line between arbitrary frequency triples.
//
// One thing this port changes rather than reproduces, and why:
//  - Upstream seeds its one-off nose-reflection calculation from a temporarily *open* velum before
//    settling into its actual (closed) resting value - a quirk of how its demo UI first opens the
//    tract to preview the nose, then closes it for the default voice. This tract never opens its
//    velum at all (see the constructor), so that step would be pure ceremony; the reflections are
//    computed once, directly, from the velum's real (closed) diameter.
public sealed class VocalTract
{
    private const int SectionCount = 44;
    private const int BladeStart = 10;
    private const int TipStart = 32;
    private const int LipStart = 39;

    private const int NoseLength = 28;
    private const int NoseStart = SectionCount - NoseLength + 1; // 17: where the velum taps the oral tract.

    // The vocal folds reflect most of what reaches them back up the tract (0.75) rather than
    // absorbing it, and the lips do the same in the other direction but invert the wave (-0.85)
    // - upstream's own tuned figures for those two boundaries, not derived from a formula.
    private const float GlottalReflection = 0.75f;
    private const float LipReflection = -0.85f;

    // Every junction loses a whisker of energy per round trip - viscosity and heat conduction in
    // a real tube - or an undamped waveguide rings forever. Upstream's own figure.
    private const float Damping = 0.999f;

    // The cosine hump's height is measured from this offset rather than from 1.5 (the tract's
    // open-mouth diameter) directly; upstream's own constant, chosen so the hump's edges blend
    // into the fixed throat and lip sections either side of it rather than meeting them at a step.
    private const float TongueCurveOffset = 1.7f;

    // A closure held long enough to be heard as one, then released, is what makes a stop; the
    // second condition guards against firing on every ordinary vowel-to-vowel glide, which also
    // touches a momentary near-zero diameter as the tongue passes through a narrow shape.
    private const float TransientLifeTimeSeconds = 0.2f;
    private const float TransientStrength = 0.3f;
    private const float TransientExponent = 200.0f;
    private const float VelumObstructionThreshold = 0.223f;

    // Two round trips of the whole tract are computed per output sample: each junction step moves
    // a wave exactly one section, so resolving 44 sections at the sample rate the caller renders
    // at would only place formants correctly for a tract about twice as long as a human one. This
    // is upstream's own fixed choice, made for the same reason - VoiceModel is what stretches or
    // compresses a whole render's sample rate to turn this fixed section count into a shorter or
    // longer physical tract for a differently sized voice, rather than varying this substep count.
    private const int SubstepsPerSample = 2;
    private const float OutputScale = 0.125f;

    private readonly float[] _diameter = new float[SectionCount];
    private readonly float[] _right = new float[SectionCount];
    private readonly float[] _left = new float[SectionCount];
    private readonly float[] _junctionOutRight = new float[SectionCount];
    private readonly float[] _junctionOutLeft = new float[SectionCount + 1];

    // Reflection coefficients from the block SetShape most recently started (_reflection) and the
    // one it is moving towards (_newReflection): Step interpolates between the two by how far
    // through the block it is. A junction's own coefficient is always safely within [-1, 1] for
    // any one fixed shape - that is what makes a Kelly-Lochbaum junction passive - but jumping
    // straight from one shape's coefficients to the next's, instantly, while the delay lines still
    // carry energy scattered under the old ones, is a different network at every sample and no
    // longer covered by that guarantee. Upstream interpolates every sample for exactly this reason
    // (its own comment calls it out); an early port of this file that snapped coefficients straight
    // to their new values at each SetShape call instead - reasoning that a shape glides slowly
    // enough for the jump to be inaudible, the same trade that works for a slowly retuned filter
    // elsewhere - measurably blew up within a few syllables, worst around a stop consonant's
    // closure and release, exactly where the shape changes fastest and the jump is largest.
    private readonly float[] _reflection = new float[SectionCount];
    private readonly float[] _newReflection = new float[SectionCount];

    private readonly float[] _noseDiameter = new float[NoseLength];
    private readonly float[] _noseRight = new float[NoseLength];
    private readonly float[] _noseLeft = new float[NoseLength];
    private readonly float[] _noseReflection = new float[NoseLength];
    private readonly float[] _noseJunctionOutRight = new float[NoseLength];
    private readonly float[] _noseJunctionOutLeft = new float[NoseLength + 1];

    private float _noseJunctionReflectionLeft;
    private float _noseJunctionReflectionRight;
    private float _noseJunctionReflectionNose;
    private float _newNoseJunctionReflectionLeft;
    private float _newNoseJunctionReflectionRight;
    private float _newNoseJunctionReflectionNose;

    private readonly List<(int Position, float StartTime, float Strength)> _transients = [];
    private readonly bool[] _obstructed = new bool[SectionCount];
    private readonly float _tractStepSeconds;
    private float _time;
    private int _lastObstruction = -1;

    public VocalTract(int sampleRate)
    {
        _tractStepSeconds = 1.0f / (sampleRate * SubstepsPerSample);

        // The velum never opens: nasal sounds are not part of what an unintelligible babble needs
        // to distinguish, so it stays at Pink Trombone's own "closed" resting figure and this
        // becomes a fixed, always-present side branch rather than a controllable one.
        const float velumClosed = 0.01f;
        for (var i = 0; i < NoseLength; i++)
        {
            if (i == 0)
            {
                _noseDiameter[i] = velumClosed;
                continue;
            }

            var d = 2.0f * i / NoseLength;
            var diameter = d < 1.0f ? 0.4f + (1.6f * d) : 0.5f + (1.5f * (2.0f - d));
            _noseDiameter[i] = MathF.Min(diameter, 1.9f);
        }

        for (var i = 1; i < NoseLength; i++)
        {
            var a0 = _noseDiameter[i - 1] * _noseDiameter[i - 1];
            var a1 = _noseDiameter[i] * _noseDiameter[i];
            var sum = a0 + a1;
            _noseReflection[i] = MathF.Abs(sum) > 1e-6f ? (a0 - a1) / sum : 1.0f;
        }

        SetShape(new TractShape(TongueIndex: 20.0f, TongueDiameter: 2.5f, LipDiameter: 1.5f));
    }

    // Sets the tract's diameters directly from a shape and recomputes both the main-tract and
    // nose-junction reflection coefficients from them - the block-boundary retune described above.
    // A shape whose diameter reaches zero somewhere and then clears again (a stop consonant's
    // release) queues a transient: the tract does not stay silent and then have a burst dropped
    // into it, the released pressure *is* the burst.
    public void SetShape(TractShape shape)
    {
        var newLastObstruction = -1;
        for (var i = 0; i < SectionCount; i++)
        {
            _diameter[i] = RestDiameter(i, shape);
            var obstructed = _diameter[i] <= 0.0f;
            if (obstructed)
            {
                newLastObstruction = i;
            }

            // Cleared, rather than left to carry a stale value, whenever the cell is obstructed
            // *or* was a moment ago: a sealed cell meeting a reflection coefficient of exactly 1
            // has nowhere to dissipate whatever it already held, and a glottis that keeps adding a
            // fresh pulse every cycle regardless rings that up without bound - measured to reach
            // floating-point infinity within a couple of thousand samples. Clearing only on the
            // instant a cell first seals is not enough: held obstructed and re-cleared once, it
            // still needs clearing on every later block, because ProcessTransients writes straight
            // into whichever cell was last obstructed, and that write has to land in a genuinely
            // silent cell each time, not accumulate in one that a floating-point residue is
            // quietly leaking into. A gliding consonant place (VoiceModel moves both TongueIndex
            // and TongueDiameter together, so the obstructed span itself slides across cells
            // rather than shrinking in place) means a cell can just as easily go the other way,
            // reopening while still holding whatever built up in the sealed pocket it was part of
            // a moment before - exactly as spurious, and cleared for the same reason.
            if (obstructed || _obstructed[i])
            {
                _right[i] = 0.0f;
                _left[i] = 0.0f;
            }

            // The nose branch couples into the oral tract at exactly these two cells (see the
            // nose-junction scattering in Substep). An obstruction that sweeps through this
            // narrow span - a consonant place near VocalTract.NoseStart, which a gliding tongue
            // passes through same as any other - swings an0 or an1 to zero and back on the *nose*
            // side of that junction too, and the nose branch's own delay line is exactly as
            // unable to carry a stale value through that as the main tract's is.
            if ((i == NoseStart || i == NoseStart + 1) && obstructed != _obstructed[i])
            {
                Array.Clear(_noseRight);
                Array.Clear(_noseLeft);
            }

            _obstructed[i] = obstructed;
        }

        if (_lastObstruction > -1 && newLastObstruction == -1 && _noseDiameter[0] < VelumObstructionThreshold)
        {
            _transients.Add((_lastObstruction, _time, TransientStrength));
        }

        _lastObstruction = newLastObstruction;

        // What Step interpolates from is the block that is ending, not whatever _reflection still
        // held from before that one - the same "reflection[i] = newReflection[i]" shift upstream
        // makes before deriving the next target.
        Array.Copy(_newReflection, _reflection, SectionCount);
        for (var i = 1; i < SectionCount; i++)
        {
            var a0 = _diameter[i - 1] * _diameter[i - 1];
            var a1 = _diameter[i] * _diameter[i];
            var sum = a0 + a1;
            _newReflection[i] = MathF.Abs(sum) > 1e-6f ? (a0 - a1) / sum : 1.0f;
        }

        _noseJunctionReflectionLeft = _newNoseJunctionReflectionLeft;
        _noseJunctionReflectionRight = _newNoseJunctionReflectionRight;
        _noseJunctionReflectionNose = _newNoseJunctionReflectionNose;

        var an0 = _diameter[NoseStart] * _diameter[NoseStart];
        var an1 = _diameter[NoseStart + 1] * _diameter[NoseStart + 1];
        var velumArea = _noseDiameter[0] * _noseDiameter[0];
        var noseSum = an0 + an1 + velumArea;
        if (MathF.Abs(noseSum) > 1e-6f)
        {
            _newNoseJunctionReflectionLeft = ((2.0f * an0) - noseSum) / noseSum;
            _newNoseJunctionReflectionRight = ((2.0f * an1) - noseSum) / noseSum;
            _newNoseJunctionReflectionNose = ((2.0f * velumArea) - noseSum) / noseSum;
        }
        else
        {
            _newNoseJunctionReflectionLeft = 1.0f;
            _newNoseJunctionReflectionRight = 1.0f;
            _newNoseJunctionReflectionNose = 1.0f;
        }
    }

    // Runs the waveguide forward by one output sample: two half-sample scattering passes, the
    // same glottal excitation fed into both, summed and scaled - upstream's own SubstepsPerSample
    // and OutputScale (see their comments above). blockProgress is how far through the current
    // control block (see VoiceModel) this sample falls, in [0, 1) - the two substeps use it and a
    // half-substep further along, upstream's own scheme for interpolating a whole output sample's
    // worth of junction coefficients smoothly rather than in one instantaneous step per block.
    public float Step(float glottalExcitation, float blockProgress)
    {
        var halfStep = 0.5f / SubstepsPerSample;
        var sample = 0.0f;
        for (var s = 0; s < SubstepsPerSample; s++)
        {
            sample += Substep(glottalExcitation, blockProgress + (s * halfStep));
        }

        return sample * OutputScale;
    }

    private float Substep(float glottalExcitation, float lambda)
    {
        ProcessTransients();

        _junctionOutRight[0] = (_left[0] * GlottalReflection) + glottalExcitation;
        _junctionOutLeft[SectionCount] = _right[SectionCount - 1] * LipReflection;

        for (var i = 1; i < SectionCount; i++)
        {
            var r = _reflection[i] + ((_newReflection[i] - _reflection[i]) * lambda);
            var w = r * (_right[i - 1] + _left[i]);
            _junctionOutRight[i] = _right[i - 1] - w;
            _junctionOutLeft[i] = _left[i] + w;
        }

        // The nose junction overwrites the plain scattering result computed for i = NoseStart
        // above: three tubes meet there (the oral tract on both sides plus the nose), so it needs
        // its own three-way scattering equations rather than the ordinary two-tube ones.
        {
            const int i = NoseStart;
            var rLeft = _noseJunctionReflectionLeft + ((_newNoseJunctionReflectionLeft - _noseJunctionReflectionLeft) * lambda);
            _junctionOutLeft[i] = (rLeft * _right[i - 1]) + ((1.0f + rLeft) * (_noseLeft[0] + _left[i]));
            var rRight = _noseJunctionReflectionRight + ((_newNoseJunctionReflectionRight - _noseJunctionReflectionRight) * lambda);
            _junctionOutRight[i] = (rRight * _left[i]) + ((1.0f + rRight) * (_right[i - 1] + _noseLeft[0]));
            var rNose = _noseJunctionReflectionNose + ((_newNoseJunctionReflectionNose - _noseJunctionReflectionNose) * lambda);
            _noseJunctionOutRight[0] = (rNose * _noseLeft[0]) + ((1.0f + rNose) * (_left[i] + _right[i - 1]));
        }

        for (var i = 0; i < SectionCount; i++)
        {
            _right[i] = _junctionOutRight[i] * Damping;
            _left[i] = _junctionOutLeft[i + 1] * Damping;
        }

        var lipOutput = _right[SectionCount - 1];

        _noseJunctionOutLeft[NoseLength] = _noseRight[NoseLength - 1] * LipReflection;
        for (var i = 1; i < NoseLength; i++)
        {
            var w = _noseReflection[i] * (_noseRight[i - 1] + _noseLeft[i]);
            _noseJunctionOutRight[i] = _noseRight[i - 1] - w;
            _noseJunctionOutLeft[i] = _noseLeft[i] + w;
        }

        // The nose branch is not damped: upstream's own choice, and it costs nothing audible - the
        // velum stays almost closed, so the energy that ever reaches this branch is tiny already.
        for (var i = 0; i < NoseLength; i++)
        {
            _noseRight[i] = _noseJunctionOutRight[i];
            _noseLeft[i] = _noseJunctionOutLeft[i + 1];
        }

        var noseOutput = _noseRight[NoseLength - 1];

        _time += _tractStepSeconds;

        return lipOutput + noseOutput;
    }

    // A transient is a released stop consonant's burst: a sharply decaying impulse dropped into
    // the tract at the point that just stopped being obstructed, dying away over one glottal
    // pulse's worth of time. Strength and exponent are upstream's own tuned figures.
    private void ProcessTransients()
    {
        for (var i = _transients.Count - 1; i >= 0; i--)
        {
            var (position, startTime, strength) = _transients[i];
            var timeAlive = _time - startTime;
            if (timeAlive > TransientLifeTimeSeconds)
            {
                _transients.RemoveAt(i);
                continue;
            }

            var amplitude = strength * MathF.Pow(2.0f, -TransientExponent * timeAlive);
            _right[position] += amplitude / 2.0f;
            _left[position] += amplitude / 2.0f;
        }
    }

    // Upstream's own rest-shape formula (TractShaper.getRestDiameter): three fixed regions - a
    // narrow throat, a slightly wider pharynx, and an open mouth past the lips - with the tongue's
    // cosine-shaped hump filling the span between them, and the lip region driven by the shape's
    // own LipDiameter instead of a further fixed constant so a labial closure has somewhere to
    // happen.
    private static float RestDiameter(int i, TractShape shape)
    {
        if (i < 7)
        {
            return 0.6f;
        }

        if (i < BladeStart)
        {
            return 1.1f;
        }

        if (i >= LipStart)
        {
            return shape.LipDiameter;
        }

        var t = 1.1f * MathF.PI * (shape.TongueIndex - i) / (TipStart - BladeStart);
        var fixedTongueDiameter = 2.0f + ((shape.TongueDiameter - 2.0f) / 1.5f);
        var curve = (1.5f - fixedTongueDiameter + TongueCurveOffset) * MathF.Cos(t);

        // The hump is flattened where it meets the lips, upstream's own fudge to keep that join
        // smooth rather than creased. Upstream also tests for BladeStart - 2 here; the early
        // returns above mean neither its loop nor this one ever reaches that index, so the
        // branch is dropped rather than transcribed dead.
        if (i == LipStart - 1)
        {
            curve *= 0.8f;
        }

        if (i == BladeStart || i == LipStart - 2)
        {
            curve *= 0.94f;
        }

        // Clamped to zero, never let negative: area is diameter squared, so an unclamped diameter
        // swinging past zero into negative territory does not read as "more closed" - it reads as
        // *reopening*, since squaring throws the sign away. A tongue shape driven hard enough
        // towards a closure (TongueDiameter at or below zero) would then carve out a second,
        // spurious near-zero-area point on its way past true closure, trapping a short, almost
        // lossless cavity between the two. Driven by a glottis that keeps adding pulses into it
        // every sample regardless of what is already resonating there, that cavity has nowhere to
        // dissipate what it accumulates and rings up without bound - measured, given enough
        // samples, to floating-point infinity. A physical tube's cross-section cannot be negative
        // either way, so clamping here is the fix and not merely a patch over the symptom.
        return MathF.Max(1.5f - curve, 0.0f);
    }
}
