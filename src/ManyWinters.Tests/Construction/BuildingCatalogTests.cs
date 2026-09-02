using ManyWinters.Core.Construction;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Construction;

public class BuildingCatalogTests
{
    [Fact]
    public void GetReturnsTheDefinitionForAKnownId()
    {
        var catalog = new BuildingCatalog([
            new BuildingDefinition(TestCatalogs.StorageHut, "Storage Hut", TestCatalogs.WoodItem, 20),
        ]);

        var definition = catalog.Get(TestCatalogs.StorageHut);

        Assert.Equal("Storage Hut", definition.DisplayName);
        Assert.Equal(TestCatalogs.WoodItem, definition.RequiredItem);
        Assert.Equal(20, definition.RequiredAmount);
    }

    [Fact]
    public void GetThrowsForAnUnknownId()
    {
        var catalog = new BuildingCatalog([]);

        Assert.Throws<KeyNotFoundException>(() => catalog.Get(TestCatalogs.StorageHut));
    }

    [Fact]
    public void LoadFromDirectoryReadsOneDefinitionPerSubdirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-buildingcatalog-{Guid.NewGuid():N}");
        var hutDir = Path.Combine(root, "storage_hut");
        Directory.CreateDirectory(hutDir);
        File.WriteAllText(
            Path.Combine(hutDir, "storage_hut.json"),
            """{ "id": "storage_hut", "displayName": "Storage Hut", "requiredItem": "wood", "requiredAmount": 20 }""");

        try
        {
            var catalog = BuildingCatalog.LoadFromDirectory(root);

            var definition = catalog.Get(new BuildingKindId("storage_hut"));
            Assert.Equal("Storage Hut", definition.DisplayName);
            Assert.Equal(20, definition.RequiredAmount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryIgnoresNonJsonFilesInABuildingFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-buildingcatalog-{Guid.NewGuid():N}");
        var hutDir = Path.Combine(root, "storage_hut");
        Directory.CreateDirectory(hutDir);
        File.WriteAllText(
            Path.Combine(hutDir, "storage_hut.json"),
            """{ "id": "storage_hut", "displayName": "Storage Hut", "requiredItem": "wood", "requiredAmount": 20 }""");
        File.WriteAllText(Path.Combine(hutDir, "notes.txt"), "this is not json and would blow up if read as such");

        try
        {
            var catalog = BuildingCatalog.LoadFromDirectory(root);

            var definition = catalog.Get(new BuildingKindId("storage_hut"));
            Assert.Equal(20, definition.RequiredAmount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryThrowsInvalidDataExceptionForAMalformedDefinition()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-buildingcatalog-{Guid.NewGuid():N}");
        var hutDir = Path.Combine(root, "storage_hut");
        Directory.CreateDirectory(hutDir);
        var filePath = Path.Combine(hutDir, "storage_hut.json");
        File.WriteAllText(filePath, "null");

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => BuildingCatalog.LoadFromDirectory(root));

            Assert.Contains(filePath, ex.Message, StringComparison.Ordinal);

            // Every catalog loads the same way through the same parser, so the message has to
            // say which kind of content it was reading, not just which file.
            Assert.StartsWith("Building definition", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
