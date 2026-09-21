using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

// The whole arc in one place: nothing in the game is called an axe, and a band still ends up
// with one. A stone is knapped into an edge, grass is twisted into a cord, the cord lashes the
// edge to a stick - and the tree falls, because what fells a tree is a chopping score and not a
// noun (see docs/materials-and-crafting-architecture.md sections 1, 4 and 6).
//
// It is also the test that would catch the arc coming apart at any one of its joints, which the
// per-command tests each side of a seam cannot.
public class MakingAnAxeTests
{
    private static Person Toolmaker(WorldState world)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicKnapping);
        person.KnownTechniques.Add(TestCatalogs.BasicTwisting);
        person.KnownTechniques.Add(TestCatalogs.BasicBinding);
        person.KnownTechniques.Add(TestCatalogs.BasicWoodcutting);

        // Practised hands, so this is a test of the chain rather than of the dice.
        foreach (var skill in new[] { KnapCommand.Skill, TwistCommand.Skill, BindCommand.Skill })
        {
            for (var i = 0; i < 50; i++)
            {
                person.Skills.Increase(skill, 1f);
            }
        }

        person.Inventory.Add(TestCatalogs.StoneItem, TestCatalogs.StonePerWedge);
        person.Inventory.Add(TestCatalogs.GrassItem, TestCatalogs.GrassPerCord);
        person.Inventory.Add(TestCatalogs.WoodItem, 1);

        return person;
    }

    private static Assembly MakeAnAxe(WorldState world, Person person)
    {
        world.Execute(new KnapCommand(person, TestCatalogs.StoneItem));
        var head = Assert.Single(person.Inventory.Assemblies);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        // The cordage is never asked for: binding reaches for the soundest one in the pack.
        world.Execute(new BindCommand(person, new CarriedThing.Worked(head), new CarriedThing.Stock(TestCatalogs.WoodItem)));

        return Assert.Single(person.Inventory.Assemblies);
    }

    [Fact]
    public void AStoneAHandfulOfGrassAndAStickBecomeOneThing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Toolmaker(world);

        var axe = MakeAnAxe(world, person);

        var joined = Assert.IsType<Assembly.Joined>(axe);
        Assert.Equal(TestCatalogs.Wedge, Assert.IsType<Assembly.Part>(joined.Left).Form);
        Assert.Empty(person.Inventory.Counts);
    }

    // Nothing was created and nothing destroyed along the way: the stone, the grass and the
    // stick all still weigh what they did, now in one object.
    [Fact]
    public void ItWeighsWhatWentIntoIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Toolmaker(world);
        var items = world.Configuration.ItemCatalog;
        var before = person.Inventory.TotalWeight(items);

        MakeAnAxe(world, person);

        Assert.Equal(before, person.Inventory.TotalWeight(items), 4);
    }

    [Fact]
    public void ATreeFallsToItWhereItWouldNotFallToTheStoneItWasMadeFrom()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Toolmaker(world);
        var tree = world.SpawnResourceNode(TestCatalogs.ConiferTree, new Position(0, 0), 100);

        Assert.Equal(ActionBlocker.MissingTool, new FellCommand(person, tree).Blocker(world));

        MakeAnAxe(world, person);

        Assert.Equal(ActionBlocker.None, new FellCommand(person, tree).Blocker(world));
        world.Execute(new FellCommand(person, tree));
        Assert.False(tree.Growth!.IsAlive);
    }

    // Hafting is what the binding buys, and the band can feel the difference: the same edge
    // swung on a stick chops harder than the edge alone.
    [Fact]
    public void TheHaftedEdgeChopsBetterThanTheBareEdgeDid()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Toolmaker(world);
        var items = world.Configuration.ItemCatalog;

        world.Execute(new KnapCommand(person, TestCatalogs.StoneItem));
        var bare = person.Inventory.BestChoppingScore(items);

        var head = Assert.Single(person.Inventory.Assemblies);
        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));
        world.Execute(new BindCommand(person, new CarriedThing.Worked(head), new CarriedThing.Stock(TestCatalogs.WoodItem)));

        Assert.True(person.Inventory.BestChoppingScore(items) > bare);
    }
}
