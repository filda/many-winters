using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

// Which verb a single thing in the pack answers to (docs/materials-and-crafting-architecture.md
// section 3) - asked from the item's own transitions alone, so the vocabulary growing to Tan
// costs this class one more branch, not a new kind of question.
public class ReductiveVerbsTests
{
    private static Person Person(WorldState world) =>
        world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

    [Fact]
    public void GrassAnswersToTwist()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Person(world);
        person.Inventory.Add(TestCatalogs.GrassItem, TestCatalogs.GrassPerCord);

        var trial = ReductiveVerbs.For(person, TestCatalogs.GrassItem, world.Configuration.ItemCatalog);

        Assert.NotNull(trial);
        Assert.IsType<TwistCommand>(trial.Value.Command);
    }

    [Fact]
    public void StoneAnswersToKnap()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Person(world);
        person.Inventory.Add(TestCatalogs.StoneItem, TestCatalogs.StonePerWedge);

        var trial = ReductiveVerbs.For(person, TestCatalogs.StoneItem, world.Configuration.ItemCatalog);

        Assert.NotNull(trial);
        Assert.IsType<KnapCommand>(trial.Value.Command);
    }

    [Fact]
    public void RawhideAnswersToTan()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Person(world);
        person.Inventory.Add(TestCatalogs.RawhideItem, 1);

        var trial = ReductiveVerbs.For(person, TestCatalogs.RawhideItem, world.Configuration.ItemCatalog);

        Assert.NotNull(trial);
        Assert.Equal(TanCommand.Skill, trial.Value.Skill);
        Assert.IsType<TanCommand>(trial.Value.Command);
    }

    // Already cured, so nothing further to try - a thing that answers to no verb at all is not
    // an error to word, simply not an offer.
    [Fact]
    public void HideAnswersToNoVerbAtAll()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Person(world);
        person.Inventory.Add(TestCatalogs.HideItem, 1);

        var trial = ReductiveVerbs.For(person, TestCatalogs.HideItem, world.Configuration.ItemCatalog);

        Assert.Null(trial);
    }
}
