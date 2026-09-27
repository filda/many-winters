using System.Numerics;

namespace ManyWinters.Audio;

// Numeric measurements the render tool prints next to every file it writes, and that tests assert
// on instead of comparing waveforms sample-by-sample.
public static class Analysis
{
    public static float Rms(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
        {
            return 0.0f;
        }

        double sumOfSquares = 0.0;
        foreach (var sample in samples)
        {
            sumOfSquares += (double)sample * sample;
        }

        return (float)Math.Sqrt(sumOfSquares / samples.Length);
    }

    public static float Peak(ReadOnlySpan<float> samples)
    {
        var peak = 0.0f;
        foreach (var sample in samples)
        {
            peak = MathF.Max(peak, MathF.Abs(sample));
        }

        return peak;
    }

    // Magnitude spectrum via radix-2 FFT. Truncating to a power of two rather than zero-padding
    // keeps every bin's energy tied to real signal, and the Hann window trades a little frequency
    // resolution for far less spectral leakage from the hard edges of a truncated buffer.
    public static float[] Fft(ReadOnlySpan<float> samples)
    {
        var length = 1;
        while (length * 2 <= samples.Length)
        {
            length *= 2;
        }

        var buffer = new Complex[length];
        for (var i = 0; i < length; i++)
        {
            var window = 0.5f * (1.0f - MathF.Cos(2.0f * MathF.PI * i / (length - 1)));
            buffer[i] = new Complex(samples[i] * window, 0.0);
        }

        Transform(buffer);

        var spectrum = new float[length / 2];
        for (var i = 0; i < spectrum.Length; i++)
        {
            spectrum[i] = (float)buffer[i].Magnitude;
        }

        return spectrum;
    }

    public static float BinFrequency(int bin, int spectrumLength, int sampleRate) =>
        bin * (float)sampleRate / (2.0f * spectrumLength);

    public static float SpectralCentroid(ReadOnlySpan<float> samples, int sampleRate)
    {
        var spectrum = Fft(samples);

        double weightedSum = 0.0;
        double magnitudeSum = 0.0;
        for (var bin = 0; bin < spectrum.Length; bin++)
        {
            var frequency = BinFrequency(bin, spectrum.Length, sampleRate);
            weightedSum += frequency * spectrum[bin];
            magnitudeSum += spectrum[bin];
        }

        return magnitudeSum > 0.0 ? (float)(weightedSum / magnitudeSum) : 0.0f;
    }

    public static float DominantFrequency(ReadOnlySpan<float> samples, int sampleRate)
    {
        var spectrum = Fft(samples);

        var peakBin = 0;
        var peakMagnitude = 0.0f;
        for (var bin = 0; bin < spectrum.Length; bin++)
        {
            if (spectrum[bin] > peakMagnitude)
            {
                peakMagnitude = spectrum[bin];
                peakBin = bin;
            }
        }

        return BinFrequency(peakBin, spectrum.Length, sampleRate);
    }

    public static float DurationAbove(ReadOnlySpan<float> samples, int sampleRate, float decibelsBelowPeak)
    {
        var peak = Peak(samples);
        var threshold = peak * MathF.Pow(10.0f, decibelsBelowPeak / 20.0f);

        for (var i = samples.Length - 1; i >= 0; i--)
        {
            if (MathF.Abs(samples[i]) > threshold)
            {
                return (i + 1) / (float)sampleRate;
            }
        }

        return 0.0f;
    }

    // In-place iterative Cooley-Tukey. samples.Length is guaranteed a power of two by Fft's
    // caller, which this method relies on rather than re-checking.
    private static void Transform(Complex[] samples)
    {
        var n = samples.Length;

        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;

            if (i < j)
            {
                (samples[i], samples[j]) = (samples[j], samples[i]);
            }
        }

        for (var size = 2; size <= n; size *= 2)
        {
            var halfSize = size / 2;
            var angleStep = -2.0 * Math.PI / size;

            for (var start = 0; start < n; start += size)
            {
                for (var k = 0; k < halfSize; k++)
                {
                    var twiddle = Complex.FromPolarCoordinates(1.0, angleStep * k);
                    var even = samples[start + k];
                    var odd = samples[start + k + halfSize] * twiddle;

                    samples[start + k] = even + odd;
                    samples[start + k + halfSize] = even - odd;
                }
            }
        }
    }
}
