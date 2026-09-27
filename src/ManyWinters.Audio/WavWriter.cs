namespace ManyWinters.Audio;

// Mono 16-bit PCM WAV. The prototype's only output format: every player opens it, and the game's
// own baked effects use the same depth and rate, so what the ear judges here is what would ship.
public static class WavWriter
{
    private const int HeaderBytes = 44;
    private const int BytesPerSample = 2;

    public static void Write(string path, float[] samples, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(samples);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        var dataBytes = samples.Length * BytesPerSample;

        writer.Write("RIFF"u8);

        // Everything after this field, i.e. the header minus "RIFF" and the size itself.
        writer.Write(HeaderBytes - 8 + dataBytes);
        writer.Write("WAVE"u8);

        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * BytesPerSample);
        writer.Write((short)BytesPerSample);
        writer.Write((short)(BytesPerSample * 8));

        writer.Write("data"u8);
        writer.Write(dataBytes);

        foreach (var sample in samples)
        {
            // 32767 rather than 32768: scaling by the negative bound would turn a sample of
            // exactly 1.0 into -32768 on the wrap, which is a full-scale click in the wrong
            // direction rather than the loudest positive sample.
            writer.Write((short)(Math.Clamp(sample, -1.0f, 1.0f) * 32767.0f));
        }
    }
}
