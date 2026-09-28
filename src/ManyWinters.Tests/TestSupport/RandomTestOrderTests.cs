namespace ManyWinters.Tests.TestSupport;

public class RandomTestOrderTests
{
    [Fact]
    public void ShuffleKeepsEveryItemExactlyOnce()
    {
        var items = Enumerable.Range(0, 50).ToArray();

        var shuffled = RandomTestOrder.Shuffle(items, new Random(7));

        Assert.Equal(items, shuffled.Order());
    }

    [Fact]
    public void ShuffleChangesTheOrder()
    {
        var items = Enumerable.Range(0, 50).ToArray();

        var shuffled = RandomTestOrder.Shuffle(items, new Random(7));

        Assert.NotEqual(items, shuffled);
    }

    [Fact]
    public void TheSameSeedGivesTheSameOrder()
    {
        var items = Enumerable.Range(0, 50).ToArray();

        Assert.Equal(RandomTestOrder.Shuffle(items, new Random(7)), RandomTestOrder.Shuffle(items, new Random(7)));
    }

    [Fact]
    public void ShuffleLeavesTheInputAlone()
    {
        var items = Enumerable.Range(0, 50).ToArray();

        RandomTestOrder.Shuffle(items, new Random(7));

        Assert.Equal(Enumerable.Range(0, 50), items);
    }
}
