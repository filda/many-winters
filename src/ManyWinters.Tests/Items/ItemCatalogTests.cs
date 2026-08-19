using ManyWinters.Core.Items;

namespace ManyWinters.Tests.Items;

public class ItemCatalogTests
{
    [Fact]
    public void GetReturnsTheDefinitionForAKnownId()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(new ItemKindId("warm_clothing"), "Warm Clothing", 1f),
        ]);

        var definition = catalog.Get(new ItemKindId("warm_clothing"));

        Assert.Equal("Warm Clothing", definition.DisplayName);
        Assert.Equal(1f, definition.Insulation);
    }

    [Fact]
    public void GetThrowsForAnUnknownId()
    {
        var catalog = new ItemCatalog([]);

        Assert.Throws<KeyNotFoundException>(() => catalog.Get(new ItemKindId("warm_clothing")));
    }

    [Fact]
    public void InsulationForReturnsTheDefinitionsInsulation()
    {
        var catalog = new ItemCatalog([
            new ItemDefinition(new ItemKindId("warm_clothing"), "Warm Clothing", 1f),
        ]);

        Assert.Equal(1f, catalog.InsulationFor(new ItemKindId("warm_clothing")));
    }

    [Fact]
    public void InsulationForReturnsZeroForAnItemWithNoDefinition()
    {
        var catalog = new ItemCatalog([]);

        Assert.Equal(0f, catalog.InsulationFor(new ItemKindId("wood")));
    }

    [Fact]
    public void LoadFromDirectoryReadsOneDefinitionPerSubdirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-itemcatalog-{Guid.NewGuid():N}");
        var itemDir = Path.Combine(root, "warm_clothing");
        Directory.CreateDirectory(itemDir);
        File.WriteAllText(
            Path.Combine(itemDir, "warm_clothing.json"),
            """{ "id": "warm_clothing", "displayName": "Warm Clothing", "insulation": 1 }""");

        try
        {
            var catalog = ItemCatalog.LoadFromDirectory(root);

            var definition = catalog.Get(new ItemKindId("warm_clothing"));
            Assert.Equal("Warm Clothing", definition.DisplayName);
            Assert.Equal(1f, definition.Insulation);
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
        var itemDir = Path.Combine(root, "warm_clothing");
        Directory.CreateDirectory(itemDir);
        File.WriteAllText(
            Path.Combine(itemDir, "warm_clothing.json"),
            """{ "id": "warm_clothing", "displayName": "Warm Clothing", "insulation": 1 }""");
        File.WriteAllText(Path.Combine(itemDir, "notes.txt"), "this is not json and would blow up if read as such");

        try
        {
            var catalog = ItemCatalog.LoadFromDirectory(root);

            var definition = catalog.Get(new ItemKindId("warm_clothing"));
            Assert.Equal(1f, definition.Insulation);
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
            var ex = Assert.Throws<InvalidDataException>(() => ItemCatalog.LoadFromDirectory(root));

            Assert.Contains(filePath, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
