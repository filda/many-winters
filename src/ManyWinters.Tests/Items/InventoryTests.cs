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

    // These tests are about how an inventory adds weight up, not about where a unit weight
    // comes from, so the one material here has density 1 and an item's volume reads directly as
    // its weight.
    private static ItemCatalog CatalogOf(params ItemDefinition[] items) =>
        new(items, new MaterialCatalog([new MaterialDefinition(Stuff, "Stuff", Density: 1f)]));

    private static ItemDefinition Weighing(ItemKindId id, string displayName, float weight) =>
        new(id, displayName, Stuff, Lump, weight);

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
        // Ten kilos of headroom is five stones, not twenty - what fits is the headroom divided
        // by the unit weight.
        var catalog = CatalogOf(Weighing(Stone, "Stone", 2f));
        var inventory = new Inventory();

        var added = inventory.AddUpToCapacity(Stone, 20, catalog, maxWeight: 10f);

        Assert.Equal(5, added);
        Assert.Equal(10f, inventory.TotalWeight(catalog));
    }

    [Fact]
    public void AddUpToCapacityLeavesNoEmptyEntryBehindWhenNothingFits()
    {
        // A zero-count entry would show up in Counts as "carrying stone" - to anything walking
        // the inventory (the UI, HasEdibleFood) that's indistinguishable from actually having
        // some, so a refused add has to leave no trace at all.
        var catalog = CatalogOf(Weighing(Stone, "Stone", 2f));
        var inventory = new Inventory();

        var added = inventory.AddUpToCapacity(Stone, 5, catalog, maxWeight: 0f);

        Assert.Equal(0, added);
        Assert.Empty(inventory.Counts);
    }
}
