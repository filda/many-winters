using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class WavWriterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".wav");

    [Fact]
    public void WritesACanonicalMono16BitHeader()
    {
        WavWriter.Write(_path, new float[100], 22050);

        var bytes = File.ReadAllBytes(_path);

        Assert.Equal("RIFF", Text(bytes, 0));
        Assert.Equal(36 + 200, BitConverter.ToInt32(bytes, 4));
        Assert.Equal("WAVE", Text(bytes, 8));
        Assert.Equal("fmt ", Text(bytes, 12));
        Assert.Equal(16, BitConverter.ToInt32(bytes, 16));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 20));
        Assert.Equal(1, BitConverter.ToInt16(bytes, 22));
        Assert.Equal(22050, BitConverter.ToInt32(bytes, 24));
        Assert.Equal(44100, BitConverter.ToInt32(bytes, 28));
        Assert.Equal(2, BitConverter.ToInt16(bytes, 32));
        Assert.Equal(16, BitConverter.ToInt16(bytes, 34));
        Assert.Equal("data", Text(bytes, 36));
        Assert.Equal(200, BitConverter.ToInt32(bytes, 40));
    }

    [Fact]
    public void HeaderCarriesTheRateItWasGiven()
    {
        WavWriter.Write(_path, new float[10], 44100);

        var bytes = File.ReadAllBytes(_path);

        Assert.Equal(44100, BitConverter.ToInt32(bytes, 24));
        Assert.Equal(88200, BitConverter.ToInt32(bytes, 28));
    }

    [Fact]
    public void FileLengthIsTheHeaderPlusTwoBytesPerSample()
    {
        WavWriter.Write(_path, new float[777], 22050);

        Assert.Equal(44 + (777 * 2), new FileInfo(_path).Length);
    }

    [Fact]
    public void SamplesAreScaledToFullScaleAndClipped()
    {
        WavWriter.Write(_path, [0.0f, 1.0f, -1.0f, 4.2f, -4.2f, 0.5f], 22050);

        var bytes = File.ReadAllBytes(_path);
        var samples = Enumerable.Range(0, 6).Select(i => BitConverter.ToInt16(bytes, 44 + (i * 2))).ToArray();

        short[] expected = [0, 32767, -32767, 32767, -32767, 16383];
        Assert.Equal(expected, samples);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        File.Delete(_path);
    }

    private static string Text(byte[] bytes, int offset) =>
        System.Text.Encoding.ASCII.GetString(bytes, offset, 4);
}
