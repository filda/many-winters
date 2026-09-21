using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Milestones;

/// <summary>
/// Can 10-20 people survive on repeated gathering, and do they starve without it? The 300-tick
/// run spans a full year including one winter; <see cref="WinterSurvivalMilestoneTests"/>
/// targets winter's pressure specifically.
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
            var person = world.SpawnPerson($"Person {i + 1}", new Position(0, 0));
            // Gathering and eating have to be learned; granted directly because this test is
            // about the gather/eat/hunger loop itself.
            person.KnownTechniques.Add(TestCatalogs.BasicForaging);
            person.KnownTechniques.Add(TestCatalogs.BasicEating);
            people.Add(person);
        }

        var node = world.SpawnResourceNode(TestCatalogs.Apple, new Position(0, 0), 1_000_000f);

        for (var tick = 0; tick < 300; tick++)
        {
            world.Advance(1);
            if (tick % 10 == 0)
            {
                foreach (var person in people)
                {
                    // A person can wander off between manual actions; put them back rather than
                    // simulate the walk.
                    person.Position = node.Position;

                    // Gathering only fills the inventory; eating is a separate step.
                    world.Execute(new GatherCommand(person, node));
                    world.Execute(new EatCommand(person, TestCatalogs.AppleItem));
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
        var world = TestCatalogs.CreateWorld();
        var people = new List<Person>();
        for (var i = 0; i < populationSize; i++)
        {
            people.Add(world.SpawnPerson($"Person {i + 1}", new Position(0, 0)));
        }

        world.Advance(150);

        Assert.All(people, p => Assert.False(p.IsAlive));
    }
}
