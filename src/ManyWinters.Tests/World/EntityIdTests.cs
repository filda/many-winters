using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

public class EntityIdTests
{
    [Fact]
    public void NextGuidIsTheSameForTheSameSeedInTheSameOrder()
    {
        var first = new Random(42);
        var second = new Random(42);

        Assert.Equal(EntityId.NextGuid(first), EntityId.NextGuid(second));
        Assert.Equal(EntityId.NextGuid(first), EntityId.NextGuid(second));
    }

    [Fact]
    public void NextGuidNeverRepeatsWithinOneGenerator()
    {
        var rng = new Random(42);

        var drawn = Enumerable.Range(0, 1000).Select(_ => EntityId.NextGuid(rng)).ToList();

        Assert.Equal(drawn.Count, drawn.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, drawn);
    }

    [Fact]
    public void SeedOfIsTheIdsFirstFourBytesLittleEndian()
    {
        var id = new Guid(new byte[] { 0x78, 0x56, 0x34, 0x12, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9 });

        Assert.Equal(0x12345678, EntityId.SeedOf(id));
    }

    [Fact]
    public void SeedOfTheEmptyIdIsZero()
    {
        Assert.Equal(0, EntityId.SeedOf(Guid.Empty));
    }

    [Fact]
    public void TestIdsPersonHasExactlyTheAskedForSeed()
    {
        Assert.Equal(7, TestIds.Person(7).Seed);
        Assert.Equal(-1, TestIds.Person(-1).Seed);
    }

    [Fact]
    public void NewIdsAreDistinctAndNeverEmpty()
    {
        Assert.NotEqual(PersonId.New(), PersonId.New());
        Assert.NotEqual(ResourceNodeId.New(), ResourceNodeId.New());
        Assert.NotEqual(Guid.Empty, PersonId.New().Value);
    }

    [Fact]
    public void SeededIdsFollowTheirGenerator()
    {
        Assert.Equal(PersonId.New(new Random(5)), PersonId.New(new Random(5)));
        Assert.Equal(ResourceNodeId.New(new Random(5)).Value, PersonId.New(new Random(5)).Value);
    }
}
