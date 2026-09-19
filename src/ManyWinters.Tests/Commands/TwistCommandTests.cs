using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class TwistCommandTests
{
    private static Person Twister(WorldState world, int grass = TestCatalogs.GrassPerCord)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicTwisting);
        person.Inventory.Add(TestCatalogs.GrassItem, grass);

        return person;
    }

    [Fact]
    public void TwistingTurnsGrassIntoAWorkedPieceOfCord()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.GrassItem));
        var cord = Assert.IsType<Assembly.Part>(Assert.Single(person.Inventory.Assemblies));
        Assert.Equal(TestCatalogs.Cord, cord.Form);
    }

    // The substance does not change, only the shape it is in: a grass cord is still grass.
    [Fact]
    public void TheWorkedPieceKeepsTheMaterialItWasMadeFrom()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world);
        var grassMaterial = world.Configuration.ItemCatalog.Get(TestCatalogs.GrassItem).Material;

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        var cord = Assert.IsType<Assembly.Part>(Assert.Single(person.Inventory.Assemblies));
        Assert.Equal(grassMaterial, cord.Material);
    }

    // Twisting neither creates nor destroys weight: what went in is what comes out, so a person
    // cannot lighten their load by working it.
    [Fact]
    public void TheWorkedPieceWeighsWhatWentIntoIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world);
        var before = person.Inventory.TotalWeight(world.Configuration.ItemCatalog);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        Assert.Equal(before, person.Inventory.TotalWeight(world.Configuration.ItemCatalog), 4);
    }

    [Fact]
    public void TwistingLeavesLeftoverGrassBehind()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world, TestCatalogs.GrassPerCord + 2);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        Assert.Equal(2, person.Inventory.Get(TestCatalogs.GrassItem));
        Assert.Single(person.Inventory.Assemblies);
    }

    [Fact]
    public void TwistingIsPractice()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        Assert.True(person.Skills.Get(TwistCommand.Skill) > 0f);
    }

    // A beginner's cord is poor but real - worth something, or the first one anybody ever makes
    // would be worthless (Assembly.Durability multiplies by quality).
    [Fact]
    public void ABeginnersWorkIsPoorButNotWorthless()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        var cord = Assert.IsType<Assembly.Part>(Assert.Single(person.Inventory.Assemblies));
        Assert.True(cord.Quality > 0f);
        Assert.True(cord.Quality < 0.5f);
    }

    [Fact]
    public void APractisedHandTurnsOutBetterWorkThanABeginner()
    {
        var world = TestCatalogs.CreateWorld();
        var beginner = Twister(world);
        var practised = Twister(world);
        for (var i = 0; i < 40; i++)
        {
            practised.Skills.Increase(TwistCommand.Skill, 1f);
        }

        world.Execute(new TwistCommand(beginner, TestCatalogs.GrassItem));
        world.Execute(new TwistCommand(practised, TestCatalogs.GrassItem));

        var beginnersCord = Assert.IsType<Assembly.Part>(Assert.Single(beginner.Inventory.Assemblies));
        var practisedCord = Assert.IsType<Assembly.Part>(Assert.Single(practised.Inventory.Assemblies));
        Assert.True(practisedCord.Quality > beginnersCord.Quality);
    }

    [Fact]
    public void QualityNeverPassesOneHoweverLongSomebodyPractises()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world);
        for (var i = 0; i < 5000; i++)
        {
            person.Skills.Increase(TwistCommand.Skill, 1f);
        }

        Assert.Equal(1f, TwistCommand.QualityFor(person), 5);
    }

    [Fact]
    public void TwistingWithoutEnoughGrassDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world, TestCatalogs.GrassPerCord - 1);
        var command = new TwistCommand(person, TestCatalogs.GrassItem);

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(person.Inventory.Assemblies);
        Assert.Equal(TestCatalogs.GrassPerCord - 1, person.Inventory.Get(TestCatalogs.GrassItem));
    }

    // Nothing says what twisting wood would leave behind, so there is nothing to make.
    [Fact]
    public void TwistingSomethingWithNoTransitionOfItsOwnDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world);
        person.Inventory.Add(TestCatalogs.WoodItem, 10);
        var command = new TwistCommand(person, TestCatalogs.WoodItem);

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(person.Inventory.Assemblies);
    }

    // The transition says what it would become; the material says whether it can become it at
    // all. Stiff, unfibrous stuff will not hold a twist however the content is authored.
    [Fact]
    public void SomethingTheMaterialItselfWillNotTakeIsBlockedAsNotTwistable()
    {
        var brittle = new ItemKindId("brittle_straw");
        var brittleMaterial = new MaterialId("brittle_straw");
        var configuration = TestCatalogs.CreateConfiguration();
        var materials = new MaterialCatalog([new MaterialDefinition(brittleMaterial, "Brittle Straw", Density: 0.2f)]);
        var world = new WorldState(configuration with
        {
            MaterialCatalog = materials,
            ItemCatalog = new ItemCatalog(
                [
                    new ItemDefinition(brittle, "Brittle Straw", brittleMaterial, new FormId("fibre"), Volume: 1f,
                        Transitions: [new FormTransition(TwistCommand.Verb, TestCatalogs.Cord, 1)]),
                ],
                materials,
                configuration.FormCatalog),
        });
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicTwisting);
        person.Inventory.Add(brittle, 5);

        var command = new TwistCommand(person, brittle);

        Assert.Equal(ActionBlocker.NotTwistable, command.Blocker(world));
        world.Execute(command);
        Assert.Empty(person.Inventory.Assemblies);
    }

    [Fact]
    public void TwistingWithoutHavingLearnedItDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.Inventory.Add(TestCatalogs.GrassItem, TestCatalogs.GrassPerCord);
        var command = new TwistCommand(person, TestCatalogs.GrassItem);

        Assert.Equal(ActionBlocker.NotLearned, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(person.Inventory.Assemblies);
    }

    [Fact]
    public void TwistingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world);
        person.IsAlive = false;
        var command = new TwistCommand(person, TestCatalogs.GrassItem);

        Assert.Equal(ActionBlocker.ActorIsDead, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(person.Inventory.Assemblies);
    }

    [Fact]
    public void NothingBlocksTwistingWithTheGrassInHandAndTheKnowledgeToDoIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world);

        Assert.Equal(ActionBlocker.None, new TwistCommand(person, TestCatalogs.GrassItem).Blocker(world));
    }

    [Fact]
    public void TwistingTwiceLeavesTwoSeparateCordsRatherThanAStackOfTwo()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Twister(world, TestCatalogs.GrassPerCord * 2);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));
        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        Assert.Equal(2, person.Inventory.Assemblies.Count);
    }
}
