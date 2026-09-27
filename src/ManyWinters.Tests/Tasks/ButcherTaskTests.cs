using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Tasks;

// ButcherTask walks to a carcass exactly the way GatherTask walks to a pile
// (docs/todo/fauna-plan.md, phase 3) - a carcass lies where it fell, it does not move.
public class ButcherTaskTests
{
    private static readonly Position CarcassPosition = new(10, 10);

    private static readonly float Reach = SimulationRules.Default.PileReachDistance;

    // Most tests here are about the walk itself, not about which speed installed it, so they all
    // share the idle AI's own unhurried pace (WorldState.DecideIdleTask) unless the test says
    // otherwise.
    private const float IdleSpeed = GatherTask.SpeedPerTick;

    private static Person NewButcher(Position position) =>
        new() { Name = "Ava", BirthTick = 0, Position = position, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex };

    private static Animal NewCarcass() =>
        new(TestCatalogs.DeerSpeciesId, new HomeRange(CarcassPosition) { Radius = 10f, DriftMetresPerSeason = 0f })
        {
            BirthTick = 0,
            Sex = Sex.Female,
            Position = CarcassPosition,
            IsAlive = false,
        };

    private static ButcherTask NewTask(float? reach = null, Animal? carcass = null, float speedPerTick = IdleSpeed) =>
        new(carcass ?? NewCarcass(), reach ?? Reach, speedPerTick);

    [Fact]
    public void IsNeverComplete()
    {
        var task = NewTask();
        var butcher = NewButcher(new Position(30, 10));

        for (var i = 0; i < 200; i++)
        {
            task.Advance(butcher);
            Assert.False(task.IsComplete);
        }
    }

    [Fact]
    public void RemembersWhatItWasSentTo()
    {
        var carcass = NewCarcass();

        var task = NewTask(carcass: carcass);

        Assert.Same(carcass, task.Carcass);
        Assert.Equal(Reach, task.Reach);
        Assert.Equal(IdleSpeed, task.SpeedPerTick);
    }

    // The bug this constructor parameter fixes (docs/todo/fauna-plan.md, phase 3, "rozhodnuto
    // 2026-09-27"): a player-directed butchering (TargetActions, MoveCommand.SpeedPerTick) has to
    // close the gap faster than the autonomous idle AI's own unhurried pace
    // (WorldState.DecideIdleTask, GatherTask.SpeedPerTick) - both used to hard-code the slower one
    // regardless of who sent the butcher.
    [Fact]
    public void ADirectedButcheringClosesTheDistanceFasterThanAnIdleOne()
    {
        var directedTask = NewTask(speedPerTick: MoveCommand.SpeedPerTick);
        var directedButcher = NewButcher(new Position(30, 10));
        var idleTask = NewTask();
        var idleButcher = NewButcher(new Position(30, 10));

        directedTask.Advance(directedButcher);
        idleTask.Advance(idleButcher);

        Assert.True(
            directedButcher.Position.X < idleButcher.Position.X,
            $"Directed butcher reached x={directedButcher.Position.X}, no closer than idle butcher's x={idleButcher.Position.X}.");
    }

    [Fact]
    public void WalksIntoReachOfTheCarcass()
    {
        var butcher = NewButcher(new Position(30, 10));
        var task = NewTask();

        for (var i = 0; i < 200; i++)
        {
            task.Advance(butcher);
        }

        Assert.True(
            WorldState.Distance(butcher.Position, CarcassPosition) <= Reach,
            $"Ended up {WorldState.Distance(butcher.Position, CarcassPosition)} away, out of butchering reach.");
    }

    [Fact]
    public void StaysPutWhenItStartsWithinReach()
    {
        var start = new Position(CarcassPosition.X + (Reach * 0.5), CarcassPosition.Y);
        var butcher = NewButcher(start);
        var task = NewTask();

        for (var i = 0; i < 20; i++)
        {
            task.Advance(butcher);
        }

        Assert.Equal(start, butcher.Position);
    }
}
