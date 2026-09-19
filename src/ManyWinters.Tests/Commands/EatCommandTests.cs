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
        var command = new EatCommand(person, TestCatalogs.AppleItem);

        Assert.Equal(ActionBlocker.None, command.Blocker(world));
        world.Execute(command);

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
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 0;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);
        var command = new EatCommand(person, TestCatalogs.AppleItem);

        Assert.Equal(ActionBlocker.NotHungry, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void EatingWithoutHavingLearnedHowToEatDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 50;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);
        var command = new EatCommand(person, TestCatalogs.AppleItem);

        Assert.Equal(ActionBlocker.NotLearned, command.Blocker(world));
        world.Execute(command);

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

        // All 20 units get eaten either way; the bonus shows in how much hunger they relieve.
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
        var command = new EatCommand(person, TestCatalogs.AppleItem);

        Assert.Equal(ActionBlocker.MissingMaterials, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(50f, person.Needs.Hunger);
        // Miming a meal with an empty pack must not count as practice toward the technique.
        Assert.Equal(0f, person.Skills.Get(EatCommand.Skill));
        Assert.DoesNotContain(TestCatalogs.EfficientEating, person.KnownTechniques);
    }

    [Fact]
    public void EatingAnItemThatIsNotFoodDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        // Without this the meal is refused for not knowing how to eat and the branch under test
        // is never reached.
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 50;
        person.Inventory.Add(TestCatalogs.WoodItem, 20);
        var command = new EatCommand(person, TestCatalogs.WoodItem);

        Assert.Equal(ActionBlocker.NotEdible, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void EatingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        // Every other reason to refuse is removed, so being dead is on its own what stops the meal.
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.IsAlive = false;
        person.Needs.Hunger = 50;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);
        var command = new EatCommand(person, TestCatalogs.AppleItem);

        Assert.Equal(ActionBlocker.ActorIsDead, command.Blocker(world));
        world.Execute(command);

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void MoreFillingFoodMeansFewerUnitsEatenToSatisfyTheSameHunger()
    {
        // Everything shipped restores 1 per unit, which cannot tell dividing hunger by the rate
        // from multiplying by it. Stew restoring 4 does: ten hunger needs three units, not forty.
        var stew = new ItemKindId("stew");
        var materials = new MaterialCatalog([new MaterialDefinition(new MaterialId("stew"), "Stew", Density: 1f)]);
        var configuration = TestCatalogs.CreateConfiguration() with
        {
            MaterialCatalog = materials,
            ItemCatalog = new ItemCatalog(
                [new ItemDefinition(stew, "Stew", new MaterialId("stew"), new FormId("vessel"), Volume: 1f, HungerRestoredPerUnit: 4f)],
                materials,
                new FormCatalog([])),
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
        // Five meals is exactly the threshold: the meal that reaches it is the one that teaches.
        var world = TestCatalogs.CreateWorld();
        var person = EaterWithFood(world);

        for (var meal = 0; meal < 5; meal++)
        {
            person.Needs.Hunger = 1f;
            world.Execute(new EatCommand(person, TestCatalogs.AppleItem));
        }

        // Practice has diminishing returns (Skills.Increase): five meals leave the skill just over
        // 2.5, where the threshold sits.
        Assert.Equal(2.553f, person.Skills.Get(EatCommand.Skill), 3);
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

        // Four meals fall short of the fifth meal's 2.553, and so of the threshold.
        Assert.Equal(2.245f, person.Skills.Get(EatCommand.Skill), 3);
        Assert.DoesNotContain(TestCatalogs.EfficientEating, person.KnownTechniques);
    }

    private static Person EaterWithFood(WorldState world)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        return person;
    }

    [Fact]
    public void NothingBlocksAHungryPersonWithFoodTheyKnowHowToEat()
    {
        var world = TestCatalogs.CreateWorld();
        var person = EaterWithFood(world);
        person.Needs.Hunger = 50f;

        Assert.Equal(ActionBlocker.None, new EatCommand(person, TestCatalogs.AppleItem).Blocker(world));
    }

    [Fact]
    public void ADeadPersonIsBlockedFromEating()
    {
        var world = TestCatalogs.CreateWorld();
        var person = EaterWithFood(world);
        person.Needs.Hunger = 50f;
        person.IsAlive = false;

        Assert.Equal(ActionBlocker.ActorIsDead, new EatCommand(person, TestCatalogs.AppleItem).Blocker(world));
    }

    [Fact]
    public void NeverHavingBeenTaughtToEatBlocksTheMealAsNotLearned()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.AppleItem, 20);
        person.Needs.Hunger = 50f;

        Assert.Equal(ActionBlocker.NotLearned, new EatCommand(person, TestCatalogs.AppleItem).Blocker(world));
    }

    [Fact]
    public void AnItemThatIsNotFoodBlocksTheMealAsNotEdible()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Inventory.Add(TestCatalogs.WoodItem, 20);
        person.Needs.Hunger = 50f;

        Assert.Equal(ActionBlocker.NotEdible, new EatCommand(person, TestCatalogs.WoodItem).Blocker(world));
    }

    // The player's Eat button stops only at no hunger at all, not at
    // WorldState.IsHungryEnoughToEat: being told to eat is not the same as deciding to.
    [Fact]
    public void HavingNoHungerLeftBlocksTheMealAsNotHungry()
    {
        var world = TestCatalogs.CreateWorld();
        var person = EaterWithFood(world);
        person.Needs.Hunger = 0f;

        Assert.Equal(ActionBlocker.NotHungry, new EatCommand(person, TestCatalogs.AppleItem).Blocker(world));
    }

    [Fact]
    public void CarryingNoneOfThatFoodBlocksTheMealAsMissingMaterials()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 50f;

        Assert.Equal(ActionBlocker.MissingMaterials, new EatCommand(person, TestCatalogs.AppleItem).Blocker(world));
    }

    // Knowledge is the last thing asked (see ActionBlocker.NotLearned), so a hungry person with an
    // empty pack hears about the pack. The player's menu leans on this: it forgives NotLearned for
    // actions where directing someone teaches them, and that would hide a second reason if
    // NotLearned could win over one.
    [Fact]
    public void AnEmptyPackIsBlamedBeforeNeverHavingLearnedToEat()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 50f;

        Assert.Equal(ActionBlocker.MissingMaterials, new EatCommand(person, TestCatalogs.AppleItem).Blocker(world));
    }

    [Fact]
    public void HavingNoHungerIsBlamedBeforeNeverHavingLearnedToEat()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        Assert.Equal(ActionBlocker.NotHungry, new EatCommand(person, TestCatalogs.AppleItem).Blocker(world));
    }
}
