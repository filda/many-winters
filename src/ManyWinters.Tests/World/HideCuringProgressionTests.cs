using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// The full hunt-butcher-tan-craft progression: hunt, then butcher (efficiently, for the hide),
// then tan, then warm clothing. Also the reason curing is worth having at all: what it is
// measured against (rawhide_clothing, made straight off a carcass with no further knowledge)
// rots a season in, where the cured garment does not.
public class HideCuringProgressionTests
{
    private static Animal DeadDeer(WorldState world, Position position)
    {
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, position);
        deer.IsAlive = false;
        deer.Inventory.Add(TestCatalogs.RawhideItem, 1);

        return deer;
    }

    private static void MakesEveryTanningAttemptSucceed(Person person)
    {
        for (var i = 0; i < 50; i++)
        {
            person.Skills.Increase(TanCommand.Skill, 1f);
        }
    }

    [Fact]
    public void HuntButcherTanAndCraftEndsInWarmClothingThatNeverSpoils()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var person = world.SpawnPerson("Ava", position, initialAgeTicks: TestCatalogs.AdultAgeTicks);
        world.Execute(new GrantTechniqueCommand(person, TestCatalogs.BasicButchering));
        world.Execute(new GrantTechniqueCommand(person, TestCatalogs.EfficientButchering));
        world.Execute(new GrantTechniqueCommand(person, TestCatalogs.BasicTanning));
        MakesEveryTanningAttemptSucceed(person);

        // Two hunts, since warm_clothing wants two hide and a deer gives one each.
        var first = DeadDeer(world, position);
        var second = DeadDeer(world, position);
        world.Execute(new ButcherCommand(person, first));
        world.Execute(new ButcherCommand(person, second));

        Assert.Equal(2, person.Inventory.Get(TestCatalogs.RawhideItem));

        world.Execute(new TanCommand(person, TestCatalogs.RawhideItem));
        world.Execute(new TanCommand(person, TestCatalogs.RawhideItem));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.RawhideItem));
        Assert.Equal(2, person.Inventory.Get(TestCatalogs.HideItem));

        world.Execute(new MakeCommand(person, TestCatalogs.WarmClothing));

        Assert.Equal(1, person.Inventory.Get(TestCatalogs.WarmClothing));

        // Long past even the season rawhide_clothing would have shrivelled in (below) - the
        // cured garment is not on any clock at all.
        world.Advance(400);

        Assert.Equal(1, person.Inventory.Get(TestCatalogs.WarmClothing));
    }

    [Fact]
    public void RawhideClothingRotsWithinASeasonButTheTannedGarmentOutlastsIt()
    {
        var world = TestCatalogs.CreateWorld();
        var catalog = world.Configuration.ItemCatalog;
        var position = new Position(0, 0);
        var person = world.SpawnPerson("Ava", position, initialAgeTicks: TestCatalogs.AdultAgeTicks);
        // Rawhide clothing needs no further knowledge - straight off a carcass with nothing
        // learned. Warm clothing needs cured hide, which this test hands over already tanned
        // rather than re-proving TanCommand.
        person.Inventory.Add(TestCatalogs.RawhideItem, TestCatalogs.WarmClothingInputAmount, tick: 0, catalog);
        person.Inventory.Add(TestCatalogs.HideItem, TestCatalogs.WarmClothingInputAmount, tick: 0, catalog);

        world.Execute(new MakeCommand(person, TestCatalogs.RawhideClothing));
        world.Execute(new MakeCommand(person, TestCatalogs.WarmClothing));

        Assert.Equal(1, person.Inventory.Get(TestCatalogs.RawhideClothing));
        Assert.Equal(1, person.Inventory.Get(TestCatalogs.WarmClothing));

        world.Advance(TestCatalogs.RawhideShelfLifeTicks - 1);
        Assert.Equal(1, person.Inventory.Get(TestCatalogs.RawhideClothing));

        world.Advance(1);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.RawhideClothing));

        world.Advance(400 - TestCatalogs.RawhideShelfLifeTicks);
        Assert.Equal(1, person.Inventory.Get(TestCatalogs.WarmClothing));
    }
}
