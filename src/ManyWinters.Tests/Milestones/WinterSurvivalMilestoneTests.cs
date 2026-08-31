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
        person.KnownTechniques.Add(TestCatalogs.BasicForaging);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 1_000_000f);

        for (var tick = 0; tick < WinterStartTick; tick++)
        {
            world.Advance(1);
            if (tick % 10 == 0)
            {
                // IdleTask can wander a person away from the node between manual actions (see
                // WorldState.Advance) - this test is about the gather/eat/hunger loop, not
                // about walking back, so it puts them right back at the node rather than
                // simulating that walk.
                person.Position = node.Position;

                // Gathering only fills the inventory now (see GatherCommand) - eating it back
                // down is a separate, explicit step, same as a real player would do.
                world.Execute(new GatherCommand(person.Id, node.Id));
                world.Execute(new EatCommand(person.Id, TestCatalogs.AppleItem));
            }
        }

        Assert.Equal(Season.Winter, world.CurrentSeason);
        Assert.True(person.IsAlive);

        // The gather-more-than-you-need-right-now loop above leaves a leftover stockpile in
        // the inventory - harmless before WorldState.Advance auto-ate from it every tick (see
        // TryAutoEat), but that stockpile alone would now keep a person fed through the 50
        // winter ticks below with no further action at all, which isn't what "stops gathering
        // right as winter begins" is meant to test. Clearing it models someone who's truly out
        // of reserves, not just someone who stopped topping them up.
        person.Inventory.Remove(TestCatalogs.AppleItem, person.Inventory.Get(TestCatalogs.AppleItem));

        // Standing right at an all-but-infinite apple node, WorldState.Advance's own idle AI
        // (DecideIdleTask) would otherwise have them autonomously resume gathering and eating
        // from it the moment hunger crosses its threshold - correct behavior in the real game,
        // but it defeats this test's actual premise of nothing being reachable any more.
        // Moved beyond IdleSearchRadius so no resource node is a candidate.
        person.Position = new Position(10_000, 10_000);

        world.Advance(50);

        Assert.False(person.IsAlive);
    }
}
