using ManyWinters.Core.Knowledge;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Knowledge;

public class SkillCatalogTests
{
    [Fact]
    public void GetReturnsTheDefinitionForAKnownId()
    {
        var catalog = new SkillCatalog([
            new SkillDefinition(TestCatalogs.Foraging, "Foraging", TestCatalogs.BasicForaging, TestCatalogs.EfficientForaging),
        ]);

        var definition = catalog.Get(TestCatalogs.Foraging);

        Assert.Equal("Foraging", definition.DisplayName);
        Assert.Equal(TestCatalogs.EfficientForaging, definition.EfficientTechnique);
    }

    [Fact]
    public void GetThrowsForAnUnknownId()
    {
        var catalog = new SkillCatalog([]);

        Assert.Throws<KeyNotFoundException>(() => catalog.Get(TestCatalogs.Foraging));
    }

    [Fact]
    public void LoadFromDirectoryReadsOneDefinitionPerSubdirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-skillcatalog-{Guid.NewGuid():N}");
        var foragingDir = Path.Combine(root, "foraging");
        Directory.CreateDirectory(foragingDir);
        File.WriteAllText(
            Path.Combine(foragingDir, "foraging.json"),
            """{ "id": "foraging", "displayName": "Foraging", "baseTechnique": "basic_foraging", "efficientTechnique": "efficient_foraging" }""");

        try
        {
            var catalog = SkillCatalog.LoadFromDirectory(root);

            var definition = catalog.Get(new SkillTypeId("foraging"));
            Assert.Equal("Foraging", definition.DisplayName);
            Assert.Equal(new TechniqueId("efficient_foraging"), definition.EfficientTechnique);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryIgnoresNonJsonFilesInASkillFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-skillcatalog-{Guid.NewGuid():N}");
        var foragingDir = Path.Combine(root, "foraging");
        Directory.CreateDirectory(foragingDir);
        File.WriteAllText(
            Path.Combine(foragingDir, "foraging.json"),
            """{ "id": "foraging", "displayName": "Foraging", "baseTechnique": "basic_foraging", "efficientTechnique": "efficient_foraging" }""");
        File.WriteAllText(Path.Combine(foragingDir, "notes.txt"), "this is not json and would blow up if read as such");

        try
        {
            var catalog = SkillCatalog.LoadFromDirectory(root);

            var definition = catalog.Get(new SkillTypeId("foraging"));
            Assert.Equal("Foraging", definition.DisplayName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryThrowsInvalidDataExceptionForAMalformedDefinition()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-skillcatalog-{Guid.NewGuid():N}");
        var foragingDir = Path.Combine(root, "foraging");
        Directory.CreateDirectory(foragingDir);
        var filePath = Path.Combine(foragingDir, "foraging.json");
        File.WriteAllText(filePath, "null");

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SkillCatalog.LoadFromDirectory(root));

            Assert.Contains(filePath, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
