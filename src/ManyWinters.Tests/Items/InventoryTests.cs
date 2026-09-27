using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;

namespace ManyWinters.Tests.Items;

public class InventoryTests
{
    private static readonly ItemKindId Wood = new("wood");
    private static readonly ItemKindId Feather = new("feather");
    private static readonly ItemKindId Stone = new("stone");

    private static readonly MaterialId Stuff = new("stuff");
    private static readonly FormId Lump = new("lump");

    // These tests are about how weight adds up, not where a unit weight comes from: density 1,
    // so an item's volume reads directly as its weight.
    private static ItemCatalog CatalogOf(params ItemDefinition[] items) =>
        new(items, new MaterialCatalog([new MaterialDefinition(Stuff, "Stuff", Density: 1f)]), new FormCatalog([]));

    private static ItemDefinition Weighing(ItemKindId id, string displayName, float weight) =>
        new(id, displayName, Stuff, Lump, weight);

    // For the age-ledger tests (docs/todo/fauna-plan.md phase 4c): one catalog with a kind whose
    // material spoils, so Add/Remove/Transfer/Expire have something to track.
    private static readonly ItemKindId Meat = new("meat");
    private static readonly MaterialId Perishable = new("meat");
    private const long MeatShelfLifeTicks = 30;

    private static ItemCatalog PerishableCatalog() => new(
        [new ItemDefinition(Meat, "Meat", Perishable, Lump, 1f)],
        new MaterialCatalog([new MaterialDefinition(Perishable, "Meat", Density: 1f, ShelfLifeTicks: MeatShelfLifeTicks)]),
        new FormCatalog([]));

    // A made object goes in whole or not at all, unlike a stack: half an axe is nothing.
    [Fact]
    public void AnAssemblyIsTakenWholeOrNotAtAll()
    {
        var catalog = CatalogOf(Weighing(Wood, "Wood", 2f));
        var inventory = new Inventory();
        var heavy = new Assembly.Part(Stuff, Lump, Quality: 1f, Volume: 10f);

        Assert.False(inventory.AddAssemblyIfItFits(heavy, catalog, maxWeight: 5f));
        Assert.Empty(inventory.Assemblies);

        Assert.True(inventory.AddAssemblyIfItFits(heavy, catalog, maxWeight: 10f));
        Assert.Single(inventory.Assemblies);
    }

    // What is already carried counts against the room for it.
    [Fact]
    public void AnAssemblyDoesNotFitOnceThePackIsAlreadyFull()
    {
        var catalog = CatalogOf(Weighing(Wood, "Wood", 2f));
        var inventory = new Inventory();
        inventory.Add(Wood, 2);

        Assert.False(inventory.AddAssemblyIfItFits(new Assembly.Part(Stuff, Lump, 1f, 2f), catalog, maxWeight: 5f));
    }

    // What is worth chopping with is asked of the whole pack, not of one tier: a hafted edge
    // somebody made is a worked object, and a found flint would be a count.
    [Fact]
    public void BestChoppingScoreWeighsWorkedThingsAlongsideRawStock()
    {
        var edge = new MaterialId("flint");
        var wedge = new FormId("wedge");
        var catalog = new ItemCatalog(
            [Weighing(Wood, "Wood", 2f)],
            new MaterialCatalog([new MaterialDefinition(Stuff, "Stuff", Density: 1f), new MaterialDefinition(edge, "Flint", Density: 2f, Hardness: 1f)]),
            new FormCatalog([new FormDefinition(Lump, "Lump"), new FormDefinition(wedge, "Wedge", EdgeSharpness: 1f)]));
        var inventory = new Inventory();
        inventory.Add(Wood, 1);

        Assert.Equal(0f, inventory.BestChoppingScore(catalog));

        inventory.AddAssembly(new Assembly.Part(edge, wedge, Quality: 1f, Volume: 1f));

        Assert.True(inventory.BestChoppingScore(catalog) > 0f);
    }

    [Fact]
    public void GetReturnsZeroForAKindThatWasNeverAdded()
    {
        var inventory = new Inventory();

        Assert.Equal(0, inventory.Get(Wood));
    }

