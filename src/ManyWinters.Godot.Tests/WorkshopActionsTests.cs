using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// The workbench, worked out apart from the panel that draws it: what of a person's pack can be
// picked up, and what trying the picked things together would be. The player picks things, never
// a verb - the count of what they picked is the whole question.
public class WorkshopActionsTests
{
    private static Assembly.Part Cord(float quality = 0.5f) =>
        new(new MaterialId("plant_fibre"), TestWorld.Cord, quality, Volume: 15f);

    [Fact]
    public void TheBenchListsBothTiersOfThePack()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 3);
        person.Inventory.AddAssembly(Cord());

        var carried = WorkshopActions.Carried(world, person);

        Assert.Equal(["Wood", "plant fibre cord"], carried.Select(entry => entry.Label));
    }

    // Two of each tier, so each tier's own alphabetical order is exercised rather than trivially
    // true of a single entry - stock always ahead of worked things, but each sorted within itself.
    [Fact]
    public void EachTierIsListedInItsOwnStableOrder()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 3);
        person.Inventory.Add(TestWorld.Apple, 1);
        var stick = new Assembly.Part(new MaterialId("wood"), new FormId("stick"), Quality: 1f, Volume: 2f);
        person.Inventory.AddAssembly(Cord());
        person.Inventory.AddAssembly(stick);

        var carried = WorkshopActions.Carried(world, person);

        Assert.Equal(["Apple", "Wood", "plant fibre cord", "wood stick"], carried.Select(entry => entry.Label));
    }

    // The bench draws the thing rather than naming it, so how many are held is a field of its own
    // to mark the picture with, not something spelled into the name.
    [Fact]
    public void HowManyAreHeldIsCountedApartFromTheName()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 3);
        person.Inventory.AddAssembly(Cord());

        var carried = WorkshopActions.Carried(world, person);

        Assert.Equal([3, 1], carried.Select(entry => entry.Count));
    }

    [Fact]
    public void AnEmptyPackPutsNothingOnTheBench()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Empty(WorkshopActions.Carried(world, person));
    }

    // Making something out of the pack is named up front, unlike a reductive or combinative
    // verb - the bench does not make the player discover an axe.
    [Fact]
    public void MakingSomethingIsOfferedToSomebodyCarryingTheMaterialForIt()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, TestWorld.AxeInputAmount);

        var craft = Assert.Single(WorkshopActions.Recipes(world, person));

        Assert.Equal("Make axe", craft.Label);
        Assert.True(craft.IsAvailable);
        Assert.IsType<MakeCommand>(craft.Command);
    }

    // Two things makeable at once are listed in a stable alphabetical order, not whichever order
    // the catalog happens to declare them in.
    [Fact]
    public void TwoThingsMakeableAtOnceAreListedInAStableOrder()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, TestWorld.ChiselInputAmount);

        var labels = WorkshopActions.Recipes(world, person).Select(offer => offer.Label);

        Assert.Equal(["Make axe", "Make chisel"], labels);
    }

    // Carrying some of the material but not enough is the same as carrying none of it: the
    // recipe list is never a column of things greyed out, so a recipe that cannot be made right
    // now is simply not on it.
    [Fact]
    public void MakingSomethingIsNotOfferedWithSomeOfTheMaterialButNotEnough()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 1);

        Assert.Empty(WorkshopActions.Recipes(world, person));
    }

    // The same rule as carrying too little: the line is absent, so the bench never grows a
    // column of things nobody could make.
    [Fact]
    public void MakingSomethingIsNotOfferedWithNoneOfTheMaterialAtAll()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Apple, 5);

        Assert.Empty(WorkshopActions.Recipes(world, person));
    }

    // The other half of the same recipe list: one too heavy for the pack - a storage hut, unlike
    // the axe the same wood also buys - is not offered here at all.
    [Fact]
    public void MakingSomethingThatDoesNotFitInThePackIsNotOffered()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, TestWorld.StorageHutInputAmount);

        Assert.DoesNotContain(WorkshopActions.Recipes(world, person), offer => offer.Label == "Make storage hut");
    }

    [Fact]
    public void PickingNothingOffersNothing()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Grass, TestWorld.GrassPerCord);

        Assert.Null(WorkshopActions.Attempt(world, person, []));
    }

    // One thing picked is a reductive verb, and which one is the item's own business.
    [Fact]
    public void PickingOneThingThatCanBeWorkedDownOffersTheAttempt()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestWorld.BasicTwisting);
        person.Inventory.Add(TestWorld.Grass, TestWorld.GrassPerCord);
        var carried = WorkshopActions.Carried(world, person);

        var offer = WorkshopActions.Attempt(world, person, carried);

        Assert.NotNull(offer);
        Assert.Equal("Make", offer.Value.Label);
        Assert.IsType<TwistCommand>(offer.Value.Command);
        Assert.True(offer.Value.IsAvailable);
    }

    // The bench does not grow a second button for a second verb: the same one pick, and the item
    // says which verb it is.
    [Fact]
    public void PickingAStoneOffersTheSameOneAttemptAndItIsKnapping()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestWorld.BasicKnapping);
        person.Inventory.Add(TestWorld.Stone, 1);
        var carried = WorkshopActions.Carried(world, person);

        var offer = WorkshopActions.Attempt(world, person, carried);

        Assert.NotNull(offer);
        Assert.Equal("Make", offer.Value.Label);
        Assert.IsType<KnapCommand>(offer.Value.Command);
        Assert.True(offer.Value.IsAvailable);
    }

    // Nothing is said about what a thing could become: a pick that leads nowhere is simply not an
    // offer, so the bench cannot hand the player the answer.
    [Fact]
    public void PickingOneThingNothingCanBeDoneToOffersNothing()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 3);
        var carried = WorkshopActions.Carried(world, person);

        Assert.Null(WorkshopActions.Attempt(world, person, carried));
    }

    [Fact]
    public void PickingOneWorkedThingWithNothingToReworkOffersNothing()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.AddAssembly(Cord());
        var carried = WorkshopActions.Carried(world, person);

        Assert.Null(WorkshopActions.Attempt(world, person, carried));
    }

    // A made thing picked alone is worked over rather than worked down - which today means its
    // edge renewed, and is the first thing the bench offers on the instance tier.
    [Fact]
    public void PickingOneWorkedThingWithAnEdgeOffersToWorkItOver()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestWorld.BasicSharpening);
        person.Inventory.AddAssembly(new Assembly.Part(new MaterialId("stone"), new FormId("wedge"), 0.3f, 1f));
        var carried = WorkshopActions.Carried(world, person);

        var offer = WorkshopActions.Attempt(world, person, carried);

        Assert.NotNull(offer);
        Assert.Equal("Make", offer.Value.Label);
        Assert.IsType<SharpenCommand>(offer.Value.Command);
        Assert.True(offer.Value.IsAvailable);
    }

    [Fact]
    public void PickingTwoThingsOffersToBindThem()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestWorld.BasicBinding);
        person.Inventory.Add(TestWorld.Wood, 1);
        person.Inventory.Add(TestWorld.Apple, 1);
        person.Inventory.AddAssembly(Cord());
        var carried = WorkshopActions.Carried(world, person);
        var pair = carried.Where(entry => entry.Label.StartsWith("Wood", StringComparison.Ordinal)
                                          || entry.Label.StartsWith("Apple", StringComparison.Ordinal)).ToList();

        var offer = WorkshopActions.Attempt(world, person, pair);

        Assert.NotNull(offer);
        Assert.Equal("Make", offer.Value.Label);
        Assert.IsType<BindCommand>(offer.Value.Command);
        Assert.True(offer.Value.IsAvailable);
    }

    // The offer still comes back when it cannot run, carrying the world's own reason - that is
    // what lets the bench say why rather than going quiet.
    [Fact]
    public void BindingWithNoCordComesBackRefusedRatherThanAbsent()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.KnownTechniques.Add(TestWorld.BasicBinding);
        person.Inventory.Add(TestWorld.Wood, 1);
        person.Inventory.Add(TestWorld.Apple, 1);
        var carried = WorkshopActions.Carried(world, person);

        var offer = WorkshopActions.Attempt(world, person, carried);

        Assert.NotNull(offer);
        Assert.Equal(ActionBlocker.MissingMaterials, offer.Value.Blocker);
    }

    // Asking at the bench is the player showing them how, as everywhere else a directed action
    // teaches.
    [Fact]
    public void AnAttemptTeachesTheVerbItNeeds()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Grass, TestWorld.GrassPerCord);
        var carried = WorkshopActions.Carried(world, person);

        var offer = WorkshopActions.Attempt(world, person, carried);

        Assert.NotNull(offer);
        Assert.Empty(person.KnownTechniques);
        Assert.Equal(TwistCommand.Skill, offer.Value.TeachFirst);
        Assert.True(offer.Value.IsAvailable);
    }

    [Fact]
    public void PickingMoreThanTwoThingsOffersNothing()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 1);
        person.Inventory.Add(TestWorld.Apple, 1);
        person.Inventory.Add(TestWorld.Grass, 1);
        var carried = WorkshopActions.Carried(world, person);

        Assert.Equal(3, carried.Count);
        Assert.Null(WorkshopActions.Attempt(world, person, carried));
    }

    // What the player has to go on: what the thing is like, never what it is for.
    [Fact]
    public void OneThingInHandIsDescribedByWhatItIsLike()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Grass, 5);
        TestWorld.LetThemComeToKnow(world, person);
        var carried = WorkshopActions.Carried(world, person);

        Assert.Equal(["fibrous", "pliable", "light"], WorkshopActions.WordsFor(world, person, carried));
    }

    [Fact]
    public void AWorkedThingIsDescribedByTheSubstanceItIsMadeOf()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.AddAssembly(Cord());
        TestWorld.LetThemComeToKnow(world, person);
        var carried = WorkshopActions.Carried(world, person);

        Assert.Equal(["fibrous", "pliable", "light"], WorkshopActions.WordsFor(world, person, carried));
    }

    // Two things at once is a question about the pair; a wall of adjectives is not an answer.
    [Fact]
    public void TwoThingsInHandAreNotDescribedAtAll()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Grass, 5);
        person.Inventory.Add(TestWorld.Wood, 5);
        var carried = WorkshopActions.Carried(world, person);

        Assert.Empty(WorkshopActions.WordsFor(world, person, carried));
    }

    [Fact]
    public void NothingInHandIsDescribedByNothing()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));

        Assert.Empty(WorkshopActions.WordsFor(world, person, []));
    }

    // Nothing is known of what wood is like in this world, so the bench says nothing about it
    // rather than saying "unknown".
    [Fact]
    public void ASubstanceNobodyDescribedIsPassedOverInSilence()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 5);
        var carried = WorkshopActions.Carried(world, person);

        Assert.Empty(WorkshopActions.WordsFor(world, person, carried));
    }

    // Eating out of the pack is offered right on the bench, so the player never has to close it
    // to reach the same action off the card.
    [Fact]
    public void PickingOneFoodItemOffersToEatIt()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Needs.Hunger = 50f;
        person.Inventory.Add(TestWorld.Apple, 5);
        var carried = WorkshopActions.Carried(world, person);

        var offer = WorkshopActions.Eat(world, person, carried);

        Assert.NotNull(offer);
        Assert.Equal("Eat", offer.Value.Label);
        Assert.IsType<EatCommand>(offer.Value.Command);
        Assert.True(offer.Value.IsAvailable);
    }

    // Wood is not food, and picking it is still nothing to eat - the same rule the card follows.
    [Fact]
    public void PickingSomethingInedibleOffersNoEat()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Needs.Hunger = 50f;
        person.Inventory.Add(TestWorld.Wood, 5);
        var carried = WorkshopActions.Carried(world, person);

        Assert.Null(WorkshopActions.Eat(world, person, carried));
    }

    // A made thing is never eaten, whatever it is made of.
    [Fact]
    public void PickingAWorkedThingOffersNoEat()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Needs.Hunger = 50f;
        person.Inventory.AddAssembly(Cord());
        var carried = WorkshopActions.Carried(world, person);

        Assert.Null(WorkshopActions.Eat(world, person, carried));
    }

    [Fact]
    public void PickingTwoThingsOffersNoEat()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Needs.Hunger = 50f;
        person.Inventory.Add(TestWorld.Apple, 1);
        person.Inventory.Add(TestWorld.Wood, 1);
        var carried = WorkshopActions.Carried(world, person);

        Assert.Null(WorkshopActions.Eat(world, person, carried));
    }

    // Dropping a stack puts down the whole of it, not the single unit a pick stands for
    // elsewhere on this bench.
    [Fact]
    public void DroppingAStackPutsDownAllOfIt()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 3);
        var carried = WorkshopActions.Carried(world, person);

        var offer = WorkshopActions.Drop(world, person, carried);

        Assert.NotNull(offer);
        Assert.Equal("Drop", offer.Value.Label);
        var drop = Assert.IsType<DropCommand>(offer.Value.Command);
        Assert.Equal(new CarriedThing.Stock(TestWorld.Wood, 3), drop.What);
        Assert.True(offer.Value.IsAvailable);
    }

    [Fact]
    public void DroppingAWorkedThingPutsItDown()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.AddAssembly(Cord());
        var carried = WorkshopActions.Carried(world, person);

        var offer = WorkshopActions.Drop(world, person, carried);

        Assert.NotNull(offer);
        var drop = Assert.IsType<DropCommand>(offer.Value.Command);
        Assert.Equal(new CarriedThing.Worked(Cord()), drop.What);
    }

    [Fact]
    public void PickingNothingOrMoreThanOneThingOffersNoDrop()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", new Position(0, 0));
        person.Inventory.Add(TestWorld.Wood, 1);
        person.Inventory.Add(TestWorld.Apple, 1);
        var carried = WorkshopActions.Carried(world, person);

        Assert.Null(WorkshopActions.Drop(world, person, []));
        Assert.Null(WorkshopActions.Drop(world, person, carried));
    }
}
