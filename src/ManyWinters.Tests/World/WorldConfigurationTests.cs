using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Maps;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.World;

public class WorldConfigurationTests
{
    [Fact]
    public void ParameterlessConfigurationHasNothingDefinedOnTheDefaultCalendar()
    {
        var configuration = new WorldConfiguration();

        Assert.Throws<KeyNotFoundException>(() => configuration.SpeciesCatalog.Get(new SpeciesId("human")));
        Assert.Throws<KeyNotFoundException>(() => configuration.ResourceCatalog.Get(new EntityKindId("apple")));
        Assert.Throws<KeyNotFoundException>(() => configuration.SkillCatalog.Get(new SkillTypeId("foraging")));
        Assert.Throws<KeyNotFoundException>(() => configuration.RecipeCatalog.Get(new ItemKindId("axe")));
        Assert.Throws<KeyNotFoundException>(() => configuration.ItemCatalog.Get(new ItemKindId("axe")));
        Assert.Null(configuration.MaterialCatalog.Find(new MaterialId("stone")));
        Assert.Null(configuration.FormCatalog.Find(new FormId("wedge")));
        Assert.Same(SeasonParameters.Default, configuration.SeasonParameters);
        Assert.Same(SimulationRules.Default, configuration.Rules);
    }

    [Fact]
    public void OneRuleCanBeOverriddenWithoutRestatingTheOthers()
    {
        var configuration = new WorldConfiguration { Rules = new SimulationRules { TicksPerSeason = 1 } };

        Assert.Equal(1, configuration.Rules.TicksPerSeason);
        Assert.Equal(SimulationRules.Default.MaxHunger, configuration.Rules.MaxHunger);
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
                "species" => [("human.json", """{ "id": "human", "displayName": "Human", "lifeCycle": { "weaningAgeYears": 1, "adultAgeYears": 4, "elderAgeYears": 7, "maxLifespanYears": 10 }, "collisionRadius": 0.35, "hungerPerTickMultiplier": 1 }""")],
                "resources" => [("apple.json", """{ "id": "apple", "displayName": "Apple", "skill": "foraging" }""")],
                "skills" => [("foraging.json", """{ "id": "foraging", "displayName": "Foraging", "baseTechnique": "basic_foraging", "efficientTechnique": "efficient_foraging" }""")],
                "recipes" => [("axe.json", """{ "output": "axe", "inputItem": "wood", "inputAmount": 5 }""")],
                "materials" => [("stone.json", """{ "id": "stone", "displayName": "Stone", "density": 2 }""")],
                "forms" => [("wedge.json", """{ "id": "wedge", "displayName": "Wedge", "edgeSharpness": 1 }""")],
                "terrain" => [("praha/features.json", """{ "rockAreas": [ { "rings": [ [[0, 0], [10, 0], [0, 7], [0, 0]] ] } ] }""")],
                "items" => [("axe.json", """{ "id": "axe", "displayName": "Axe", "material": "stone", "form": "wedge", "volume": 2.5 }""")],
                _ => throw new InvalidOperationException($"Unexpected catalog folder '{catalog}'."),
            };
        });

        Assert.Equal(["species", "materials", "forms", "resources", "skills", "recipes", "items", "terrain"], asked);
        Assert.Equal("Human", configuration.SpeciesCatalog.Get(new SpeciesId("human")).DisplayName);
        Assert.Equal(10, configuration.SpeciesCatalog.Get(new SpeciesId("human")).LifeCycle.MaxLifespanYears);
        Assert.Equal("Apple", configuration.ResourceCatalog.Get(new EntityKindId("apple")).DisplayName);
        Assert.Equal("Foraging", configuration.SkillCatalog.Get(new SkillTypeId("foraging")).DisplayName);
        Assert.Equal(5, configuration.RecipeCatalog.Get(new ItemKindId("axe")).InputAmount);
        Assert.Equal(2f, configuration.MaterialCatalog.Find(new MaterialId("stone"))?.Density);
        Assert.Equal(1f, configuration.FormCatalog.Find(new FormId("wedge"))?.EdgeSharpness);
        // Derived, not stated: stone's density times the axe's volume.
        Assert.Equal(5f, configuration.ItemCatalog.WeightFor(new ItemKindId("axe")));
        Assert.Same(SeasonParameters.Default, configuration.SeasonParameters);
        Assert.Same(SimulationRules.Default, configuration.Rules);
        Assert.True(configuration.Terrain.IsRock(new Position(2.5, 1.5)));
        Assert.False(configuration.Terrain.IsRock(new Position(8, 6)));
    }

    [Fact]
    public void ANewConfigurationHasNoTerrainFeatures()
    {
        Assert.Same(TerrainFeatures.None, new WorldConfiguration().Terrain);
    }

    [Fact]
    public void LoadFromDirectoryReadsEveryCatalogFromItsOwnFolderUnderTheContentRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"manywinters-worldconfiguration-{Guid.NewGuid():N}");
        WriteDefinition(root, "species", "human", """{ "id": "human", "displayName": "Human", "lifeCycle": { "weaningAgeYears": 1, "adultAgeYears": 4, "elderAgeYears": 7, "maxLifespanYears": 10 }, "collisionRadius": 0.35, "hungerPerTickMultiplier": 1 }""");
        WriteDefinition(root, "resources", "apple", """{ "id": "apple", "displayName": "Apple", "skill": "foraging" }""");
        WriteDefinition(root, "skills", "foraging", """{ "id": "foraging", "displayName": "Foraging", "baseTechnique": "basic_foraging", "efficientTechnique": "efficient_foraging" }""");
        WriteDefinition(root, "recipes", "axe", """{ "output": "axe", "inputItem": "wood", "inputAmount": 5 }""");
        WriteDefinition(root, "materials", "stone", """{ "id": "stone", "displayName": "Stone", "density": 2 }""");
        WriteDefinition(root, "forms", "wedge", """{ "id": "wedge", "displayName": "Wedge", "edgeSharpness": 1 }""");
        WriteDefinition(root, "terrain", "features", """{ "waterAreas": [ { "rings": [ [[0, 0], [10, 0], [0, 7], [0, 0]] ] } ] }""");
        WriteDefinition(root, "items", "axe", """{ "id": "axe", "displayName": "Axe", "material": "stone", "form": "wedge", "volume": 2.5 }""");

        try
        {
            var configuration = WorldConfiguration.LoadFromDirectory(root);

            Assert.Equal(4, configuration.SpeciesCatalog.Get(new SpeciesId("human")).LifeCycle.AdultAgeYears);
            Assert.Equal(new SkillTypeId("foraging"), configuration.ResourceCatalog.Get(new EntityKindId("apple")).Skill);
            Assert.Equal(new TechniqueId("efficient_foraging"), configuration.SkillCatalog.Get(new SkillTypeId("foraging")).EfficientTechnique);
            Assert.Equal(new ItemKindId("wood"), configuration.RecipeCatalog.Get(new ItemKindId("axe")).InputItem);
            Assert.Equal("Axe", configuration.ItemCatalog.Get(new ItemKindId("axe")).DisplayName);
            Assert.Equal("Stone", configuration.MaterialCatalog.Find(new MaterialId("stone"))?.DisplayName);
            Assert.Equal("Wedge", configuration.FormCatalog.Find(new FormId("wedge"))?.DisplayName);
            Assert.Equal(5f, configuration.ItemCatalog.WeightFor(new ItemKindId("axe")));
            Assert.True(configuration.Terrain.IsWater(new Position(2.5, 1.5)));
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