    [Fact]
    public void AddIncreasesTheCount()
    {
        var inventory = new Inventory();

        inventory.Add(Wood, 5);
        inventory.Add(Wood, 3);

        Assert.Equal(8, inventory.Get(Wood));
    }

    [Fact]
    public void RemoveDecreasesTheCountAndReturnsTrueWhenEnoughIsAvailable()
    {
        var inventory = new Inventory();
        inventory.Add(Wood, 5);

        var removed = inventory.Remove(Wood, 3);

        Assert.True(removed);
        Assert.Equal(2, inventory.Get(Wood));
    }

    [Fact]
    public void RemoveDropsTheKindEntirelyOnceItReachesZero()
    {
        var inventory = new Inventory();
        inventory.Add(Wood, 5);

        inventory.Remove(Wood, 5);

        Assert.Empty(inventory.Counts);
    }

    [Fact]
    public void RemoveReturnsFalseAndDoesNothingWhenNotEnoughIsAvailable()
    {
        var inventory = new Inventory();
        inventory.Add(Wood, 2);

        var removed = inventory.Remove(Wood, 3);

        Assert.False(removed);
        Assert.Equal(2, inventory.Get(Wood));
    }

    [Fact]
    public void TotalWeightSumsWeightAcrossEveryKindHeld()
    {
        var catalog = CatalogOf(
            Weighing(Wood, "Wood", 1f),
            Weighing(Feather, "Feather", 0.1f));
        var inventory = new Inventory();
        inventory.Add(Wood, 10);
        inventory.Add(Feather, 20);

        Assert.Equal(12f, inventory.TotalWeight(catalog));
    }

    [Fact]
    public void TotalWeightTreatsAKindWithNoDefinitionAsWeightless()
    {
        var catalog = CatalogOf();
        var inventory = new Inventory();
        inventory.Add(Wood, 10);

        Assert.Equal(0f, inventory.TotalWeight(catalog));
    }

    [Fact]
    public void AddUpToCapacityAddsEverythingWhenItAllFits()
    {
        var catalog = CatalogOf(Weighing(Wood, "Wood", 1f));
        var inventory = new Inventory();

        var added = inventory.AddUpToCapacity(Wood, 10, catalog, maxWeight: 50f);

        Assert.Equal(10, added);
        Assert.Equal(10, inventory.Get(Wood));
    }

    [Fact]
    public void AddUpToCapacityOnlyAddsWhatStillFitsWhenPartiallyFull()
    {
        var catalog = CatalogOf(Weighing(Wood, "Wood", 1f));
        var inventory = new Inventory();
        inventory.Add(Wood, 45);

        var added = inventory.AddUpToCapacity(Wood, 10, catalog, maxWeight: 50f);

        Assert.Equal(5, added);
        Assert.Equal(50, inventory.Get(Wood));
    }

    [Fact]
    public void AddUpToCapacityAddsNothingWhenAlreadyFull()
    {
        var catalog = CatalogOf(Weighing(Wood, "Wood", 1f));
        var inventory = new Inventory();
        inventory.Add(Wood, 50);

        var added = inventory.AddUpToCapacity(Wood, 10, catalog, maxWeight: 50f);

        Assert.Equal(0, added);
        Assert.Equal(50, inventory.Get(Wood));
    }

    [Fact]
    public void AddUpToCapacityIsUnlimitedForAZeroWeightItem()
    {
        var catalog = CatalogOf(Weighing(Wood, "Wood", 0f));
        var inventory = new Inventory();
        inventory.Add(Wood, 1000);

        var added = inventory.AddUpToCapacity(Wood, 10, catalog, maxWeight: 50f);

        Assert.Equal(10, added);
        Assert.Equal(1010, inventory.Get(Wood));
    }

    [Fact]
    public void AddUpToCapacityCountsHowManyUnitsFitRatherThanHowMuchTheyWeigh()
    {
        // What fits is headroom divided by unit weight: ten kilos is five stones, not twenty.
        var catalog = CatalogOf(Weighing(Stone, "Stone", 2f));
        var inventory = new Inventory();

        var added = inventory.AddUpToCapacity(Stone, 20, catalog, maxWeight: 10f);

        Assert.Equal(5, added);
        Assert.Equal(10f, inventory.TotalWeight(catalog));
    }

