namespace ManyWinters.Audio;

// Pulls in fixed-size chunks the way the game's _Process tops up its generator buffer, so a
// source whose parameters change between pulls renders as it would sound rather than as one long
// uninterrupted Read call would.
public static class SampleSourceRenderer
{
    public static float[] Render(ISampleSource source, float seconds, int chunkSamples)
    {
        ArgumentNullException.ThrowIfNull(source);

        var totalSamples = (int)MathF.Round(seconds * source.SampleRate);
        var result = new float[totalSamples];

        var offset = 0;
        while (offset < totalSamples)
        {
            // The last chunk is short when the length does not divide evenly, rather than
            // over-reading into a buffer the caller does not have.
            var length = Math.Min(chunkSamples, totalSamples - offset);
            source.Read(result.AsSpan(offset, length));
            offset += length;
        }

        return result;
    }
}
