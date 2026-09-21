using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class TwistCommandTests
{
    // Practised enough that the hands never fail (skill reaches full mastery at this point), so a
    // test about what twisting produces is not also a test of the dice; the roll itself is tested below.
    private static Person Twister(WorldState world, int grass = TestCatalogs.GrassPerCord)
    {
        var person = Novice(world, grass);
        Practise(person);

        return person;
    }

    private static Person Novice(WorldState world, int grass = TestCatalogs.GrassPerCord)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicTwisting);
        person.Inventory.Add(TestCatalogs.GrassItem, grass);

        return person;
    }

    private static void Practise(Person person, int times = 50)
    {
        for (var i = 0; i < times; i++)
        {
            person.Skills.Increase(TwistCommand.Skill, 1f);
        }
    }

    // Walks the clock to a tick where the real roll falls the wanted way, rather than stubbing it.
    private static void AdvanceToATickThatWill(WorldState world, Person person, bool succeed)
    {
        while (WorkAttempt.Succeeds(person, TwistCommand.Skill, TwistCommand.Verb, world.Clock.CurrentTick) != succeed)
        {
            world.Clock.Advance();
        }
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
    // would be worthless (quality multiplies into durability elsewhere).
    [Fact]
    public void ABeginnersWorkIsPoorButNotWorthless()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Novice(world);
        AdvanceToATickThatWill(world, person, succeed: true);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        var cord = Assert.IsType<Assembly.Part>(Assert.Single(person.Inventory.Assemblies));
        Assert.True(cord.Quality > 0f);
        Assert.True(cord.Quality < 0.5f);
    }

    [Fact]
    public void APractisedHandTurnsOutBetterWorkThanABeginner()
    {
        var world = TestCatalogs.CreateWorld();
        var beginner = Novice(world);
        var practised = Twister(world);
        AdvanceToATickThatWill(world, beginner, succeed: true);

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
        Practise(person, times: 5000);

        Assert.Equal(1f, WorkAttempt.QualityFor(person, TwistCommand.Skill), 5);
    }

    // A spoiled attempt still costs the material and still teaches - both happen regardless of the roll.
    [Fact]
    public void ASpoiledAttemptCostsTheMaterialAndLeavesNothingBehind()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Novice(world);
        AdvanceToATickThatWill(world, person, succeed: false);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.GrassItem));
        Assert.Empty(person.Inventory.Assemblies);
    }

    [Fact]
    public void ASpoiledAttemptIsStillPractice()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Novice(world);
        AdvanceToATickThatWill(world, person, succeed: false);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        Assert.True(person.Skills.Get(TwistCommand.Skill) > 0f);
    }

    // A sound idea is never refused for want of skill: a beginner gets there, just not every time
    // (see docs/materials-and-crafting-architecture.md section 7).
    [Fact]
    public void ABeginnerSucceedsSometimesAndAPractisedHandAlways()
    {
        var world = TestCatalogs.CreateWorld();
        var beginner = Novice(world);
        var practised = Twister(world);

        var beginnerWins = 0;
        for (var tick = 0; tick < 200; tick++)
        {
            if (WorkAttempt.Succeeds(beginner, TwistCommand.Skill, TwistCommand.Verb, tick))
            {
                beginnerWins++;
            }

            Assert.True(WorkAttempt.Succeeds(practised, TwistCommand.Skill, TwistCommand.Verb, tick));
        }

        Assert.InRange(beginnerWins, 1, 199);
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
    // all - stiff, unfibrous stuff will not hold a twist however the content is authored.
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
