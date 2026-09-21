using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

// The second reductive verb. What it shares with the first - the dice, the cost of a spoiled
// attempt, the practice earned either way - is one piece of work with its own tests; what is
// tested here is what makes knapping knapping: it asks a substance to be hard and brittle, and
// what comes out is the first edge in the game.
public class KnapCommandTests
{
    // Practised enough that the hands never fail (chance of success reaches 1 at mastery), so a
    // test about what knapping produces is not also a test of the dice.
    private static Person Knapper(WorldState world, int stone = TestCatalogs.StonePerWedge)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicKnapping);
        person.Inventory.Add(TestCatalogs.StoneItem, stone);
        for (var i = 0; i < 50; i++)
        {
            person.Skills.Increase(KnapCommand.Skill, 1f);
        }

        return person;
    }

    [Fact]
    public void KnappingTurnsAStoneLumpIntoAWorkedWedge()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Knapper(world);

        world.Execute(new KnapCommand(person, TestCatalogs.StoneItem));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.StoneItem));
        var wedge = Assert.IsType<Assembly.Part>(Assert.Single(person.Inventory.Assemblies));
        Assert.Equal(TestCatalogs.Wedge, wedge.Form);
    }

    // The point of the whole arc: a wedge is a thing that can cut, and a lump of the very same
    // stone is not.
    [Fact]
    public void WhatComesOutOfItChopsWhereTheLumpItWasMadeFromDidNot()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Knapper(world);
        var items = world.Configuration.ItemCatalog;

        Assert.Equal(0f, person.Inventory.BestChoppingScore(items));

        world.Execute(new KnapCommand(person, TestCatalogs.StoneItem));

        Assert.True(person.Inventory.BestChoppingScore(items) > 0f);
    }

    // Hard and brittle is what knapping asks for, and nothing else is named: a substance that
    // gives rather than fractures is refused however the content is authored.
    [Fact]
    public void SomethingTooToughToFractureIsBlockedAsNotKnappable()
    {
        var soapstone = new ItemKindId("soapstone");
        var soft = new MaterialId("soapstone");
        var configuration = TestCatalogs.CreateConfiguration();
        var materials = new MaterialCatalog([new MaterialDefinition(soft, "Soapstone", Density: 2f, Hardness: 0.2f, Toughness: 0.8f)]);
        var world = new WorldState(configuration with
        {
            MaterialCatalog = materials,
            ItemCatalog = new ItemCatalog(
                [
                    new ItemDefinition(soapstone, "Soapstone", soft, new FormId("lump"), Volume: 1f,
                        Transitions: [new FormTransition(KnapCommand.Verb, TestCatalogs.Wedge, 1)]),
                ],
                materials,
                configuration.FormCatalog),
        });
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicKnapping);
        person.Inventory.Add(soapstone, 5);

        var command = new KnapCommand(person, soapstone);

        Assert.Equal(ActionBlocker.NotKnappable, command.Blocker(world));
        world.Execute(command);
        Assert.Empty(person.Inventory.Assemblies);
    }

    // Nothing says what knapping grass would leave behind, so there is nothing to make.
    [Fact]
    public void KnappingSomethingWithNoTransitionOfItsOwnDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Knapper(world);
        person.Inventory.Add(TestCatalogs.GrassItem, 10);
        var command = new KnapCommand(person, TestCatalogs.GrassItem);

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(person.Inventory.Assemblies);
    }

    [Fact]
    public void KnappingWithoutHavingLearnedItDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.Inventory.Add(TestCatalogs.StoneItem, TestCatalogs.StonePerWedge);
        var command = new KnapCommand(person, TestCatalogs.StoneItem);

        Assert.Equal(ActionBlocker.NotLearned, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(person.Inventory.Assemblies);
    }

    [Fact]
    public void KnappingWithNoStoneInHandDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Knapper(world, stone: 0);
        var command = new KnapCommand(person, TestCatalogs.StoneItem);

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(person.Inventory.Assemblies);
    }

    // Striking a stone is how somebody comes to know stone, whatever the strike leaves behind.
    [Fact]
    public void KnappingTeachesWhatStoneIsLike()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Knapper(world);
        var stone = world.Configuration.ItemCatalog.Get(TestCatalogs.StoneItem).Material;

        Assert.False(person.Beliefs.HoldsAnythingAbout(stone));

        world.Execute(new KnapCommand(person, TestCatalogs.StoneItem));

        Assert.True(person.Beliefs.IsFirm(stone, MaterialProperty.Hardness));
    }
}
