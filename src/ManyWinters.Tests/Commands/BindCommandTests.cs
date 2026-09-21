using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class BindCommandTests
{
    private static readonly CarriedThing Wood = new CarriedThing.Stock(TestCatalogs.WoodItem);
    private static readonly CarriedThing Stone = new CarriedThing.Stock(TestCatalogs.StoneItem);

    // Somebody who knows how to bind, carrying a stick, a stone and one cord to lash them with.
    // Practised enough that the hands never fail (chance of success reaches 1 at mastery), so a
    // test about what binding produces is not also a test of the dice. The rolling has its own
    // tests below.
    private static Person Binder(WorldState world, float cordQuality = 0.5f)
    {
        var person = Novice(world, cordQuality);
        Practise(person);

        return person;
    }

    private static Person Novice(WorldState world, float cordQuality = 0.5f)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicBinding);
        person.Inventory.Add(TestCatalogs.WoodItem, 1);
        person.Inventory.Add(TestCatalogs.StoneItem, 1);
        person.Inventory.AddAssembly(Cord(cordQuality));

        return person;
    }

    private static void Practise(Person person, int times = 50)
    {
        for (var i = 0; i < times; i++)
        {
            person.Skills.Increase(BindCommand.Skill, 1f);
        }
    }

    private static void AdvanceToATickThatWill(WorldState world, Person person, bool succeed)
    {
        while (WorkAttempt.Succeeds(person, BindCommand.Skill, BindCommand.Verb, world.Clock.CurrentTick) != succeed)
        {
            world.Clock.Advance();
        }
    }

    private static Assembly.Part Cord(float quality = 0.5f) =>
        new(new MaterialId("plant_fibre"), TestCatalogs.Cord, quality, Volume: 15f);

    [Fact]
    public void BindingTwoThingsLeavesOneObjectMadeOfBoth()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Binder(world);

        world.Execute(new BindCommand(person, Wood, Stone));

        var bound = Assert.IsType<Assembly.Joined>(Assert.Single(person.Inventory.Assemblies));
        var left = Assert.IsType<Assembly.Part>(bound.Left);
        var right = Assert.IsType<Assembly.Part>(bound.Right);
        Assert.Equal(new MaterialId("wood"), left.Material);
        Assert.Equal(new MaterialId("stone"), right.Material);
    }

    [Fact]
    public void BindingTakesBothThingsAndTheCordOutOfThePack()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Binder(world);

        world.Execute(new BindCommand(person, Wood, Stone));

        Assert.Equal(0, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.StoneItem));
        Assert.DoesNotContain(person.Inventory.Assemblies, held => held is Assembly.Part);
    }

    // Nothing is created or destroyed by tying a knot, the cordage included.
    [Fact]
    public void TheBoundThingWeighsEverythingThatWentIntoIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Binder(world);
        var before = person.Inventory.TotalWeight(world.Configuration.ItemCatalog);

        world.Execute(new BindCommand(person, Wood, Stone));

        Assert.Equal(before, person.Inventory.TotalWeight(world.Configuration.ItemCatalog), 4);
    }

    [Fact]
    public void BindingIsPractice()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Binder(world);

        world.Execute(new BindCommand(person, Wood, Stone));

        Assert.True(person.Skills.Get(BindCommand.Skill) > 0f);
    }

    // A joint is never better than the cord it is made of, so a better cord makes a better
    // object without the binder being told which to use.
    [Fact]
    public void ABetterCordMakesAStrongerJoint()
    {
        var world = TestCatalogs.CreateWorld();
        var poorly = Binder(world, cordQuality: 0.2f);
        var well = Binder(world, cordQuality: 0.9f);

        world.Execute(new BindCommand(poorly, Wood, Stone));
        world.Execute(new BindCommand(well, Wood, Stone));

        var weak = Assert.IsType<Assembly.Joined>(Assert.Single(poorly.Inventory.Assemblies));
        var strong = Assert.IsType<Assembly.Joined>(Assert.Single(well.Inventory.Assemblies));
        Assert.True(strong.JointStrength > weak.JointStrength);
    }

    [Fact]
    public void TheSoundestCordInThePackIsTheOneUsed()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Binder(world, cordQuality: 0.2f);
        person.Inventory.AddAssembly(Cord(0.9f));

        world.Execute(new BindCommand(person, Wood, Stone));

        var leftover = Assert.IsType<Assembly.Part>(Assert.Single(person.Inventory.Assemblies, held => held is Assembly.Part));
        Assert.Equal(0.2f, leftover.Quality, 5);
    }

    [Fact]
    public void APractisedHandTiesAStrongerJointThanABeginner()
    {
        var world = TestCatalogs.CreateWorld();
        var beginner = Novice(world);
        var practised = Binder(world);
        AdvanceToATickThatWill(world, beginner, succeed: true);

        world.Execute(new BindCommand(beginner, Wood, Stone));
        world.Execute(new BindCommand(practised, Wood, Stone));

        var beginnersWork = Assert.IsType<Assembly.Joined>(Assert.Single(beginner.Inventory.Assemblies));
        var practisedWork = Assert.IsType<Assembly.Joined>(Assert.Single(practised.Inventory.Assemblies));
        Assert.True(practisedWork.JointStrength > beginnersWork.JointStrength);
    }

    // Depth: a bound thing is a thing, so it can be bound again (see section 6, no part cap).
    [Fact]
    public void ABoundThingCanItselfBeBoundToSomethingElse()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Binder(world);
        person.Inventory.Add(TestCatalogs.WoodItem, 1);
        person.Inventory.AddAssembly(Cord());

        world.Execute(new BindCommand(person, Wood, Stone));
        var firstBound = Assert.Single(person.Inventory.Assemblies, held => held is Assembly.Joined);

        world.Execute(new BindCommand(person, new CarriedThing.Worked(firstBound), Wood));

        var outer = Assert.IsType<Assembly.Joined>(Assert.Single(person.Inventory.Assemblies));
        Assert.IsType<Assembly.Joined>(outer.Left);
    }

    [Fact]
    public void BindingWithNoCordAtAllDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicBinding);
        person.Inventory.Add(TestCatalogs.WoodItem, 1);
        person.Inventory.Add(TestCatalogs.StoneItem, 1);
        var command = new BindCommand(person, Wood, Stone);

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.Empty(person.Inventory.Assemblies);
    }

    // A shape that cannot lash is not cordage, however much of it somebody carries.
    [Fact]
    public void SomethingThatIsNotCordageCannotDoTheBinding()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicBinding);
        person.Inventory.Add(TestCatalogs.WoodItem, 1);
        person.Inventory.Add(TestCatalogs.StoneItem, 1);
        person.Inventory.AddAssembly(new Assembly.Part(new MaterialId("wood"), TestCatalogs.Wedge, Quality: 1f, Volume: 1f));

        Assert.Equal(ActionBlocker.MissingMaterials, new BindCommand(person, Wood, Stone).Blocker(world));
    }

    // The cord the player chose to bind into the object is not also the cord doing the tying.
    [Fact]
    public void ACordPickedOutToBeBoundIsNotAlsoUsedAsTheBinding()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicBinding);
        person.Inventory.Add(TestCatalogs.WoodItem, 1);
        var onlyCord = Cord();
        person.Inventory.AddAssembly(onlyCord);

        var command = new BindCommand(person, new CarriedThing.Worked(onlyCord), Wood);

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
    }

    [Fact]
    public void BindingSomethingNotInThePackDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Binder(world);
        var command = new BindCommand(person, Wood, new CarriedThing.Stock(TestCatalogs.AppleItem));

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.DoesNotContain(person.Inventory.Assemblies, held => held is Assembly.Joined);
    }

    // Two of the same thing needs two of it, not one counted twice.
    [Fact]
    public void BindingTwoOfTheSameStockNeedsTwoOfIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Binder(world);

        Assert.Equal(ActionBlocker.MissingMaterials, new BindCommand(person, Wood, Wood).Blocker(world));

        person.Inventory.Add(TestCatalogs.WoodItem, 1);

        Assert.Equal(ActionBlocker.None, new BindCommand(person, Wood, Wood).Blocker(world));
    }

    [Fact]
    public void BindingWithoutHavingLearnedItDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Binder(world);
        person.KnownTechniques.Remove(TestCatalogs.BasicBinding);
        var command = new BindCommand(person, Wood, Stone);

        Assert.Equal(ActionBlocker.NotLearned, command.Blocker(world));
        world.Execute(command);

        Assert.DoesNotContain(person.Inventory.Assemblies, held => held is Assembly.Joined);
    }

    [Fact]
    public void BindingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Binder(world);
        person.IsAlive = false;
        var command = new BindCommand(person, Wood, Stone);

        Assert.Equal(ActionBlocker.ActorIsDead, command.Blocker(world));
        world.Execute(command);

        Assert.DoesNotContain(person.Inventory.Assemblies, held => held is Assembly.Joined);
    }

    [Fact]
    public void NothingBlocksBindingWithBothThingsAndACordInHand()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Binder(world);

        Assert.Equal(ActionBlocker.None, new BindCommand(person, Wood, Stone).Blocker(world));
    }

    // A lashing that slipped costs the cordage and the time, but not what it was tied around:
    // two things that came apart are still two things.
    [Fact]
    public void ASlippedLashingCostsTheCordButNotWhatItHeld()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Novice(world);
        AdvanceToATickThatWill(world, person, succeed: false);

        world.Execute(new BindCommand(person, Wood, Stone));

        Assert.Empty(person.Inventory.Assemblies);
        Assert.Equal(1, person.Inventory.Get(TestCatalogs.WoodItem));
        Assert.Equal(1, person.Inventory.Get(TestCatalogs.StoneItem));
    }

    [Fact]
    public void ASlippedLashingIsStillPractice()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Novice(world);
        AdvanceToATickThatWill(world, person, succeed: false);

        world.Execute(new BindCommand(person, Wood, Stone));

        Assert.True(person.Skills.Get(BindCommand.Skill) > 0f);
    }
}
