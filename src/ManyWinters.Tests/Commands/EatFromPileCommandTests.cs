using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class EatFromPileCommandTests
{
    [Fact]
    public void EatingTakesOnlyWhatTheMealNeedsAndLeavesTheRestOnThePile()
    {
        var world = TestCatalogs.CreateWorld();
        // Test apples restore one hunger each, so a hunger of 60 is 60 apples.
        var pile = world.SpawnItemPile(TestCatalogs.AppleItem, new Position(0, 0), 100);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 60f;

        world.Execute(new EatFromPileCommand(person, pile));

        Assert.Equal(0f, person.Needs.Hunger);
        Assert.Equal(40, pile.StaticAmount);
        Assert.Contains(pile, world.Entities);
        Assert.Equal(0, person.Inventory.Get(TestCatalogs.AppleItem));
    }

    [Fact]
    public void EatingThePileCleanRemovesIt()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.AppleItem, new Position(0, 0), 1);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 90f;

        world.Execute(new EatFromPileCommand(person, pile));

        Assert.Equal(0, pile.StaticAmount);
        Assert.DoesNotContain(pile, world.Entities);
    }

    [Fact]
    public void NobodyNeedsToKnowGatheringToEatFromAPile()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.AppleItem, new Position(0, 0), 50);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 60f;

        Assert.Equal(ActionBlocker.None, new EatFromPileCommand(person, pile).Blocker(world));
    }

    [Fact]
    public void SomebodyWhoNeverLearnedToEatCannot()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.AppleItem, new Position(0, 0), 50);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.Needs.Hunger = 60f;

        world.Execute(new EatFromPileCommand(person, pile));

        Assert.Equal(ActionBlocker.NotLearned, new EatFromPileCommand(person, pile).Blocker(world));
        Assert.Equal(50, pile.StaticAmount);
    }

    [Fact]
    public void APileOfSomethingInedibleIsNotAMeal()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.WoodItem, new Position(0, 0), 5);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 60f;

        world.Execute(new EatFromPileCommand(person, pile));

        Assert.Equal(ActionBlocker.NotEdible, new EatFromPileCommand(person, pile).Blocker(world));
        Assert.Equal(5, pile.StaticAmount);
    }

    [Fact]
    public void EatingFromBeyondReachDoesNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.AppleItem, new Position(world.Configuration.Rules.MaxInteractionDistance + 1, 0), 50);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 60f;

        world.Execute(new EatFromPileCommand(person, pile));

        Assert.Equal(ActionBlocker.TooFar, new EatFromPileCommand(person, pile).Blocker(world));
        Assert.Equal(60f, person.Needs.Hunger);
    }

    [Fact]
    public void AnEmptiedPileIsGone()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.AppleItem, new Position(0, 0), 1);
        pile.StaticAmount = 0;
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.Needs.Hunger = 60f;

        Assert.Equal(ActionBlocker.TargetIsGone, new EatFromPileCommand(person, pile).Blocker(world));
    }

    [Fact]
    public void ADeadPersonCannotEat()
    {
        var world = TestCatalogs.CreateWorld();
        var pile = world.SpawnItemPile(TestCatalogs.AppleItem, new Position(0, 0), 50);
        var person = world.SpawnPerson("Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        person.IsAlive = false;

        Assert.Equal(ActionBlocker.ActorIsDead, new EatFromPileCommand(person, pile).Blocker(world));
    }
}
