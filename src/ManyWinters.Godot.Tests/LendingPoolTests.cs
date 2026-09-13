using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

public class LendingPoolTests
{
    // A stand-in for the pooled ShaderMaterial: Resource-derived types cannot be touched here
    // (see this project's README), which is why the pool is generic.
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
        // An entity's layers are lent to at the same time; sharing one material would have them
        // fight over its uniforms.
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
        // Four out, all returned, four out again reuses all four rather than making a fifth -
        // what keeps hovering from leaving a material behind per entity ever hovered.
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
