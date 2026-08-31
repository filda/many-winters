using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Milestones;

/// <summary>
/// Roadmap Step 6 ("First Headless + Visual Milestone"): can 10-20 people survive
/// through repeated gathering, and do they actually starve without it? Written before
/// seasons existed (Step 9); the 300-tick run now spans a full year including one
/// winter, so this doubles as a basic "survive the winter" check for regular gathering.
/// See <see cref="WinterSurvivalMilestoneTests"/> for a check targeted specifically at
/// winter's harsher pressure.
/// </summary>
public class SurvivalMilestoneTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void PeopleSurviveThreeHundredTicksWithRegularGathering(int populationSize)
    {
        var world = TestCatalogs.CreateWorld();
        var people = new List<Person>();
        for (var i = 0; i < populationSize; i++)
        {
            var person = world.AddPerson($"Person {i + 1}", new Position(0, 0));
            // Gathering and eating both have to be learned now (see SkillDefinition.
            // BaseTechnique) - granted directly here since this test is about the
            // gather/eat/hunger loop itself, not about how that knowledge would spread.
            person.KnownTechniques.Add(TestCatalogs.BasicForaging);
            person.KnownTechniques.Add(TestCatalogs.BasicEating);
            people.Add(person);
        }

        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 1_000_000f);

        for (var tick = 0; tick < 300; tick++)
        {
            world.Advance(1);
            if (tick % 10 == 0)
            {
                foreach (var person in people)
                {
                    // IdleTask can wander a person away from the node between manual actions
                    // (see WorldState.Advance) - this test is about the gather/eat/hunger
                    // loop, not about walking back, so it puts them right back at the node
                    // rather than simulating that walk.
                    person.Position = node.Position;

                    // Gathering only fills the inventory now (see GatherCommand) - eating it
                    // back down is a separate, explicit step, same as a real player would do.
                    world.Execute(new GatherCommand(person.Id, node.Id));
                    world.Execute(new EatCommand(person.Id, TestCatalogs.AppleItem));
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
