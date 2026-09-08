using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class FreePositionSearchTests
{
    // A generator handing out 0, 1, 2, ... on the X axis, so a test can say which draw a
    // returned position came from just by reading its X.
    private static Func<Position> Counting(List<Position> drawn) =>
        () =>
        {
            var candidate = new Position(drawn.Count, 0);
            drawn.Add(candidate);
            return candidate;
        };

    [Fact]
    public void TakesTheFirstCandidateWhenItsSpotIsFree()
    {
        var drawn = new List<Position>();

        var found = FreePositionSearch.Find(Counting(drawn), _ => true, maxAttempts: 20);

        Assert.Equal(new Position(0, 0), found);
        Assert.Single(drawn);
    }

    [Fact]
    public void KeepsDrawingWhileTheSpotIsTaken()
    {
        var drawn = new List<Position>();

        var found = FreePositionSearch.Find(Counting(drawn), candidate => candidate.X >= 3, maxAttempts: 20);

        Assert.Equal(new Position(3, 0), found);
        Assert.Equal(4, drawn.Count);
    }

    [Fact]
    public void GivesUpAfterTheAttemptBudgetAndReturnsTheLastCandidateDrawn()
    {
        var drawn = new List<Position>();

        var found = FreePositionSearch.Find(Counting(drawn), _ => false, maxAttempts: 5);

        Assert.Equal(new Position(5, 0), found);
        Assert.Equal(6, drawn.Count);
    }
}
