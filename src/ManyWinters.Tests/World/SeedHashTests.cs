using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class SeedHashTests
{
    [Theory]
    [InlineData(0u, 0)]
    [InlineData(1u, 824515495)]
    [InlineData(2u, 1722258072)]
    [InlineData(123456789u, 1952335732)]
    [InlineData(uint.MaxValue, 539527247)]
    public void AvalancheIsAFixedFunctionOfItsInput(uint value, int expected)
    {
        // A saved world has to look and behave the same when reloaded, on any runtime, so
        // what this produces is behaviour rather than an implementation detail. Regenerate
        // these deliberately if the hash is meant to change; don't relax them.
        Assert.Equal(expected, SeedHash.Avalanche(value));
    }

    [Fact]
    public void AdjacentSeedsComeOutFarApart()
    {
        // The whole point: System.Random on neighbouring small seeds draws eerily similar
        // first values, which reads as people wandering in step. Spread apart, consecutive
        // seeds have nothing to do with one another.
        var first = SeedHash.Avalanche(1);
        var second = SeedHash.Avalanche(2);
        var third = SeedHash.Avalanche(3);

        Assert.True(Math.Abs((long)first - second) > 100_000_000);
        Assert.True(Math.Abs((long)second - third) > 100_000_000);
    }

    [Fact]
    public void EveryDistinctSeedInALongRunComesOutDistinct()
    {
        var seen = new HashSet<int>();

        for (var seed = 0u; seed < 10_000; seed++)
        {
            Assert.True(seen.Add(SeedHash.Avalanche(seed)), $"seed {seed} collided");
        }
    }
}
