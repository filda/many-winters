using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class EnvelopeTests
{
    [Fact]
    public void AttackRampReachesUnityAtAttackSeconds()
    {
        const int sampleRate = 22050;
        const float attackSeconds = 0.017f;

        var envelope = new Envelope(sampleRate, attackSeconds, 0.5f);
        var attackSamples = (int)(attackSeconds * sampleRate);

        float value = 0.0f;
        for (var i = 0; i <= attackSamples; i++)
        {
            value = envelope.Next();
        }

        Assert.InRange(value, 0.99f, 1.0f);
    }

    [Fact]
    public void LevelAtAttackPlusT60IsSixtyDecibelsDown()
    {
        const int sampleRate = 22050;
        const float attackSeconds = 0.011f;
        const float t60Seconds = 0.23f;

        var envelope = new Envelope(sampleRate, attackSeconds, t60Seconds);
        var totalSamples = (int)((attackSeconds + t60Seconds) * sampleRate);

        float value = 0.0f;
        for (var i = 0; i < totalSamples; i++)
        {
            value = envelope.Next();
        }

        Assert.InRange(value, 0.0009f, 0.0011f);
    }
}
