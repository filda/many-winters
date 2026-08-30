using ManyWinters.Core.Items;

namespace ManyWinters.Tests.Items;

public class InventoryTests
{
    private static readonly ItemKindId Wood = new("wood");
    private static readonly ItemKindId Feather = new("feather");

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
        var catalog = new ItemCatalog([
            new ItemDefinition(Wood, "Wood", Weight: 1f),
            new ItemDefinition(Feather, "Feather", Weight: 0.1f),
        ]);
        var inventory = new Inventory();
        inventory.Add(Wood, 10);
        inventory.Add(Feather, 20);

        Assert.Equal(12f, inventory.TotalWeight(catalog));
    }

    [Fact]
    public void TotalWeightTreatsAKindWithNoDefinitionAsWeightless()
    {
        var catalog = new ItemCatalog([]);
        var inventory = new Inventory();
        inventory.Add(Wood, 10);

        Assert.Equal(0f, inventory.TotalWeight(catalog));
    }

    [Fact]
    public void AddUpToCapacityAddsEverythingWhenItAllFits()
    {
        var catalog = new ItemCatalog([new ItemDefinition(Wood, "Wood", Weight: 1f)]);
        var inventory = new Inventory();

        var added = inventory.AddUpToCapacity(Wood, 10, catalog, maxWeight: 50f);

        Assert.Equal(10, added);
        Assert.Equal(10, inventory.Get(Wood));
    }

    [Fact]
    public void AddUpToCapacityOnlyAddsWhatStillFitsWhenPartiallyFull()
    {
        var catalog = new ItemCatalog([new ItemDefinition(Wood, "Wood", Weight: 1f)]);
        var inventory = new Inventory();
        inventory.Add(Wood, 45);

        var added = inventory.AddUpToCapacity(Wood, 10, catalog, maxWeight: 50f);

        Assert.Equal(5, added);
        Assert.Equal(50, inventory.Get(Wood));
    }

    [Fact]
    public void AddUpToCapacityAddsNothingWhenAlreadyFull()
    {
        var catalog = new ItemCatalog([new ItemDefinition(Wood, "Wood", Weight: 1f)]);
        var inventory = new Inventory();
        inventory.Add(Wood, 50);

        var added = inventory.AddUpToCapacity(Wood, 10, catalog, maxWeight: 50f);

        Assert.Equal(0, added);
        Assert.Equal(50, inventory.Get(Wood));
    }

    [Fact]
    public void AddUpToCapacityIsUnlimitedForAZeroWeightItem()
    {
        var catalog = new ItemCatalog([new ItemDefinition(Wood, "Wood", Weight: 0f)]);
        var inventory = new Inventory();
        inventory.Add(Wood, 1000);

        var added = inventory.AddUpToCapacity(Wood, 10, catalog, maxWeight: 50f);

        Assert.Equal(10, added);
        Assert.Equal(1010, inventory.Get(Wood));
    }
}
