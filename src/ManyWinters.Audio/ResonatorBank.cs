namespace ManyWinters.Audio;

public readonly record struct Mode(float Frequency, float T60Seconds, float Gain);

// A bank of damped two-pole resonators, the modal core of the impact model: an excitation buffer
// (a noise burst) rings each mode at its own frequency and decay independently, and the modes sum
// to the struck body's timbre.
public sealed class ResonatorBank(int sampleRate, IReadOnlyList<Mode> modes)
{
    // Adds into output rather than overwriting: the impact model mixes a striker bank and a
    // struck bank into the same buffer, and a caller that wanted a clean start would just clear
    // the buffer first rather than every resonator needing an "overwrite" variant.
    public void Process(ReadOnlySpan<float> excitation, Span<float> output)
    {
        foreach (var mode in modes)
        {
            var w = 2.0f * MathF.PI * mode.Frequency / sampleRate;

            // exp(-ln(1000)/(T60*sampleRate)) is the per-sample pole radius that makes the
            // envelope reach 1/1000 (-60 dB) after exactly T60Seconds worth of samples.
            var r = MathF.Exp(-MathF.Log(1000.0f) / MathF.Max(mode.T60Seconds * sampleRate, 1.0f));
            var g = mode.Gain * (1.0f - r);
            var twoRCosW = 2.0f * r * MathF.Cos(w);
            var rSquared = r * r;

            float y1 = 0.0f;
            float y2 = 0.0f;

            for (var i = 0; i < excitation.Length; i++)
            {
                var y = (g * excitation[i]) + (twoRCosW * y1) - (rSquared * y2);
                output[i] += y;
                y2 = y1;
                y1 = y;
            }
        }
    }
}
