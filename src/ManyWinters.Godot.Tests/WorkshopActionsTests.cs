using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// The workbench, worked out apart from the panel that draws it: what of a person's pack can be
// picked up, and what trying the picked things together would be. The player picks things, never
// a verb - the count of what they picked is the whole question (see WorkshopActions).
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

    // The bench draws the thing rather than naming it, so how many are held is a field of its own
    // to mark the picture with, not something spelled into the name (see WorkshopPanel).
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
        Assert.Equal("Try it", offer.Value.Label);
        Assert.IsType<TwistCommand>(offer.Value.Command);
        Assert.True(offer.Value.IsAvailable);
    }

    // The bench does not grow a second button for a second verb: the same one pick, and the item
    // says which verb it is (see ReductiveVerbs).
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
        Assert.Equal("Try it", offer.Value.Label);
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
        Assert.Equal("Try it", offer.Value.Label);
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
        Assert.IsType<BindCommand>(offer.Value.Command);
        Assert.True(offer.Value.IsAvailable);
    }

    // The offer still comes back when it cannot run, carrying the world's own reason - that is
    // what lets the bench say why rather than going quiet (see ActionBlockerText).
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
    // teaches (see ActionOffer.TeachFirst).
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
}
