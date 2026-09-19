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

    private static readonly FormId Wedge = new("wedge");
    private static readonly ItemKindId Axe = new("axe");

    private const float StoneHardness = 0.8f;

    private static FormCatalog Forms() => new([
        new FormDefinition(Garment, "Garment"),
        new FormDefinition(Lump, "Lump"),
        new FormDefinition(Wedge, "Wedge", EdgeSharpness: 1f),
    ]);

    private static MaterialCatalog Materials() => new([
        new MaterialDefinition(Hide, "Hide", Density: 0.75f, Insulation: 1f),
        new MaterialDefinition(Stone, "Stone", Density: 2f, Hardness: StoneHardness),
    ]);

    [Fact]
    public void GetReturnsTheDefinitionForAKnownId()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(WarmClothing, "Warm Clothing", Hide, Garment, Volume: 4f),
        ], Materials(), Forms());

        var definition = catalog.Get(WarmClothing);

        Assert.Equal("Warm Clothing", definition.DisplayName);
        Assert.Equal(Hide, definition.Material);
        Assert.Equal(Garment, definition.Form);
    }

    [Fact]
    public void GetThrowsForAnUnknownId()
    {
        var catalog = new ItemCatalog([], Materials(), Forms());

        Assert.Throws<KeyNotFoundException>(() => catalog.Get(WarmClothing));
    }

    [Fact]
    public void InsulationForComesFromTheMaterialRatherThanTheItem()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(WarmClothing, "Warm Clothing", Hide, Garment, Volume: 4f),
        ], Materials(), Forms());

        Assert.Equal(1f, catalog.InsulationFor(WarmClothing));
    }

    [Fact]
    public void InsulationForReturnsZeroForAMaterialThatInsulatesNothing()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(StoneItem, "Stone", Stone, Lump, Volume: 1f),
        ], Materials(), Forms());

        Assert.Equal(0f, catalog.InsulationFor(StoneItem));
    }

    [Fact]
    public void InsulationForReturnsZeroForAnItemWithNoDefinition()
    {
        var catalog = new ItemCatalog([], Materials(), Forms());

        Assert.Equal(0f, catalog.InsulationFor(Wood));
    }

    [Fact]
    public void InsulationForReturnsZeroWhenTheMaterialWasNeverDescribed()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(WarmClothing, "Warm Clothing", Hide, Garment, Volume: 4f),
        ], new MaterialCatalog([]), Forms());

        Assert.Equal(0f, catalog.InsulationFor(WarmClothing));
    }

    [Fact]
    public void WeightForMultipliesTheMaterialsDensityByTheItemsVolume()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(StoneItem, "Stone", Stone, Lump, Volume: 1.5f),
        ], Materials(), Forms());

        Assert.Equal(3f, catalog.WeightFor(StoneItem));
    }

    [Fact]
    public void WeightForDiffersBetweenTwoItemsOfTheSameShapeInDifferentMaterials()
    {
        var hideLump = new ItemKindId("hide_lump");
        var catalog = new ItemCatalog([
            new ItemDefinition(StoneItem, "Stone", Stone, Lump, Volume: 2f),
            new ItemDefinition(hideLump, "Hide Lump", Hide, Lump, Volume: 2f),
        ], Materials(), Forms());

        Assert.Equal(4f, catalog.WeightFor(StoneItem));
        Assert.Equal(1.5f, catalog.WeightFor(hideLump));
    }

    [Fact]
    public void WeightForReturnsZeroForAnItemWithNoDefinition()
    {
        var catalog = new ItemCatalog([], Materials(), Forms());

        Assert.Equal(0f, catalog.WeightFor(Wood));
    }

    [Fact]
    public void WeightForReturnsZeroWhenTheMaterialWasNeverDescribed()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(StoneItem, "Stone", Stone, Lump, Volume: 1.5f),
        ], new MaterialCatalog([]), Forms());

        Assert.Equal(0f, catalog.WeightFor(StoneItem));
    }

    [Fact]
    public void HungerRestoredPerUnitForReturnsTheDefinitionsValue()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(Apple, "Apple", Stone, Lump, HungerRestoredPerUnit: 1f),
        ], Materials(), Forms());

        Assert.Equal(1f, catalog.HungerRestoredPerUnitFor(Apple));
    }

    [Fact]
    public void HungerRestoredPerUnitForReturnsZeroForAnItemWithNoDefinition()
    {
        var catalog = new ItemCatalog([], Materials(), Forms());

        Assert.Equal(0f, catalog.HungerRestoredPerUnitFor(Apple));
    }

    [Fact]
    public void CarryCapacityBonusForReturnsTheDefinitionsValue()
    {
        var basket = new ItemKindId("basket");
        var catalog = new ItemCatalog([
            new ItemDefinition(basket, "Basket", Stone, Lump, CarryCapacityBonus: 20f),
        ], Materials(), Forms());

        Assert.Equal(20f, catalog.CarryCapacityBonusFor(basket));
    }

    [Fact]
    public void CarryCapacityBonusForReturnsZeroForAnItemWithNoDefinition()
    {
        var catalog = new ItemCatalog([], Materials(), Forms());

        Assert.Equal(0f, catalog.CarryCapacityBonusFor(new ItemKindId("basket")));
    }

    [Fact]
    public void ChoppingScoreForIsTheFormsEdgeTimesTheMaterialsHardnessTimesTheRootOfItsWeight()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(Axe, "Axe", Stone, Wedge, Volume: 2f),
        ], Materials(), Forms());

        // Weight is 2 * 2 = 4, so the score is 1 * 0.8 * 2.
        Assert.Equal(1.6f, catalog.ChoppingScoreFor(Axe), 5);
    }

    // The point of keeping geometry apart from substance: the very same stone, unworked, is no
    // tool at all (see docs/materials-and-crafting-architecture.md section 1).
    [Fact]
    public void ARawLumpScoresNothingWhereAWedgeOfTheSameMaterialScores()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(Axe, "Axe", Stone, Wedge, Volume: 2f),
            new ItemDefinition(StoneItem, "Stone", Stone, Lump, Volume: 2f),
        ], Materials(), Forms());

        Assert.Equal(0f, catalog.ChoppingScoreFor(StoneItem));
        Assert.True(catalog.ChoppingScoreFor(Axe) > 0f);
    }

    // An edge is only as good as what it is cut into: a wedge of something soft bites nothing.
    [Fact]
    public void AWedgeOfASoftMaterialScoresNothing()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(Axe, "Axe", Hide, Wedge, Volume: 2f),
        ], Materials(), Forms());

        Assert.Equal(0f, catalog.ChoppingScoreFor(Axe));
    }

    [Fact]
    public void ChoppingScoreForReturnsZeroForAnUndescribedForm()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(Axe, "Axe", Stone, new FormId("unheard_of"), Volume: 2f),
        ], Materials(), Forms());

        Assert.Equal(0f, catalog.ChoppingScoreFor(Axe));
    }

    [Fact]
    public void ChoppingScoreForReturnsZeroForAnItemWithNoDefinition()
    {
        var catalog = new ItemCatalog([], Materials(), Forms());

        Assert.Equal(0f, catalog.ChoppingScoreFor(Axe));
    }

    [Fact]
    public void AHeavierWedgeOfTheSameMaterialChopsBetter()
    {
        var heavyAxe = new ItemKindId("heavy_axe");
        var catalog = new ItemCatalog([
            new ItemDefinition(Axe, "Axe", Stone, Wedge, Volume: 2f),
            new ItemDefinition(heavyAxe, "Heavy Axe", Stone, Wedge, Volume: 8f),
        ], Materials(), Forms());

        Assert.True(catalog.ChoppingScoreFor(heavyAxe) > catalog.ChoppingScoreFor(Axe));
    }

    [Fact]
    public void LoadFromDirectoryReadsOneDefinitionPerSubdirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-itemcatalog-{Guid.NewGuid():N}");
        WriteItem(root, "warm_clothing");

        try
        {
            var catalog = ItemCatalog.LoadFromDirectory(root, Materials(), Forms());

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
            var catalog = ItemCatalog.LoadFromDirectory(root, Materials(), Forms());

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
            var ex = Assert.Throws<InvalidDataException>(() => ItemCatalog.LoadFromDirectory(root, Materials(), Forms()));

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
