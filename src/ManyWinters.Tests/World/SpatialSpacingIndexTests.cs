using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class SpatialSpacingIndexTests
{
    private static SpatialSpacingIndex<Position> NewIndex(double cellSize = 1.0) =>
        new(cellSize, p => p.X, p => p.Y);

    [Fact]
    public void AnEmptyIndexIsNeverTooClose()
    {
        var index = NewIndex();

        Assert.False(index.IsTooClose(0, 0, _ => 5.0));
    }

    [Fact]
    public void RejectsACandidateWithinTheGapOfAnAddedItem()
    {
        var index = NewIndex();
        index.Add(new Position(0, 0));

        Assert.True(index.IsTooClose(0.5, 0, _ => 1.0));
    }

    [Fact]
    public void AcceptsACandidateOutsideTheGap()
    {
        var index = NewIndex();
        index.Add(new Position(0, 0));

        Assert.False(index.IsTooClose(2.0, 0, _ => 1.0));
    }

    [Fact]
    public void ChecksNeighbouringCellsNotJustTheCandidatesOwnCell()
    {
        // (0.9, 0) and (1.1, 0) sit in different cells but 0.2 apart; only the 3x3
        // neighbourhood scan catches this.
        var index = NewIndex(cellSize: 1.0);
        index.Add(new Position(0.9, 0));

        Assert.True(index.IsTooClose(1.1, 0, _ => 1.0));
    }

    [Fact]
    public void TheRequiredGapCanDependOnWhichExistingItemIsChecked()
    {
        var index = NewIndex();
        index.Add(new Position(0.5, 0));

        Assert.True(index.IsTooClose(0, 0, existing => existing.X + 0.4));
        Assert.False(index.IsTooClose(0, 0, existing => existing.X - 0.4));
    }

    [Fact]
    public void EachAddedItemIsCheckedOnItsOwn()
    {
        var index = NewIndex();
        index.Add(new Position(0, 0));
        index.Add(new Position(5, 0));

        Assert.True(index.IsTooClose(0.5, 0, _ => 1.0));
        Assert.True(index.IsTooClose(5.5, 0, _ => 1.0));
        Assert.False(index.IsTooClose(2.5, 0, _ => 1.0));
    }

    [Fact]
    public void AddingAnItemDoesNotAffectEarlierQueriesResults()
    {
        // IsTooClose only sees what has already been added; callers check a candidate before
        // keeping it.
        var index = NewIndex();

        Assert.False(index.IsTooClose(0, 0, _ => 1.0));

        index.Add(new Position(0.5, 0));

        Assert.True(index.IsTooClose(0, 0, _ => 1.0));
    }
}
