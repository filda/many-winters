using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class LendingPoolTests
{
    // A stand-in for the ShaderMaterial the hover rim actually pools - the rule against
    // touching Resource-derived types here is exactly why the pool is generic (see the test
    // project's README).
    private sealed class Lent
    {
    }

    private static LendingPool<Lent> CountingPool(out Func<int> created)
    {
        var count = 0;
        created = () => count;

        return new LendingPool<Lent>(() =>
        {
            count++;
            return new Lent();
        });
    }

    [Fact]
    public void AFreshPoolHoldsNothing()
    {
        var pool = CountingPool(out var created);

        Assert.Equal(0, pool.Idle);
        Assert.Equal(0, created());
    }

    [Fact]
    public void TakingFromAnEmptyPoolMakesANewOne()
    {
        var pool = CountingPool(out var created);

        Assert.NotNull(pool.Take());
        Assert.Equal(1, created());
    }

    [Fact]
    public void TwoUsersAtOnceGetTwoDifferentObjects()
    {
        // What the pool has to guarantee above all else: an entity's layers are lent to at the
        // same time, and handing the same material to two of them would have them fight over
        // its uniforms.
        var pool = CountingPool(out var created);

        var first = pool.Take();
        var second = pool.Take();

        Assert.NotSame(first, second);
        Assert.Equal(2, created());
    }

    [Fact]
    public void WhatComesBackIsLentOutAgainRatherThanRemade()
    {
        var pool = CountingPool(out var created);
        var borrowed = pool.Take();

        pool.Return(borrowed);

        Assert.Same(borrowed, pool.Take());
        Assert.Equal(1, created());
    }

    [Fact]
    public void ReturningLeavesItIdleUntilSomethingTakesIt()
    {
        var pool = CountingPool(out _);
        var borrowed = pool.Take();
        Assert.Equal(0, pool.Idle);

        pool.Return(borrowed);
        Assert.Equal(1, pool.Idle);

        pool.Take();
        Assert.Equal(0, pool.Idle);
    }

    [Fact]
    public void ThePoolOnlyEverGrowsToTheWidestSimultaneousUse()
    {
        // Four layers out at once, all handed back, then four out again: the second round
        // reuses all four rather than making a fifth. This is the property that keeps one
        // hovered entity from leaving a material behind per entity it ever hovered.
        var pool = CountingPool(out var created);

        var round = new[] { pool.Take(), pool.Take(), pool.Take(), pool.Take() };
        foreach (var item in round)
        {
            pool.Return(item);
        }

        for (var i = 0; i < 4; i++)
        {
            pool.Take();
        }

        Assert.Equal(4, created());
    }
}