    [Fact]
    public void AddUpToCapacityLeavesNoEmptyEntryBehindWhenNothingFits()
    {
        // A zero-count entry would read as "carrying stone" to anything checking what's held, so
        // a refused add has to leave no trace.
        var catalog = CatalogOf(Weighing(Stone, "Stone", 2f));
        var inventory = new Inventory();

        var added = inventory.AddUpToCapacity(Stone, 5, catalog, maxWeight: 0f);

        Assert.Equal(0, added);
        Assert.Empty(inventory.Counts);
    }

    [Fact]
    public void HasRoomForIsTrueWhileAtLeastOneUnitStillFits()
    {
        var catalog = CatalogOf(Weighing(Stone, "Stone", 2f));
        var inventory = new Inventory();
        inventory.Add(Stone, 24);

        Assert.True(inventory.HasRoomFor(Stone, catalog, maxWeight: 50f));
    }

    [Fact]
    public void HasRoomForIsFalseOnceNotEvenOneUnitFits()
    {
        // 49 of 50 kilos used and a unit weighs 2: the last kilo is not room, matching the same
        // rounding-down done elsewhere.
        var catalog = CatalogOf(Weighing(Stone, "Stone", 2f));
        var inventory = new Inventory();
        inventory.Add(Stone, 24);

        Assert.False(inventory.HasRoomFor(Stone, catalog, maxWeight: 49f));
    }

    [Fact]
    public void HasRoomForIsFalseWhenTheInventoryIsAlreadyFullOfSomethingElse()
    {
        var catalog = CatalogOf(Weighing(Wood, "Wood", 1f), Weighing(Stone, "Stone", 2f));
        var inventory = new Inventory();
        inventory.Add(Wood, 50);

        Assert.False(inventory.HasRoomFor(Stone, catalog, maxWeight: 50f));
    }

    [Fact]
    public void HasRoomForIsAlwaysTrueForAZeroWeightItem()
    {
        var catalog = CatalogOf(Weighing(Feather, "Feather", 0f));
        var inventory = new Inventory();
        inventory.Add(Feather, 1000);

        Assert.True(inventory.HasRoomFor(Feather, catalog, maxWeight: 0f));
    }

    // The instance tier: a worked thing is held as itself, not as a count.
    [Fact]
    public void AWorkedThingIsHeldAsItselfRatherThanCounted()
    {
        var inventory = new Inventory();
        var cord = new Assembly.Part(Stuff, new FormId("cord"), Quality: 0.5f, Volume: 3f);

        inventory.AddAssembly(cord);

        Assert.Equal(cord, Assert.Single(inventory.Assemblies));
        Assert.Empty(inventory.Counts);
    }

    [Fact]
    public void TwoWorkedThingsOfTheSameShapeDoNotCollapseIntoOne()
    {
        var inventory = new Inventory();
        var form = new FormId("cord");

        inventory.AddAssembly(new Assembly.Part(Stuff, form, Quality: 0.2f, Volume: 3f));
        inventory.AddAssembly(new Assembly.Part(Stuff, form, Quality: 0.9f, Volume: 3f));

        Assert.Equal(2, inventory.Assemblies.Count);
    }

    [Fact]
    public void TotalWeightCountsWorkedThingsAlongsideStackedOnes()
    {
        var inventory = new Inventory();
        var catalog = CatalogOf(Weighing(Wood, "Wood", 2f));
        inventory.Add(Wood, 3);
        inventory.AddAssembly(new Assembly.Part(Stuff, new FormId("cord"), Quality: 0.5f, Volume: 4f));

        // Six for the stacked wood, four for the cord: density is 1 in this catalog.
        Assert.Equal(10f, inventory.TotalWeight(catalog));
    }

    [Fact]
    public void AnEmptyInventoryCarriesNoWorkedThings()
    {
        Assert.Empty(new Inventory().Assemblies);
    }

