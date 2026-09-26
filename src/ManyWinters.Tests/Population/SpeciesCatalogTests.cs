using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;

namespace ManyWinters.Tests.Population;

public class SpeciesCatalogTests
{
    [Fact]
    public void GetReturnsTheDefinitionForAKnownId()
    {
        var lifeCycle = new LifeCycle(1, 4, 7, 10);
        var catalog = new SpeciesCatalog([
            new SpeciesDefinition(Person.HumanSpecies, "Human", lifeCycle),
        ]);

        var definition = catalog.Get(Person.HumanSpecies);

        Assert.Equal("Human", definition.DisplayName);
        Assert.Equal(lifeCycle, definition.LifeCycle);
    }

    [Fact]
    public void GetThrowsForAnUnknownId()
    {
        var catalog = new SpeciesCatalog([]);

        Assert.Throws<KeyNotFoundException>(() => catalog.Get(Person.HumanSpecies));
    }

    [Fact]
    public void LoadFromDirectoryReadsOneDefinitionPerSubdirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-speciescatalog-{Guid.NewGuid():N}");
        var humanDir = Path.Combine(root, "human");
        Directory.CreateDirectory(humanDir);
        File.WriteAllText(
            Path.Combine(humanDir, "human.json"),
            """{ "id": "human", "displayName": "Human", "lifeCycle": { "weaningAgeYears": 1, "adultAgeYears": 4, "elderAgeYears": 7, "maxLifespanYears": 10 } }""");

        try
        {
            var catalog = SpeciesCatalog.LoadFromDirectory(root);

            var definition = catalog.Get(new SpeciesId("human"));
            Assert.Equal("Human", definition.DisplayName);
            Assert.Equal(new LifeCycle(1, 4, 7, 10), definition.LifeCycle);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryReadsTheDiet()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-speciescatalog-{Guid.NewGuid():N}");
        var humanDir = Path.Combine(root, "human");
        Directory.CreateDirectory(humanDir);
        File.WriteAllText(
            Path.Combine(humanDir, "human.json"),
            """
            {
              "id": "human",
              "displayName": "Human",
              "lifeCycle": { "weaningAgeYears": 1, "adultAgeYears": 4, "elderAgeYears": 7, "maxLifespanYears": 10 },
              "diet": [
                { "material": "apple", "digestibility": 1 },
                { "material": "grass", "digestibility": 0.3 }
              ]
            }
            """);

        try
        {
            var catalog = SpeciesCatalog.LoadFromDirectory(root);

            var definition = catalog.Get(new SpeciesId("human"));
            Assert.Equal(1f, definition.DigestibilityOf(new MaterialId("apple")));
            Assert.Equal(0.3f, definition.DigestibilityOf(new MaterialId("grass")));
            Assert.Equal(0f, definition.DigestibilityOf(new MaterialId("stone")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryIgnoresNonJsonFilesInASpeciesFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-speciescatalog-{Guid.NewGuid():N}");
        var humanDir = Path.Combine(root, "human");
        Directory.CreateDirectory(humanDir);
        File.WriteAllText(
            Path.Combine(humanDir, "human.json"),
            """{ "id": "human", "displayName": "Human", "lifeCycle": { "weaningAgeYears": 1, "adultAgeYears": 4, "elderAgeYears": 7, "maxLifespanYears": 10 } }""");
        File.WriteAllText(Path.Combine(humanDir, "notes.txt"), "this is not json and would blow up if read as such");

        try
        {
            var catalog = SpeciesCatalog.LoadFromDirectory(root);

            var definition = catalog.Get(new SpeciesId("human"));
            Assert.Equal("Human", definition.DisplayName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadFromDirectoryThrowsInvalidDataExceptionForAMalformedDefinition()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-speciescatalog-{Guid.NewGuid():N}");
        var humanDir = Path.Combine(root, "human");
        Directory.CreateDirectory(humanDir);
        var filePath = Path.Combine(humanDir, "human.json");
        File.WriteAllText(filePath, "null");

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SpeciesCatalog.LoadFromDirectory(root));

            Assert.Contains(filePath, ex.Message, StringComparison.Ordinal);
            Assert.StartsWith("Species definition", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
