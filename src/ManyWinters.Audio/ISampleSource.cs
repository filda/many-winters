namespace ManyWinters.Audio;

// Endless sounds: pulled a buffer at a time, parameters may change between pulls. The tool
// renders one to WAV by pulling it for N seconds; the game pushes the same buffers into
// AudioStreamGenerator. One code path, two consumers.
public interface ISampleSource
{
    int SampleRate { get; }

    void Read(Span<float> buffer);
}