    // Carry capacity is the whole pack's business, so a worked thing fills it like anything else.
    [Fact]
    public void AWorkedThingTakesUpCarryCapacity()
    {
        var inventory = new Inventory();
        var catalog = CatalogOf(Weighing(Wood, "Wood", 2f));
        inventory.AddAssembly(new Assembly.Part(Stuff, new FormId("cord"), Quality: 0.5f, Volume: 9f));

        Assert.False(inventory.HasRoomFor(Wood, catalog, maxWeight: 10f));
    }

    // docs/todo/fauna-plan.md phase 4c: the age ledger. Untimed Add/plain Remove behave exactly
    // as before (every test above uses them); these are about the tick-aware overloads.

    [Fact]
    public void AddWithATickEnrollsAPerishableKindInTheAgeLedgerButNotANonPerishableOne()
    {
        var catalog = PerishableCatalog();
        var inventory = new Inventory();

        inventory.Add(Meat, 5, tick: 10, catalog);
        inventory.Add(Wood, 5, tick: 10, catalog);

        Assert.Equal(5, inventory.Ages[Meat].Sum(entry => entry.Count));
        Assert.False(inventory.Ages.ContainsKey(Wood));
    }

    // The invariant docs/todo/fauna-plan.md phase 4c calls for: a ledgered kind's entries always
    // sum to its count, through Add, Remove and Expire.
    [Fact]
    public void TheLedgerTotalAlwaysEqualsTheCountThroughAddRemoveAndExpire()
    {
        var catalog = PerishableCatalog();
        var inventory = new Inventory();

        inventory.Add(Meat, 5, tick: 0, catalog);
        inventory.Add(Meat, 5, tick: 10, catalog);
        Assert.Equal(inventory.Get(Meat), inventory.Ages[Meat].Sum(entry => entry.Count));

        inventory.Remove(Meat, 3);
        Assert.Equal(inventory.Get(Meat), inventory.Ages[Meat].Sum(entry => entry.Count));

        inventory.Expire(currentTick: MeatShelfLifeTicks, catalog);
        Assert.Equal(inventory.Get(Meat), inventory.Ages.GetValueOrDefault(Meat, []).Sum(entry => entry.Count));
    }

    [Fact]
    public void RemoveConsumesTheOldestUnitsFirst()
    {
        var catalog = PerishableCatalog();
        var inventory = new Inventory();
        inventory.Add(Meat, 5, tick: 0, catalog);
        inventory.Add(Meat, 5, tick: 20, catalog);

        inventory.Remove(Meat, 5);

        // Only the batch added at tick 20 should remain - it survives past the older batch's
        // shelf life, which the older batch (already fully consumed) would not have.
        Assert.Empty(inventory.Expire(currentTick: 20 + MeatShelfLifeTicks - 1, catalog));
        Assert.Equal(5, inventory.Expire(currentTick: 20 + MeatShelfLifeTicks, catalog).Sum(loss => loss.Count));
    }

    [Fact]
    public void ExpireDropsOnlyWhatHasCrossedItsOwnShelfLifeAndReportsTheLoss()
    {
        var catalog = PerishableCatalog();
        var inventory = new Inventory();
        inventory.Add(Meat, 5, tick: 0, catalog);

        var stillFresh = inventory.Expire(currentTick: MeatShelfLifeTicks - 1, catalog);
        Assert.Empty(stillFresh);
        Assert.Equal(5, inventory.Get(Meat));

        var lost = inventory.Expire(currentTick: MeatShelfLifeTicks, catalog);
        Assert.Equal((Meat, 5), Assert.Single(lost));
        Assert.Equal(0, inventory.Get(Meat));
    }

    [Fact]
    public void ExpireLeavesANonPerishableKindAloneNoMatterHowMuchTimePasses()
    {
        var catalog = PerishableCatalog();
        var inventory = new Inventory();
        inventory.Add(Wood, 5, tick: 0, catalog);

        var lost = inventory.Expire(currentTick: 1_000_000, catalog);

        Assert.Empty(lost);
        Assert.Equal(5, inventory.Get(Wood));
    }

