using ManyWinters.Core.Population.Naming;

namespace ManyWinters.Tests.Population.Naming;

public class WeightedSetTests
{
    [Fact]
    public void AnEmptySetCannotBeSampled()
    {
        var set = new WeightedSet<string>();

        Assert.True(set.IsEmpty);
        Assert.Throws<InvalidOperationException>(() => set.Sample(new Random(1)));
    }

    [Fact]
    public void ANonPositiveWeightIsIgnored()
    {
        var set = new WeightedSet<string>();

        set.Add("a", 0f);
        set.Add("a", -1f);

        Assert.True(set.IsEmpty);
    }

    [Fact]
    public void AddingTheSameItemTwiceAccumulatesItsWeight()
    {
        var set = new WeightedSet<string>();

        set.Add("a", 1f);
        set.Add("a", 2f);

        Assert.Equal(3f, set.TotalWeight);
        Assert.Equal(("a", 3f), set.Enumerate().Single());
    }

    [Fact]
    public void SampleAlwaysReturnsAnAddedItem()
    {
        var set = new WeightedSet<string>();
        set.Add("a", 1f);
        set.Add("b", 1f);
        var allowed = new[] { "a", "b" };

        var rng = new Random(7);
        for (var i = 0; i < 100; i++)
        {
            Assert.Contains(set.Sample(rng), allowed);
        }
    }

    [Fact]
    public void AnItemWithAllTheWeightIsAlwaysSampled()
    {
        var set = new WeightedSet<string>();
        set.Add("a", 1f);
        set.Add("b", 1000f);

        var rng = new Random(7);
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal("b", set.Sample(rng));
        }
    }

    [Fact]
    public void DecayAllShrinksEveryWeightAndTheTotalBySameFactor()
    {
        var set = new WeightedSet<string>();
        set.Add("a", 2f);
        set.Add("b", 4f);

        set.DecayAll(0.5f);

        Assert.Equal(3f, set.TotalWeight);
        Assert.Equal([("a", 1f), ("b", 2f)], set.Enumerate().ToList());
    }

    [Fact]
    public void SamplingIsDeterministicForTheSameSeed()
    {
        WeightedSet<string> BuildSet()
        {
            var set = new WeightedSet<string>();
            set.Add("a", 1f);
            set.Add("b", 2f);
            set.Add("c", 3f);
            return set;
        }

        var first = Enumerable.Range(0, 20).Select(_ => BuildSet().Sample(new Random(42))).ToList();
        var second = Enumerable.Range(0, 20).Select(_ => BuildSet().Sample(new Random(42))).ToList();

        Assert.Equal(first, second);
    }
}
