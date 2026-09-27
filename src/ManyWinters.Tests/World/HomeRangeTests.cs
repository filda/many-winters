using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class HomeRangeTests
{
    private const long TicksPerSeason = 75;

    [Fact]
    public void AnchorDoesNotMoveWithinASeason()
    {
        var home = new HomeRange(new Position(0, 0)) { Radius = 15f, DriftMetresPerSeason = 20f };

        // Tick 0 only establishes which season "now" is - a freshly spawned herd's ground must
        // not jump on the world's very first tick.
        for (var tick = 0; tick < TicksPerSeason; tick++)
        {
            home.Advance(tick, TicksPerSeason);
        }

        Assert.Equal(new Position(0, 0), home.Anchor);
    }

    [Fact]
    public void AnchorMovesExactlyDriftMetresPerSeasonOnceASeasonTurns()
    {
        var home = new HomeRange(new Position(0, 0)) { Radius = 15f, DriftMetresPerSeason = 20f };
        home.Advance(0, TicksPerSeason);

        home.Advance(TicksPerSeason, TicksPerSeason);

        Assert.Equal(20.0, WorldState.Distance(new Position(0, 0), home.Anchor), 5);
    }

    [Fact]
    public void RepeatedAdvanceCallsWithinTheSameSeasonMoveTheAnchorOnlyOnce()
    {
        var home = new HomeRange(new Position(0, 0)) { Radius = 15f, DriftMetresPerSeason = 20f };
        home.Advance(0, TicksPerSeason);

        home.Advance(TicksPerSeason, TicksPerSeason);
        var afterFirstCall = home.Anchor;

        // Several more calls that stay within the same season (season index unchanged).
        home.Advance(TicksPerSeason + 1, TicksPerSeason);
        home.Advance(TicksPerSeason + 2, TicksPerSeason);
        home.Advance((2 * TicksPerSeason) - 1, TicksPerSeason);

        Assert.Equal(afterFirstCall, home.Anchor);
    }

    [Fact]
    public void EachSeasonTurnMovesTheAnchorByExactlyDriftMetresNeverFurther()
    {
        var home = new HomeRange(new Position(0, 0)) { Radius = 15f, DriftMetresPerSeason = 20f };
        home.Advance(0, TicksPerSeason);
        var previous = home.Anchor;

        for (var season = 1; season <= 12; season++)
        {
            home.Advance(season * TicksPerSeason, TicksPerSeason);

            var moved = WorldState.Distance(previous, home.Anchor);
            Assert.Equal(20.0, moved, 5);
            previous = home.Anchor;
        }
    }

    [Fact]
    public void ZeroDriftLeavesTheAnchorWhereItStarted()
    {
        var home = new HomeRange(new Position(5, -3)) { Radius = 15f, DriftMetresPerSeason = 0f };

        for (var season = 1; season <= 6; season++)
        {
            home.Advance(season * TicksPerSeason, TicksPerSeason);
        }

        Assert.Equal(new Position(5, -3), home.Anchor);
    }

    [Fact]
    public void TwoHomeRangesDriftInDifferentDirections()
    {
        var first = new HomeRange(new Position(0, 0)) { Radius = 15f, DriftMetresPerSeason = 20f };
        var second = new HomeRange(new Position(0, 0)) { Radius = 15f, DriftMetresPerSeason = 20f };
        first.Advance(0, TicksPerSeason);
        second.Advance(0, TicksPerSeason);

        first.Advance(TicksPerSeason, TicksPerSeason);
        second.Advance(TicksPerSeason, TicksPerSeason);

        Assert.NotEqual(first.Anchor, second.Anchor);
    }

    [Fact]
    public void TheSameHomeRangeDriftsTheSameWayOnAFreshReplay()
    {
        var first = new HomeRange(new Position(0, 0)) { Id = new HomeRangeId(new Guid(7, 0, 0, new byte[8])), Radius = 15f, DriftMetresPerSeason = 20f };
        var second = new HomeRange(new Position(0, 0)) { Id = new HomeRangeId(new Guid(7, 0, 0, new byte[8])), Radius = 15f, DriftMetresPerSeason = 20f };

        for (var season = 0; season <= 4; season++)
        {
            first.Advance(season * TicksPerSeason, TicksPerSeason);
            second.Advance(season * TicksPerSeason, TicksPerSeason);
        }

        Assert.Equal(first.Anchor, second.Anchor);
    }
}
