using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Milestones;

/// <summary>
/// Roadmap Step 6 ("First Headless + Visual Milestone"): can 10-20 people survive
/// through repeated gathering, and do they actually starve without it? Seasons/winter
/// don't exist yet (Step 9), so this validates the underlying survival pressure instead
/// of a literal winter.
/// </summary>
public class SurvivalMilestoneTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void PeopleSurviveThreeHundredTicksWithRegularGathering(int populationSize)
    {
        var world = new WorldState();
        var people = new List<Person>();
        for (var i = 0; i < populationSize; i++)
        {
            people.Add(world.AddPerson($"Person {i + 1}", new Position(0, 0)));
        }

        var node = world.AddResourceNode(ResourceKind.Apple, new Position(0, 0), 1_000_000f);

        for (var tick = 0; tick < 300; tick++)
        {
            world.Advance(1);
            if (tick % 10 == 0)
            {
                foreach (var person in people)
                {
                    world.Execute(new GatherCommand(person.Id, node.Id));
                }
            }
        }

        Assert.All(people, p => Assert.True(p.IsAlive));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void PeopleStarveWithoutAnyGathering(int populationSize)
    {
        var world = new WorldState();
        var people = new List<Person>();
        for (var i = 0; i < populationSize; i++)
        {
            people.Add(world.AddPerson($"Person {i + 1}", new Position(0, 0)));
        }

        world.Advance(150);

        Assert.All(people, p => Assert.False(p.IsAlive));
    }
}
