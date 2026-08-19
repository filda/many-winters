using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Milestones;

/// <summary>
/// Roadmap Step 9 ("Seasons and First Winter"): proves winter's pressure is real rather
/// than a season label with no teeth — someone who was fine all year starves once they
/// stop gathering right as winter begins, distinct from <see cref="SurvivalMilestoneTests.PeopleStarveWithoutAnyGathering"/>,
/// which never gathers at all.
/// </summary>
public class WinterSurvivalMilestoneTests
{
    private const int WinterStartTick = 225;

    [Fact]
    public void PeopleWhoStopGatheringRightAsWinterBeginsStarveDuringIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 1_000_000f);

        for (var tick = 0; tick < WinterStartTick; tick++)
        {
            world.Advance(1);
            if (tick % 10 == 0)
            {
                world.Execute(new GatherCommand(person.Id, node.Id));
            }
        }

        Assert.Equal(Season.Winter, world.CurrentSeason);
        Assert.True(person.IsAlive);

        world.Advance(50);

        Assert.False(person.IsAlive);
    }
}