    // Untracked stock (the plain, untimed Add every other test in this file uses) is exempt from
    // Expire - it was never enrolled in the ledger to begin with, the deliberate simplification
    // that keeps the ~200 existing untimed test setups elsewhere in this codebase unaffected.
    [Fact]
    public void StockAddedWithoutATickNeverExpires()
    {
        var catalog = PerishableCatalog();
        var inventory = new Inventory();
        inventory.Add(Meat, 5);

        var lost = inventory.Expire(currentTick: 1_000_000, catalog);

        Assert.Empty(lost);
        Assert.Equal(5, inventory.Get(Meat));
    }

    // Transfer/TransferUpToCapacity: moving something already aged does not restamp it, unlike
    // Add (docs/todo/fauna-plan.md phase 4c) - meat butchered at tick 0, deposited at tick 10 and
    // withdrawn at tick 20 still spoils at tick 30, not at tick 20 + 30.
    [Fact]
    public void TransferPreservesTheOriginalAgeRatherThanRestampingItNow()
    {
        var catalog = PerishableCatalog();
        var pack = new Inventory();
        var store = new Inventory();
        pack.Add(Meat, 5, tick: 0, catalog);

        pack.Transfer(Meat, 5, store);
        Assert.Equal(5, store.Get(Meat));

        var backInPack = new Inventory();
        store.TransferUpToCapacity(Meat, 5, backInPack, catalog, maxWeight: 100f);

        var beforeShelfLife = backInPack.Expire(currentTick: MeatShelfLifeTicks - 1, catalog);
        Assert.Empty(beforeShelfLife);

        var atShelfLife = backInPack.Expire(currentTick: MeatShelfLifeTicks, catalog);
        Assert.Equal(5, atShelfLife.Sum(loss => loss.Count));
    }

    [Fact]
    public void TransferUpToCapacityOnlyMovesWhatFitsAndLeavesTheRestBehind()
    {
        var catalog = CatalogOf(Weighing(Wood, "Wood", 1f));
        var source = new Inventory();
        var destination = new Inventory();
        source.Add(Wood, 10);
        destination.Add(Wood, 45);

        var moved = source.TransferUpToCapacity(Wood, 10, destination, catalog, maxWeight: 50f);

        Assert.Equal(5, moved);
        Assert.Equal(5, source.Get(Wood));
        Assert.Equal(50, destination.Get(Wood));
    }

    [Fact]
    public void TransferMovesTheWholeUncappedAmountRegardlessOfDestinationCapacity()
    {
        var source = new Inventory();
        var destination = new Inventory();
        source.Add(Wood, 500);

        var moved = source.Transfer(Wood, 500, destination);

        Assert.Equal(500, moved);
        Assert.Equal(0, source.Get(Wood));
        Assert.Equal(500, destination.Get(Wood));
    }

    // The whole point of an assembly's own MadeTick (docs/todo/fauna-plan.md phase 4c): a worked
    // object spoils by the shortest shelf life among its own parts' materials, built directly
    // rather than through Knap/Twist/Bind/Sharpen (their production wiring is covered by each
    // command's own tests).
    [Fact]
    public void ExpireRemovesAWorkedThingOnceItsOwnPartsShelfLifeIsUp()
    {
        var catalog = PerishableCatalog();
        var inventory = new Inventory();
        var perishableThing = new Assembly.Part(Perishable, Lump, Quality: 1f, Volume: 1f) { MadeTick = 0 };
        inventory.AddAssembly(perishableThing);

        inventory.Expire(currentTick: MeatShelfLifeTicks - 1, catalog);
        Assert.Single(inventory.Assemblies);

        inventory.Expire(currentTick: MeatShelfLifeTicks, catalog);
        Assert.Empty(inventory.Assemblies);
    }

    [Fact]
    public void ExpireLeavesAWorkedThingOfANonPerishableMaterialAlone()
    {
        var catalog = PerishableCatalog();
        var inventory = new Inventory();
        inventory.AddAssembly(new Assembly.Part(Stuff, Lump, Quality: 1f, Volume: 1f) { MadeTick = 0 });

        inventory.Expire(currentTick: 1_000_000, catalog);

        Assert.Single(inventory.Assemblies);
    }
}
