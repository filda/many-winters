using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

public class ResourceCatalogTests
{
    [Fact]
    public void GetReturnsTheDefinitionForAKnownId()
    {
        var catalog = new ResourceCatalog([
            new ResourceDefinition(TestCatalogs.Apple, "Apple", TestCatalogs.Foraging),
        ]);

        var definition = catalog.Get(TestCatalogs.Apple);

        Assert.Equal("Apple", definition.DisplayName);
        Assert.Equal(TestCatalogs.Foraging, definition.Skill);
    }

    [Fact]
    public void GetThrowsForAnUnknownId()
    {
        var catalog = new ResourceCatalog([]);

        Assert.Throws<KeyNotFoundException>(() => catalog.Get(TestCatalogs.Apple));
    }

    [Fact]
    public void LoadFromDirectoryReadsOneDefinitionPerSubdirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-resourcecatalog-{Guid.NewGuid():N}");
        var appleDir = Path.Combine(root, "apple");
        Directory.CreateDirectory(appleDir);
        File.WriteAllText(
            Path.Combine(appleDir, "apple.json"),
            """{ "id": "apple", "displayName": "Apple", "skill": "foraging" }""");

        try
        {
            var catalog = ResourceCatalog.LoadFromDirectory(root);

            var definition = catalog.Get(new ResourceKindId("apple"));
            Assert.Equal("Apple", definition.DisplayName);
            Assert.Equal(new SkillTypeId("foraging"), definition.Skill);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryIgnoresNonJsonFilesInAResourceFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-resourcecatalog-{Guid.NewGuid():N}");
        var appleDir = Path.Combine(root, "apple");
        Directory.CreateDirectory(appleDir);
        File.WriteAllText(
            Path.Combine(appleDir, "apple.json"),
            """{ "id": "apple", "displayName": "Apple", "skill": "foraging" }""");
        File.WriteAllText(Path.Combine(appleDir, "notes.txt"), "this is not json and would blow up if read as such");

        try
        {
            var catalog = ResourceCatalog.LoadFromDirectory(root);

            var definition = catalog.Get(new ResourceKindId("apple"));
            Assert.Equal("Apple", definition.DisplayName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryThrowsInvalidDataExceptionForAMalformedDefinition()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-resourcecatalog-{Guid.NewGuid():N}");
        var appleDir = Path.Combine(root, "apple");
        Directory.CreateDirectory(appleDir);
        var filePath = Path.Combine(appleDir, "apple.json");
        File.WriteAllText(filePath, "null");

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => ResourceCatalog.LoadFromDirectory(root));

            Assert.Contains(filePath, ex.Message, StringComparison.Ordinal);
            Assert.StartsWith("Resource definition", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
