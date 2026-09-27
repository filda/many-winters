namespace ManyWinters.Audio;

// A Poisson point process rendered as discrete impulses: the rain/gravel/scatter texture the
// impact and ambient models need, where events happen independently at a mean rate rather than on
// a fixed grid.
public sealed class PoissonImpulses(Rng rng, int sampleRate, float ratePerSecond, float minimumAmplitude, float maximumAmplitude)
{
    public void Fill(Span<float> buffer)
    {
        buffer.Clear();

        // Per-sample probability that keeps the expected count over the whole buffer at
        // ratePerSecond * (buffer.Length / sampleRate), independent of how Fill is chunked.
        var probabilityPerSample = ratePerSecond / sampleRate;

        for (var i = 0; i < buffer.Length; i++)
        {
            if (rng.NextFloat() < probabilityPerSample)
            {
                var magnitude = minimumAmplitude + (rng.NextFloat() * (maximumAmplitude - minimumAmplitude));
                buffer[i] = rng.NextFloat() < 0.5f ? -magnitude : magnitude;
            }
        }
    }
}
