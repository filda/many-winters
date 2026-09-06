using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
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
        Assert.Null(configuration.MaterialCatalog.Find(new MaterialId("stone")));
        Assert.Same(SeasonParameters.Default, configuration.SeasonParameters);
        Assert.Same(SimulationRules.Default, configuration.Rules);
    }

    [Fact]
    public void OneRuleCanBeOverriddenWithoutRestatingTheOthers()
    {
        var configuration = new WorldConfiguration { Rules = new SimulationRules { MaxLifespanYears = 1 } };

        Assert.Equal(1, configuration.Rules.MaxLifespanYears);
        Assert.Equal(SimulationRules.Default.TicksPerSeason, configuration.Rules.TicksPerSeason);
        Assert.Equal(SimulationRules.Default.MaxInteractionDistance, configuration.Rules.MaxInteractionDistance);
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
                "materials" => [("stone.json", """{ "id": "stone", "displayName": "Stone", "density": 2 }""")],
                "items" => [("axe.json", """{ "id": "axe", "displayName": "Axe", "material": "stone", "form": "wedge", "volume": 2.5 }""")],
                _ => throw new InvalidOperationException($"Unexpected catalog folder '{catalog}'."),
            };
        });

        Assert.Equal(["materials", "resources", "skills", "recipes", "buildings", "items"], asked);
        Assert.Equal("Apple", configuration.ResourceCatalog.Get(new ResourceKindId("apple")).DisplayName);
        Assert.Equal("Foraging", configuration.SkillCatalog.Get(new SkillTypeId("foraging")).DisplayName);
        Assert.Equal(5, configuration.RecipeCatalog.Get(new ItemKindId("axe")).InputAmount);
        Assert.Equal(20, configuration.BuildingCatalog.Get(new BuildingKindId("storage_hut")).RequiredAmount);
        Assert.Equal(2f, configuration.MaterialCatalog.Find(new MaterialId("stone"))?.Density);
        // Derived, not stated: stone's density times the axe's volume is the weight the axe
        // file used to carry itself.
        Assert.Equal(5f, configuration.ItemCatalog.WeightFor(new ItemKindId("axe")));
        Assert.Same(SeasonParameters.Default, configuration.SeasonParameters);
        Assert.Same(SimulationRules.Default, configuration.Rules);
    }

    [Fact]
    public void LoadFromDirectoryReadsEveryCatalogFromItsOwnFolderUnderTheContentRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-worldconfiguration-{Guid.NewGuid():N}");
        WriteDefinition(root, "resources", "apple", """{ "id": "apple", "displayName": "Apple", "skill": "foraging" }""");
        WriteDefinition(root, "skills", "foraging", """{ "id": "foraging", "displayName": "Foraging", "baseTechnique": "basic_foraging", "efficientTechnique": "efficient_foraging" }""");
        WriteDefinition(root, "recipes", "axe", """{ "output": "axe", "inputItem": "wood", "inputAmount": 5 }""");
        WriteDefinition(root, "buildings", "storage_hut", """{ "id": "storage_hut", "displayName": "Storage Hut", "requiredItem": "wood", "requiredAmount": 20 }""");
        WriteDefinition(root, "materials", "stone", """{ "id": "stone", "displayName": "Stone", "density": 2 }""");
        WriteDefinition(root, "items", "axe", """{ "id": "axe", "displayName": "Axe", "material": "stone", "form": "wedge", "volume": 2.5 }""");

        try
        {
            var configuration = WorldConfiguration.LoadFromDirectory(root);

            Assert.Equal(new SkillTypeId("foraging"), configuration.ResourceCatalog.Get(new ResourceKindId("apple")).Skill);
            Assert.Equal(new TechniqueId("efficient_foraging"), configuration.SkillCatalog.Get(new SkillTypeId("foraging")).EfficientTechnique);
            Assert.Equal(new ItemKindId("wood"), configuration.RecipeCatalog.Get(new ItemKindId("axe")).InputItem);
            Assert.Equal(new ItemKindId("wood"), configuration.BuildingCatalog.Get(new BuildingKindId("storage_hut")).RequiredItem);
            Assert.Equal("Axe", configuration.ItemCatalog.Get(new ItemKindId("axe")).DisplayName);
            Assert.Equal("Stone", configuration.MaterialCatalog.Find(new MaterialId("stone"))?.DisplayName);
            Assert.Equal(5f, configuration.ItemCatalog.WeightFor(new ItemKindId("axe")));
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
