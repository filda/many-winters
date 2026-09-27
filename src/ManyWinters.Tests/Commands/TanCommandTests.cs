using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

// The third reductive verb: curing turns rawhide into hide. Twist and Knap are already
// exhaustively tested against the machinery every reductive verb shares (ReductiveWork,
// WorkAttempt); this covers what is Tan's own - the material changing underneath the shape -
// and the blocker ordering the pattern demands.
public class TanCommandTests
{
    private static Person Tanner(WorldState world, int rawhide = 1)
    {
        var person = Novice(world, rawhide);
        Practise(person);

        return person;
    }

    private static Person Novice(WorldState world, int rawhide = 1)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicTanning);
        person.Inventory.Add(TestCatalogs.RawhideItem, rawhide);

        return person;
    }

    private static void Practise(Person person, int times = 50)
    {
        for (var i = 0; i < times; i++)
        {
            person.Skills.Increase(TanCommand.Skill, 1f);
        }
    }

    private static void AdvanceToATickThatWill(WorldState world, Person person, bool succeed)
    {
        while (WorkAttempt.Succeeds(person, TanCommand.Skill, TanCommand.Verb, world.Clock.CurrentTick) != succeed)
        {
            world.Clock.Advance();
        }
    }

    // Unlike Twist and Knap, curing exchanges one already-known stock substance for another - it
    // does not fashion a new individual object - so what comes out is a countable "hide", not a
    // worked Assembly.
    [Fact]
    public void TanningTurnsRawhideIntoHide()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Tanner(world);

        world.Execute(new TanCommand(person, TestCatalogs.RawhideItem));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.RawhideItem));
        Assert.Equal(1, person.Inventory.Get(TestCatalogs.HideItem));
        Assert.Empty(person.Inventory.Assemblies);
    }

    // The whole point of curing: what comes out no longer carries the input's own perishing
    // clock, because it is no longer the same substance.
    [Fact]
    public void TheCuredHideDoesNotSpoilEvenThoughTheRawhideItCameFromWould()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Tanner(world);

        world.Execute(new TanCommand(person, TestCatalogs.RawhideItem));

        world.Advance(TestCatalogs.RawhideShelfLifeTicks * 10);

        Assert.Equal(1, person.Inventory.Get(TestCatalogs.HideItem));
    }

    [Fact]
    public void TanningWithoutEnoughRawhideDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Tanner(world, rawhide: 0);
        var command = new TanCommand(person, TestCatalogs.RawhideItem);

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.HideItem));
    }

    // The transition says what curing would turn a thing into; the material says whether it can
    // be cured at all - a substance with no shelf life will not cure however the content is
    // authored, the same rule Twist and Knap hold to for their own verbs.
    [Fact]
    public void SomethingWithNoShelfLifeIsBlockedAsNotCurable()
    {
        var alreadyTanned = new ItemKindId("already_tanned");
        var alreadyTannedMaterial = new MaterialId("already_tanned");
        var configuration = TestCatalogs.CreateConfiguration();
        var materials = new MaterialCatalog([new MaterialDefinition(alreadyTannedMaterial, "Already Tanned", Density: 0.7f)]);
        var world = new WorldState(configuration with
        {
            MaterialCatalog = materials,
            ItemCatalog = new ItemCatalog(
                [
                    new ItemDefinition(alreadyTanned, "Already Tanned", alreadyTannedMaterial, new FormId("whole"), Volume: 1f,
                        Transitions: [new FormTransition(TanCommand.Verb, new FormId("whole"), 1)]),
                ],
                materials,
                configuration.FormCatalog),
        });
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicTanning);
        person.Inventory.Add(alreadyTanned, 1);

        var command = new TanCommand(person, alreadyTanned);

        Assert.Equal(ActionBlocker.NotCurable, command.Blocker(world));
        world.Execute(command);
        Assert.Empty(person.Inventory.Assemblies);
    }

    [Fact]
    public void TanningWithoutHavingLearnedItDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.Inventory.Add(TestCatalogs.RawhideItem, 1);
        var command = new TanCommand(person, TestCatalogs.RawhideItem);

        Assert.Equal(ActionBlocker.NotLearned, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(person.Inventory.Assemblies);
    }

    // Knowledge is asked last, like every other reductive verb: a person missing the material
    // hears about that first even if they have never learned to tan at all.
    [Fact]
    public void MissingMaterialsOutranksNotLearned()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        var command = new TanCommand(person, TestCatalogs.RawhideItem);

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
    }

    [Fact]
    public void TanningByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Tanner(world);
        person.IsAlive = false;
        var command = new TanCommand(person, TestCatalogs.RawhideItem);

        Assert.Equal(ActionBlocker.ActorIsDead, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(person.Inventory.Assemblies);
    }

    [Fact]
    public void NothingBlocksTanningWithTheRawhideInHandAndTheKnowledgeToDoIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Tanner(world);

        Assert.Equal(ActionBlocker.None, new TanCommand(person, TestCatalogs.RawhideItem).Blocker(world));
    }

    [Fact]
    public void TanningIsPractice()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Tanner(world);

        world.Execute(new TanCommand(person, TestCatalogs.RawhideItem));

        Assert.True(person.Skills.Get(TanCommand.Skill) > 0f);
    }

    // A spoiled attempt still costs the material and still teaches - both happen regardless of
    // the roll, the same as every other reductive verb.
    [Fact]
    public void ASpoiledAttemptCostsTheMaterialAndLeavesNothingBehind()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Novice(world);
        AdvanceToATickThatWill(world, person, succeed: false);

        world.Execute(new TanCommand(person, TestCatalogs.RawhideItem));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.RawhideItem));
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.HideItem));
        Assert.Empty(person.Inventory.Assemblies);
    }
}
