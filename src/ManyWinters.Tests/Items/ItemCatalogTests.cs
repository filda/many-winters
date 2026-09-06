using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;

namespace ManyWinters.Tests.Items;

public class ItemCatalogTests
{
    private static readonly MaterialId Hide = new("hide");
    private static readonly MaterialId Stone = new("stone");
    private static readonly FormId Garment = new("garment");
    private static readonly FormId Lump = new("lump");

    private static readonly ItemKindId WarmClothing = new("warm_clothing");
    private static readonly ItemKindId StoneItem = new("stone");
    private static readonly ItemKindId Apple = new("apple");
    private static readonly ItemKindId Wood = new("wood");

    private static MaterialCatalog Materials() => new([
        new MaterialDefinition(Hide, "Hide", Density: 0.75f, Insulation: 1f),
        new MaterialDefinition(Stone, "Stone", Density: 2f),
    ]);

    [Fact]
    public void GetReturnsTheDefinitionForAKnownId()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(WarmClothing, "Warm Clothing", Hide, Garment, Volume: 4f),
        ], Materials());

        var definition = catalog.Get(WarmClothing);

        Assert.Equal("Warm Clothing", definition.DisplayName);
        Assert.Equal(Hide, definition.Material);
        Assert.Equal(Garment, definition.Form);
    }

    [Fact]
    public void GetThrowsForAnUnknownId()
    {
        var catalog = new ItemCatalog([], Materials());

        Assert.Throws<KeyNotFoundException>(() => catalog.Get(WarmClothing));
    }

    [Fact]
    public void InsulationForComesFromTheMaterialRatherThanTheItem()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(WarmClothing, "Warm Clothing", Hide, Garment, Volume: 4f),
        ], Materials());

        Assert.Equal(1f, catalog.InsulationFor(WarmClothing));
    }

    [Fact]
    public void InsulationForReturnsZeroForAMaterialThatInsulatesNothing()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(StoneItem, "Stone", Stone, Lump, Volume: 1f),
        ], Materials());

        Assert.Equal(0f, catalog.InsulationFor(StoneItem));
    }

    [Fact]
    public void InsulationForReturnsZeroForAnItemWithNoDefinition()
    {
        var catalog = new ItemCatalog([], Materials());

        Assert.Equal(0f, catalog.InsulationFor(Wood));
    }

    [Fact]
    public void InsulationForReturnsZeroWhenTheMaterialWasNeverDescribed()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(WarmClothing, "Warm Clothing", Hide, Garment, Volume: 4f),
        ], new MaterialCatalog([]));

        Assert.Equal(0f, catalog.InsulationFor(WarmClothing));
    }

    [Fact]
    public void WeightForMultipliesTheMaterialsDensityByTheItemsVolume()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(StoneItem, "Stone", Stone, Lump, Volume: 1.5f),
        ], Materials());

        Assert.Equal(3f, catalog.WeightFor(StoneItem));
    }

    [Fact]
    public void WeightForDiffersBetweenTwoItemsOfTheSameShapeInDifferentMaterials()
    {
        var hideLump = new ItemKindId("hide_lump");
        var catalog = new ItemCatalog([
            new ItemDefinition(StoneItem, "Stone", Stone, Lump, Volume: 2f),
            new ItemDefinition(hideLump, "Hide Lump", Hide, Lump, Volume: 2f),
        ], Materials());

        Assert.Equal(4f, catalog.WeightFor(StoneItem));
        Assert.Equal(1.5f, catalog.WeightFor(hideLump));
    }

    [Fact]
    public void WeightForReturnsZeroForAnItemWithNoDefinition()
    {
        var catalog = new ItemCatalog([], Materials());

        Assert.Equal(0f, catalog.WeightFor(Wood));
    }

    [Fact]
    public void WeightForReturnsZeroWhenTheMaterialWasNeverDescribed()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(StoneItem, "Stone", Stone, Lump, Volume: 1.5f),
        ], new MaterialCatalog([]));

        Assert.Equal(0f, catalog.WeightFor(StoneItem));
    }

    [Fact]
    public void HungerRestoredPerUnitForReturnsTheDefinitionsValue()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(Apple, "Apple", Stone, Lump, HungerRestoredPerUnit: 1f),
        ], Materials());

        Assert.Equal(1f, catalog.HungerRestoredPerUnitFor(Apple));
    }

    [Fact]
    public void HungerRestoredPerUnitForReturnsZeroForAnItemWithNoDefinition()
    {
        var catalog = new ItemCatalog([], Materials());

        Assert.Equal(0f, catalog.HungerRestoredPerUnitFor(Apple));
    }

    [Fact]
    public void CarryCapacityBonusForReturnsTheDefinitionsValue()
    {
        var basket = new ItemKindId("basket");
        var catalog = new ItemCatalog([
            new ItemDefinition(basket, "Basket", Stone, Lump, CarryCapacityBonus: 20f),
        ], Materials());

        Assert.Equal(20f, catalog.CarryCapacityBonusFor(basket));
    }

    [Fact]
    public void CarryCapacityBonusForReturnsZeroForAnItemWithNoDefinition()
    {
        var catalog = new ItemCatalog([], Materials());

        Assert.Equal(0f, catalog.CarryCapacityBonusFor(new ItemKindId("basket")));
    }

    [Fact]
    public void LoadFromDirectoryReadsOneDefinitionPerSubdirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-itemcatalog-{Guid.NewGuid():N}");
        WriteItem(root, "warm_clothing");

        try
        {
            var catalog = ItemCatalog.LoadFromDirectory(root, Materials());

            var definition = catalog.Get(WarmClothing);
            Assert.Equal("Warm Clothing", definition.DisplayName);
            Assert.Equal(Hide, definition.Material);
            Assert.Equal(Garment, definition.Form);
            Assert.Equal(3f, catalog.WeightFor(WarmClothing));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryIgnoresNonJsonFilesInAnItemFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-itemcatalog-{Guid.NewGuid():N}");
        var itemDir = WriteItem(root, "warm_clothing");
        File.WriteAllText(Path.Combine(itemDir, "notes.txt"), "this is not json and would blow up if read as such");

        try
        {
            var catalog = ItemCatalog.LoadFromDirectory(root, Materials());

            Assert.Equal(1f, catalog.InsulationFor(WarmClothing));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryThrowsInvalidDataExceptionForAMalformedDefinition()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-itemcatalog-{Guid.NewGuid():N}");
        var itemDir = Path.Combine(root, "warm_clothing");
        Directory.CreateDirectory(itemDir);
        var filePath = Path.Combine(itemDir, "warm_clothing.json");
        File.WriteAllText(filePath, "null");

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => ItemCatalog.LoadFromDirectory(root, Materials()));

            Assert.Contains(filePath, ex.Message, StringComparison.Ordinal);
            Assert.StartsWith("Item definition", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string WriteItem(string root, string id)
    {
        var itemDir = Path.Combine(root, id);
        Directory.CreateDirectory(itemDir);
        File.WriteAllText(
            Path.Combine(itemDir, $"{id}.json"),
            """{ "id": "warm_clothing", "displayName": "Warm Clothing", "material": "hide", "form": "garment", "volume": 4 }""");

        return itemDir;
    }
}
