using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Tasks;

public class FleeTaskTests
{
    private static readonly SpeciesDefinition.FleeDefinition Flee = new(FleeDistance: 8f, SafeDistance: 16f, SpeedPerTick: 0.6f);

    private static Person NewPerson(string name, Position position) =>
        new() { Name = name, BirthTick = 0, Position = position, Mother = Person.Unknown, Father = Person.Unknown, Sex = TestPeople.AnySex };

    [Fact]
    public void RemembersItsThreat()
    {
        var threat = NewPerson("Ava", new Position(0, 0));

        var task = new FleeTask(threat, Flee);

        Assert.Same(threat, task.Threat);
    }

    [Fact]
    public void MovesDirectlyAwayFromTheThreatBySpeedPerTick()
    {
        var threat = NewPerson("Ava", new Position(0, 0));
        var deer = NewPerson("Deer", new Position(1, 0));
        var task = new FleeTask(threat, Flee);

        task.Advance(deer);

        Assert.Equal(new Position(1 + Flee.SpeedPerTick, 0), deer.Position);
    }

    [Fact]
    public void MovesAwayProportionallyOnADiagonal()
    {
        var threat = NewPerson("Ava", new Position(0, 0));
        var deer = NewPerson("Deer", new Position(3, 4));
        var task = new FleeTask(threat, Flee);

        task.Advance(deer);

        // Unit vector away from the threat is (3/5, 4/5); one step at SpeedPerTick 0.6 is (0.36, 0.48).
        Assert.Equal(3.36, deer.Position.X, precision: 5);
        Assert.Equal(4.48, deer.Position.Y, precision: 5);
    }

    [Fact]
    public void IsNotCompleteWhileCloserThanSafeDistance()
    {
        var threat = NewPerson("Ava", new Position(0, 0));
        var deer = NewPerson("Deer", new Position(1, 0));
        var task = new FleeTask(threat, Flee);

        task.Advance(deer);

        Assert.False(task.IsComplete);
    }

    [Fact]
    public void BecomesCompleteOnceTheGapReachesSafeDistance()
    {
        var threat = NewPerson("Ava", new Position(0, 0));
        var deer = NewPerson("Deer", new Position(1, 0));
        var task = new FleeTask(threat, Flee);

        for (var i = 0; i < 200 && !task.IsComplete; i++)
        {
            task.Advance(deer);
        }

        Assert.True(task.IsComplete);
        Assert.True(WorldState.Distance(deer.Position, threat.Position) >= Flee.SafeDistance);
    }

    [Fact]
    public void BecomesCompleteOnceTheThreatDies()
    {
        var threat = NewPerson("Ava", new Position(0, 0));
        var deer = NewPerson("Deer", new Position(1, 0));
        var task = new FleeTask(threat, Flee);
        task.Advance(deer);
        Assert.False(task.IsComplete);

        threat.IsAlive = false;
        task.Advance(deer);

        Assert.True(task.IsComplete);
    }

    [Fact]
    public void StandsStillWhenAlreadyExactlyOnTheThreat()
    {
        var threat = NewPerson("Ava", new Position(2, 2));
        var deer = NewPerson("Deer", new Position(2, 2));
        var task = new FleeTask(threat, Flee);

        task.Advance(deer);

        Assert.Equal(new Position(2, 2), deer.Position);
        Assert.False(task.IsComplete);
    }
}
