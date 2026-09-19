using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// What the player is offered for the person they have selected. Only acts on that person - out
// of their own pack, with their own hands - since what is aimed at something else in the world
// is asked for by pointing at it (TargetActions). Nothing here can ever be offered with nothing
// to act on.
public class PersonActionsTests
{
    private static ActionOffer OfType<TCommand>(WorldState world, Person person) =>
        Assert.Single(PersonActions.For(world, person), offer => offer.Command is TCommand);

    // Everything that needed a target - felling, burying, depositing, building, having a child -
    // left for the contextual menu, and took "nothing nearby" with it: every offer that is made
    // now has a command behind it.
    [Fact]
    public void EveryOfferHasSomethingToActOn()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Apple, 5);

        Assert.DoesNotContain(PersonActions.For(world, person), offer => offer.Command is null);
    }

    [Fact]
    public void OnlyActsOnThePersonThemselvesAreOffered()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Apple, 5);
        world.AddEntity(new Entity
        {
            Kind = TestWorld.AppleTree,
            Category = EntityCategory.Growable,
            Position = new Position(0, 0),
            Growth = new GrowthState { RemainingAmount = 100, MaxAmount = 100 },
        });

        var labels = PersonActions.For(world, person).Select(offer => offer.Label).ToList();

        Assert.Equal(["Eat", "Drop apple"], labels);
    }

    [Fact]
    public void TheSameWorldAlwaysOffersTheSameActionsInTheSameOrder()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Equal(
            PersonActions.For(world, person).Select(offer => offer.Label),
            PersonActions.For(world, person).Select(offer => offer.Label));
    }

    // An Eat button on an empty pack is an instruction to go and find food, which is not what
    // pressing it would do - so a hungry person carrying nothing is offered nothing.
    [Fact]
    public void EatingIsNotOfferedWithAnEmptyPack()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Needs.Hunger = 50f;

        Assert.Empty(PersonActions.For(world, person));
    }

    // Wood is not food, and carrying a pack of it is still an empty pack as far as eating goes -
    // however much else it is good for.
    [Fact]
    public void EatingIsNotOfferedForSomethingInedible()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Needs.Hunger = 50f;
        person.Inventory.Add(TestWorld.Wood, 5);

        Assert.DoesNotContain(PersonActions.For(world, person), offer => offer.Command is EatCommand);
    }

    // Making something out of what is in the pack is an act on the person themselves, so it
    // belongs here rather than in a menu aimed at something in the world.
    [Fact]
    public void MakingSomethingIsOfferedToSomebodyCarryingTheMaterialForIt()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, TestWorld.AxeInputAmount);

        var craft = OfType<MakeCommand>(world, person);

        Assert.Equal("Make axe", craft.Label);
        Assert.True(craft.IsAvailable);
        Assert.IsType<MakeCommand>(craft.Command);
    }

    // Offered from the first unit, not from the whole cost: "Make axe" over two of the five wood
    // it takes is a goal the player can send them after, and the blocker says how far off it is.
    [Fact]
    public void SomebodyPartWayToTheMaterialIsToldWhatIsMissing()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 1);

        Assert.Equal(ActionBlocker.MissingMaterials, OfType<MakeCommand>(world, person).Blocker);
    }

    // Carrying none of the material at all and the line is absent, the same rule Eat follows -
    // otherwise the card grows a column of things nobody could make.
    [Fact]
    public void MakingSomethingIsNotOfferedWithNoneOfTheMaterialAtAll()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Apple, 5);

        Assert.DoesNotContain(PersonActions.For(world, person), offer => offer.Command is MakeCommand);
    }

    [Fact]
    public void EatingIsAvailableToAHungryPersonCarryingFood()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Needs.Hunger = 50f;
        person.Inventory.Add(TestWorld.Apple, 5);

        var eat = OfType<EatCommand>(world, person);

        Assert.True(eat.IsAvailable);
        Assert.IsType<EatCommand>(eat.Command);
    }

    [Fact]
    public void AFullPersonIsToldTheyAreNotHungryRatherThanThatTheyLackFood()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Apple, 5);

        Assert.Equal(ActionBlocker.NotHungry, OfType<EatCommand>(world, person).Blocker);
    }

    // Pointing at the food is how the person is shown how to eat (see
    // SkillDefinition.BaseTechnique), so never having learned cannot be what stops the offer -
    // that would leave them no way to ever learn.
    [Fact]
    public void EatingIsNotBlockedForNotHavingBeenTaughtIt()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Needs.Hunger = 50f;
        person.Inventory.Add(TestWorld.Apple, 5);

        var eat = OfType<EatCommand>(world, person);

        Assert.Empty(person.KnownTechniques);
        Assert.True(eat.IsAvailable);
        Assert.Equal(EatCommand.Skill, eat.TeachFirst);
    }

    // Dropping is an act on the person themselves - putting something down out of their own pack -
    // so it belongs here rather than in a menu aimed at something in the world.
    [Fact]
    public void DroppingIsOfferedForEachItemKindCarried()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Apple, 5);

        var drop = OfType<DropItemCommand>(world, person);

        Assert.Equal("Drop apple", drop.Label);
        Assert.True(drop.IsAvailable);
        Assert.Equal(new DropItemCommand(person, TestWorld.Apple, 5), drop.Command);
    }

    // Carrying none of a kind at all and the line is absent, the same rule Eat and Crafts follow -
    // otherwise the card grows a column of things nobody has to put down.
    [Fact]
    public void DroppingIsNotOfferedWithAnEmptyPack()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.DoesNotContain(PersonActions.For(world, person), offer => offer.Command is DropItemCommand);
    }

    [Fact]
    public void DroppingIsOfferedOnePerItemKindInTheSameOrderEveryTime()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 3);
        person.Inventory.Add(TestWorld.Apple, 2);

        var labels = PersonActions.For(world, person)
            .Where(offer => offer.Command is DropItemCommand)
            .Select(offer => offer.Label);

        Assert.Equal(["Drop apple", "Drop wood"], labels);
    }

    [Fact]
    public void ADeadPersonIsOfferedTheSameActionsAndBlockedOnAllOfThem()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Needs.Hunger = 50f;
        person.Inventory.Add(TestWorld.Apple, 5);
        person.IsAlive = false;

        var offers = PersonActions.For(world, person);

        Assert.NotEmpty(offers);
        Assert.DoesNotContain(offers, offer => offer.IsAvailable);
    }
}
