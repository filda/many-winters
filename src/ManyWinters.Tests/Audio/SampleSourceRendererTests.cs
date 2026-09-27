using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class SampleSourceRendererTests
{
    [Fact]
    public void ReturnsSecondsTimesSampleRateSamples()
    {
        var source = new RecordingSource(sampleRate: 1000);

        var result = SampleSourceRenderer.Render(source, seconds: 2.5f, chunkSamples: 100);

        Assert.Equal(2500, result.Length);
    }

    [Fact]
    public void PullsInFixedChunksWithAShortFinalChunk()
    {
        var source = new RecordingSource(sampleRate: 1000);

        // 1000 samples requested at 300 per chunk: three full chunks and a 100-sample remainder.
        SampleSourceRenderer.Render(source, seconds: 1.0f, chunkSamples: 300);

        Assert.Equal([300, 300, 300, 100], source.ChunkSizes);
    }

    [Fact]
    public void SamplesArriveInTheOrderTheSourceProducedThem()
    {
        var source = new RecordingSource(sampleRate: 100);

        var result = SampleSourceRenderer.Render(source, seconds: 1.0f, chunkSamples: 30);

        for (var i = 0; i < result.Length; i++)
        {
            Assert.Equal(i, result[i]);
        }
    }

    // Records the size of every chunk it was asked to fill, and fills each one with an
    // incrementing counter so callers can check ordering across chunk boundaries.
    private sealed class RecordingSource(int sampleRate) : ISampleSource
    {
        private int _next;

        public List<int> ChunkSizes { get; } = [];

        public int SampleRate { get; } = sampleRate;

        public void Read(Span<float> buffer)
        {
            ChunkSizes.Add(buffer.Length);
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = _next++;
            }
        }
    }
}
