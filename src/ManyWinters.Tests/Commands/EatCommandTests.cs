using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class EatCommandTests
{
    [Fact]
    public void EatingRelievesHungerAndConsumesOnlyAsMuchAsWasNeeded()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 15;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        world.Execute(new EatCommand(person, TestCatalogs.AppleItem));

        Assert.Equal(0f, person.Needs.Hunger);
        Assert.Equal(5, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void EatingNeverReducesHungerBelowZero()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 5;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        world.Execute(new EatCommand(person, TestCatalogs.AppleItem));

        Assert.Equal(0f, person.Needs.Hunger);
        Assert.Equal(15, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void EatingOnlyConsumesWhatIsAvailableWhenThereIsNotEnoughToFullySatisfyHunger()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 50;
        person.Inventory.Add(TestCatalogs.AppleItem, 10);

        world.Execute(new EatCommand(person, TestCatalogs.AppleItem));

        Assert.Equal(40f, person.Needs.Hunger);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void EatingWithNoHungerDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 0;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        world.Execute(new EatCommand(person, TestCatalogs.AppleItem));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void EatingWithoutHavingLearnedHowToEatDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 50;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        world.Execute(new EatCommand(person, TestCatalogs.AppleItem));

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void KnowingEfficientEatingRestoresMoreHungerPerUnitEaten()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.KnownTechniques.Add(TestCatalogs.EfficientEating);
        person.Needs.Hunger = 100;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        world.Execute(new EatCommand(person, TestCatalogs.AppleItem));

        // All 20 units get eaten either way (not enough to fully satisfy 100 hunger even at
        // the efficient rate) - the bonus shows up in how much hunger that same 20 relieves.
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
        Assert.Equal(76f, person.Needs.Hunger);
    }

    [Fact]
    public void EatingWithNoneOfThatFoodInInventoryDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 50;

        world.Execute(new EatCommand(person, TestCatalogs.AppleItem));

        Assert.Equal(50f, person.Needs.Hunger);
        // Nor does miming a meal count as practice - going through the motions with an empty
        // pack must not train anyone toward the efficient technique.
        Assert.Equal(0f, person.Skills.Get(EatCommand.Skill));
        Assert.DoesNotContain(TestCatalogs.EfficientEating, person.KnownTechniques);
    }

    [Fact]
    public void EatingAnItemThatIsNotFoodDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        // Without this the meal is refused for not knowing how to eat, and the branch this
        // test is named after is never reached at all.
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 50;
        person.Inventory.Add(TestCatalogs.WoodItem, 20);

        world.Execute(new EatCommand(person, TestCatalogs.WoodItem));

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void EatingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        // Every other reason to refuse is removed - they know how, they are hungry, and the
        // food is in hand - so being dead is on its own what stops the meal.
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.IsAlive = false;
        person.Needs.Hunger = 50;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        world.Execute(new EatCommand(person, TestCatalogs.AppleItem));

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void MoreFillingFoodMeansFewerUnitsEatenToSatisfyTheSameHunger()
    {
        // Everything shipped restores exactly 1 per unit, which hides whether hunger is
        // divided by that rate or multiplied by it - both give the same answer at 1. Stew
        // restoring 4 tells them apart: ten hunger needs three units, not forty.
        var stew = new ItemKindId("stew");
        var materials = new MaterialCatalog([new MaterialDefinition(new MaterialId("stew"), "Stew", Density: 1f)]);
        var configuration = TestCatalogs.CreateConfiguration() with
        {
            MaterialCatalog = materials,
            ItemCatalog = new ItemCatalog(
                [new ItemDefinition(stew, "Stew", new MaterialId("stew"), new FormId("vessel"), Volume: 1f, HungerRestoredPerUnit: 4f)],
                materials),
        };
        var world = new WorldState(configuration);
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 10f;
        person.Inventory.Add(stew, 20);

        world.Execute(new EatCommand(person, stew));

        Assert.Equal(17, person.Inventory.Get(stew));
        Assert.Equal(0f, person.Needs.Hunger);
    }

    [Fact]
    public void EnoughPracticeDiscoversTheEfficientTechnique()
    {
        // Five meals is exactly the threshold - the meal that reaches it is the one that
        // teaches, not the one after.
        var world = TestCatalogs.CreateWorld();
        var person = EaterWithFood(world);

        for (var meal = 0; meal < 5; meal++)
        {
            person.Needs.Hunger = 1f;
            world.Execute(new EatCommand(person, TestCatalogs.AppleItem));
        }

        Assert.Equal(5f, person.Skills.Get(EatCommand.Skill));
        Assert.Contains(TestCatalogs.EfficientEating, person.KnownTechniques);
    }

    [Fact]
    public void OneMealShortOfTheThresholdTeachesNothingYet()
    {
        var world = TestCatalogs.CreateWorld();
        var person = EaterWithFood(world);

        for (var meal = 0; meal < 4; meal++)
        {
            person.Needs.Hunger = 1f;
            world.Execute(new EatCommand(person, TestCatalogs.AppleItem));
        }

        Assert.Equal(4f, person.Skills.Get(EatCommand.Skill));
        Assert.DoesNotContain(TestCatalogs.EfficientEating, person.KnownTechniques);
    }

    private static Person EaterWithFood(WorldState world)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        return person;
    }
}
