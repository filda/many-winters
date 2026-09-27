namespace ManyWinters.Audio;

// Seeded xorshift32. Determinism is the point: a rendered listening set has to be byte-identical
// on every machine for the blind gate to mean anything, and a test can only assert "no two hits
// are the same" if the same seed replays. System.Random makes no cross-version promise about its
// sequence, so it cannot carry that.
public sealed class Rng
{
    private uint _state;
    private float? _spareGaussian;

    public Rng(int seed)
    {
        // xorshift is stuck at zero and correlates badly between nearby small seeds - consecutive
        // mode detunes would drift in step - so the seed is spread through an avalanche first.
        var value = unchecked((uint)seed);
        value = unchecked((value ^ (value >> 16)) * 0x45d9f3bu);
        value = unchecked((value ^ (value >> 16)) * 0x45d9f3bu);
        value ^= value >> 16;
        _state = value == 0 ? 0x9e3779b9u : value;
    }

    // Uniform in [0, 1). The top 24 bits are the ones xorshift mixes best, and 24 bits is exactly
    // the float mantissa, so every result is representable without rounding back to 1.
    public float NextFloat() => (NextState() >> 8) * (1.0f / 16777216.0f);

    // Standard normal, Box-Muller. The transform yields two independent values per pair of
    // uniforms; throwing the second away would halve the throughput for nothing.
    public float NextGaussian()
    {
        if (_spareGaussian is { } spare)
        {
            _spareGaussian = null;
            return spare;
        }

        // log(0) is negative infinity, and NextFloat can return exactly 0. The floor is one step
        // of NextFloat's own resolution, which caps the draw at about 5.7 sigma.
        var radius = MathF.Sqrt(-2.0f * MathF.Log(MathF.Max(NextFloat(), 1e-7f)));
        var angle = 2.0f * MathF.PI * NextFloat();
        _spareGaussian = radius * MathF.Sin(angle);
        return radius * MathF.Cos(angle);
    }

    private uint NextState()
    {
        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return _state;
    }
}
