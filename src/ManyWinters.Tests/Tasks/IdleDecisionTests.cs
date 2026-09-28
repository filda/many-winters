using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Tasks;

// Direct calls to IdleDecision.Reconsider rather than through Advance - WorldStateTests already
// covers the same behaviour through a full tick, and stays as is.
public class IdleDecisionTests
{
    [Fact]
    public void ACreatureWithNothingToDoGetsAnIdleTask()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

        IdleDecision.Reconsider(world, person, world.Clock.CurrentTick);

        Assert.IsType<IdleTask>(person.Tasks.Current);
    }

    [Fact]
    public void APlayerIssuedMoveTaskIsLeftAlone()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var move = new MoveTask(new Position(10, 0), world.Configuration.Rules.SpeedPerTick);
        person.Tasks.Interrupt(move);

        IdleDecision.Reconsider(world, person, world.Clock.CurrentTick);

        Assert.Same(move, person.Tasks.Current);
    }

    [Fact]
    public void AHungryPersonWhoKnowsHowToEatWithFoodInRangeGetsSentToGatherIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.Needs.Hunger = 60f;
        var foodNode = world.SpawnResourceNode(TestCatalogs.Apple, new Position(30, 0), 100f);

        IdleDecision.Reconsider(world, person, world.Clock.CurrentTick);

        var task = Assert.IsType<GatherTask>(person.Tasks.Current);
        Assert.Equal(foodNode.Id, task.Target.Id);
    }
}
