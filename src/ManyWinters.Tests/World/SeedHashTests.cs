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
        // A saved world must reload identically on any runtime, so the output is behaviour.
        // Regenerate deliberately if the hash changes; don't relax.
        Assert.Equal(expected, SeedHash.Avalanche(value));
    }

    [Fact]
    public void AdjacentSeedsComeOutFarApart()
    {
        // System.Random on neighbouring small seeds draws similar first values, which reads as
        // people wandering in step.
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

    [Fact]
    public void StableStringHashIsAFixedFunctionOfItsInput()
    {
        // Every deterministic roll keyed off a string (a material, a technique, a skill) needs
        // the same input to hash the same way on every run and every machine - a replay must not
        // fork over string.GetHashCode()'s per-process randomization. The literal pins the
        // current djb2-xor algorithm, so a change of algorithm is caught here rather than only
        // showing up as a shifted roll somewhere else.
        Assert.Equal(SeedHash.StableStringHash("plant_fibre"), SeedHash.StableStringHash("plant_fibre"));
        Assert.Equal(-36856793, SeedHash.StableStringHash("plant_fibre"));
    }
}
