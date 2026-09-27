namespace ManyWinters.Audio;

// Uniform white noise: the raw material every other noise-based primitive (pink noise, impulses,
// wind) filters or gates rather than drawing from Rng directly.
public sealed class WhiteNoise(Rng rng)
{
    public float Next() => (rng.NextFloat() * 2.0f) - 1.0f;
}
