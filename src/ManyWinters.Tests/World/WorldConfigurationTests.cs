using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class WorldConfigurationTests
{
    [Fact]
    public void ParameterlessConfigurationHasNothingDefinedOnTheDefaultCalendar()
    {
        var configuration = new WorldConfiguration();

        Assert.Throws<KeyNotFoundException>(() => configuration.ResourceCatalog.Get(new ResourceKindId("apple")));
        Assert.Throws<KeyNotFoundException>(() => configuration.SkillCatalog.Get(new SkillTypeId("foraging")));
        Assert.Throws<KeyNotFoundException>(() => configuration.RecipeCatalog.Get(new ItemKindId("axe")));
        Assert.Throws<KeyNotFoundException>(() => configuration.BuildingCatalog.Get(new BuildingKindId("storage_hut")));
        Assert.Throws<KeyNotFoundException>(() => configuration.ItemCatalog.Get(new ItemKindId("axe")));
        Assert.Same(SeasonParameters.Default, configuration.SeasonParameters);
    }

    [Fact]
    public void LoadFromJsonAsksForEachCatalogFolderByNameAndWiresWhatComesBack()
    {
        var asked = new List<string>();
        var configuration = WorldConfiguration.LoadFromJson(catalog =>
        {
            asked.Add(catalog);
            return catalog switch
            {
                "resources" => [("apple.json", """{ "id": "apple", "displayName": "Apple", "skill": "foraging" }""")],
                "skills" => [("foraging.json", """{ "id": "foraging", "displayName": "Foraging", "baseTechnique": "basic_foraging", "efficientTechnique": "efficient_foraging" }""")],
                "recipes" => [("axe.json", """{ "output": "axe", "inputItem": "wood", "inputAmount": 5 }""")],
                "buildings" => [("storage_hut.json", """{ "id": "storage_hut", "displayName": "Storage Hut", "requiredItem": "wood", "requiredAmount": 20 }""")],
                "items" => [("axe.json", """{ "id": "axe", "displayName": "Axe", "weight": 5 }""")],
                _ => throw new InvalidOperationException($"Unexpected catalog folder '{catalog}'."),
            };
        });

        Assert.Equal(["resources", "skills", "recipes", "buildings", "items"], asked);
        Assert.Equal("Apple", configuration.ResourceCatalog.Get(new ResourceKindId("apple")).DisplayName);
        Assert.Equal("Foraging", configuration.SkillCatalog.Get(new SkillTypeId("foraging")).DisplayName);
        Assert.Equal(5, configuration.RecipeCatalog.Get(new ItemKindId("axe")).InputAmount);
        Assert.Equal(20, configuration.BuildingCatalog.Get(new BuildingKindId("storage_hut")).RequiredAmount);
        Assert.Equal(5f, configuration.ItemCatalog.Get(new ItemKindId("axe")).Weight);
        Assert.Same(SeasonParameters.Default, configuration.SeasonParameters);
    }

    [Fact]
    public void LoadFromDirectoryReadsEveryCatalogFromItsOwnFolderUnderTheContentRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-worldconfiguration-{Guid.NewGuid():N}");
        WriteDefinition(root, "resources", "apple", """{ "id": "apple", "displayName": "Apple", "skill": "foraging" }""");
        WriteDefinition(root, "skills", "foraging", """{ "id": "foraging", "displayName": "Foraging", "baseTechnique": "basic_foraging", "efficientTechnique": "efficient_foraging" }""");
        WriteDefinition(root, "recipes", "axe", """{ "output": "axe", "inputItem": "wood", "inputAmount": 5 }""");
        WriteDefinition(root, "buildings", "storage_hut", """{ "id": "storage_hut", "displayName": "Storage Hut", "requiredItem": "wood", "requiredAmount": 20 }""");
        WriteDefinition(root, "items", "axe", """{ "id": "axe", "displayName": "Axe", "weight": 5 }""");

        try
        {
            var configuration = WorldConfiguration.LoadFromDirectory(root);

            Assert.Equal(new SkillTypeId("foraging"), configuration.ResourceCatalog.Get(new ResourceKindId("apple")).Skill);
            Assert.Equal(new TechniqueId("efficient_foraging"), configuration.SkillCatalog.Get(new SkillTypeId("foraging")).EfficientTechnique);
            Assert.Equal(new ItemKindId("wood"), configuration.RecipeCatalog.Get(new ItemKindId("axe")).InputItem);
            Assert.Equal(new ItemKindId("wood"), configuration.BuildingCatalog.Get(new BuildingKindId("storage_hut")).RequiredItem);
            Assert.Equal("Axe", configuration.ItemCatalog.Get(new ItemKindId("axe")).DisplayName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteDefinition(string root, string catalog, string id, string json)
    {
        var directory = Path.Combine(root, catalog, id);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{id}.json"), json);
    }
}
