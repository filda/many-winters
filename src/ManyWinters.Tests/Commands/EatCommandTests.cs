using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class EatCommandTests
{
    [Fact]
    public void EatingRelievesHungerAndConsumesOnlyAsMuchAsWasNeeded()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 15;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        world.Execute(new EatCommand(person.Id, TestCatalogs.AppleItem));

        Assert.Equal(0f, person.Needs.Hunger);
        Assert.Equal(5, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void EatingNeverReducesHungerBelowZero()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 5;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        world.Execute(new EatCommand(person.Id, TestCatalogs.AppleItem));

        Assert.Equal(0f, person.Needs.Hunger);
        Assert.Equal(15, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void EatingOnlyConsumesWhatIsAvailableWhenThereIsNotEnoughToFullySatisfyHunger()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 50;
        person.Inventory.Add(TestCatalogs.AppleItem, 10);

        world.Execute(new EatCommand(person.Id, TestCatalogs.AppleItem));

        Assert.Equal(40f, person.Needs.Hunger);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void EatingWithNoHungerDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 0;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        world.Execute(new EatCommand(person.Id, TestCatalogs.AppleItem));

        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void EatingWithNoneOfThatFoodInInventoryDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 50;

        world.Execute(new EatCommand(person.Id, TestCatalogs.AppleItem));

        Assert.Equal(50f, person.Needs.Hunger);
    }

    [Fact]
    public void EatingAnItemThatIsNotFoodDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 50;
        person.Inventory.Add(TestCatalogs.WoodItem, 20);

        world.Execute(new EatCommand(person.Id, TestCatalogs.WoodItem));

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.WoodItem));
    }

    [Fact]
    public void EatingByADeadPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.AddPerson("Ava", new Position(0, 0));
        person.IsAlive = false;
        person.Needs.Hunger = 50;
        person.Inventory.Add(TestCatalogs.AppleItem, 20);

        world.Execute(new EatCommand(person.Id, TestCatalogs.AppleItem));

        Assert.Equal(50f, person.Needs.Hunger);
        Assert.Equal(20, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void EatingWithAnUnknownPersonDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();

        world.Execute(new EatCommand(new PersonId(999), TestCatalogs.AppleItem));
    }
}
