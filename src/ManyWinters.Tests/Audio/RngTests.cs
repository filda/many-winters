using ManyWinters.Audio;

namespace ManyWinters.Tests.Audio;

public class RngTests
{
    [Fact]
    public void SameSeedProducesTheSameSequence()
    {
        var first = Draw(new Rng(4711), 100);
        var second = Draw(new Rng(4711), 100);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentSeedsProduceDifferentSequences()
    {
        var first = Draw(new Rng(4711), 100);
        var second = Draw(new Rng(4712), 100);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void FloatsStayInsideTheUnitInterval()
    {
        var values = Draw(new Rng(-31337), 10_000);

        Assert.All(values, value => Assert.InRange(value, 0.0f, 0.99999994f));
    }

    // A seed of zero is the one value a bare xorshift cannot recover from: its state stays zero
    // and every draw is zero forever.
    [Fact]
    public void ZeroSeedStillProducesVariation()
    {
        var values = Draw(new Rng(0), 100);

        Assert.True(values.Distinct().Count() > 90);
    }

    [Fact]
    public void GaussianDrawsAreDeterministicForASeed()
    {
        var first = DrawGaussian(new Rng(97), 100);
        var second = DrawGaussian(new Rng(97), 100);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GaussianDrawsAreCentredWithUnitDeviation()
    {
        var values = DrawGaussian(new Rng(5150), 100_000);

        var mean = values.Average();
        var deviation = Math.Sqrt(values.Average(value => (value - mean) * (value - mean)));

        Assert.InRange(mean, -0.02, 0.02);
        Assert.InRange(deviation, 0.98, 1.02);
    }

    private static float[] Draw(Rng rng, int count) =>
        Enumerable.Range(0, count).Select(_ => rng.NextFloat()).ToArray();

    private static double[] DrawGaussian(Rng rng, int count) =>
        Enumerable.Range(0, count).Select(_ => (double)rng.NextGaussian()).ToArray();
}
