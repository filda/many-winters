using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Milestones;

/// <summary>
/// Winter's pressure is real, not a label: someone fine all year starves once they stop
/// gathering as winter begins. Distinct from
/// <see cref="SurvivalMilestoneTests.PeopleStarveWithoutAnyGathering"/>, which never gathers.
/// </summary>
public class WinterSurvivalMilestoneTests
{
    private const int WinterStartTick = 225;

    [Fact]
    public void PeopleWhoStopGatheringRightAsWinterBeginsStarveDuringIt()
    {
        // The 50-tick winter window is sized to the shared threshold; a hardy person's own
        // MaxHunger draw (SimulationRules.MaxHungerFor) could ride out the margin.
        var world = TestCatalogs.CreateWorldWithoutHungerVariation();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 1_000_000f);

        for (var tick = 0; tick < WinterStartTick; tick++)
        {
            world.Advance(1);
            if (tick % 10 == 0)
            {
                // IdleTask can wander a person off between manual actions; put them back rather
                // than simulate the walk.
                person.Position = node.Position;

                // Gathering only fills the inventory (GatherCommand); eating is a separate step.
                world.Execute(new GatherCommand(person, node));
                world.Execute(new EatCommand(person, TestCatalogs.AppleItem));
            }
        }

        Assert.Equal(Season.Winter, world.CurrentSeason);
        Assert.True(person.IsAlive);

        // The loop above leaves a stockpile that TryAutoEat would live on through the 50 winter
        // ticks; clearing it models someone truly out of reserves.
        person.Inventory.Remove(TestCatalogs.AppleItem, person.Inventory.Get(TestCatalogs.AppleItem));

        // Next to an all-but-infinite apple node, DecideIdleTask would resume gathering once
        // hunger crosses its threshold; moved beyond IdleSearchRadius so nothing is reachable.
        person.Position = new Position(10_000, 10_000);

        world.Advance(50);

        Assert.False(person.IsAlive);
    }
}
