using ManyWinters.Core.Persistence;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Persistence;

public class SaveGameServiceTests
{
    [Fact]
    public void RoundTripPreservesTickAndPeople()
    {
        var world = new WorldState();
        world.Clock.Advance(42);
        var ava = world.AddPerson("Ava", new Position(1.5f, 2.5f));
        ava.Needs.Hunger = 30;
        ava.Needs.Fatigue = 10;
        ava.Skills.Increase(TestCatalogs.Foraging, 3.5f);
        ava.KnownTechniques.Add(TestCatalogs.EfficientForaging);
        ava.Inventory.Add(TestCatalogs.WoodItem, 7);
        var bran = world.AddPerson("Bran", new Position(-3f, 0f));
        bran.IsAlive = false;
        bran.Needs.Hunger = 100;
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(4f, 5f), 42f);
        node.RemainingAmount = 10f;
        var building = world.AddBuilding(TestCatalogs.StorageHut, new Position(-1f, -2f));
        building.Condition = 63f;
        building.Inventory.Add(TestCatalogs.WoodItem, 12);

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path);

            Assert.Equal(world.Clock.CurrentTick, restored.Clock.CurrentTick);
            Assert.Equal(world.People.Count, restored.People.Count);

            var restoredAva = restored.People.Single(p => p.Name == "Ava");
            Assert.Equal(ava.Id, restoredAva.Id);
            Assert.Equal(ava.Position, restoredAva.Position);
            Assert.True(restoredAva.IsAlive);
            Assert.Equal(ava.Needs.Hunger, restoredAva.Needs.Hunger);
            Assert.Equal(ava.Needs.Fatigue, restoredAva.Needs.Fatigue);
            Assert.Equal(ava.Skills.Get(TestCatalogs.Foraging), restoredAva.Skills.Get(TestCatalogs.Foraging));
            Assert.Equal(ava.KnownTechniques, restoredAva.KnownTechniques);
            Assert.Equal(ava.Inventory.Get(TestCatalogs.WoodItem), restoredAva.Inventory.Get(TestCatalogs.WoodItem));

            var restoredBran = restored.People.Single(p => p.Name == "Bran");
            Assert.False(restoredBran.IsAlive);

            Assert.Equal(world.ResourceNodes.Count, restored.ResourceNodes.Count);
            var restoredNode = Assert.Single(restored.ResourceNodes);
            Assert.Equal(node.Id, restoredNode.Id);
            Assert.Equal(node.Kind, restoredNode.Kind);
            Assert.Equal(node.Position, restoredNode.Position);
            Assert.Equal(node.RemainingAmount, restoredNode.RemainingAmount);
            Assert.Equal(node.MaxAmount, restoredNode.MaxAmount);

            Assert.Equal(world.Buildings.Count, restored.Buildings.Count);
            var restoredBuilding = Assert.Single(restored.Buildings);
            Assert.Equal(building.Id, restoredBuilding.Id);
            Assert.Equal(building.Kind, restoredBuilding.Kind);
            Assert.Equal(building.Position, restoredBuilding.Position);
            Assert.Equal(building.Condition, restoredBuilding.Condition);
            Assert.Equal(building.Inventory.Get(TestCatalogs.WoodItem), restoredBuilding.Inventory.Get(TestCatalogs.WoodItem));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RestoredWorldContinuesIdSequenceWithoutCollisions()
    {
        var world = new WorldState();
        world.AddPerson("Ava", new Position(0, 0));
        world.AddPerson("Bran", new Position(0, 0));

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path);

            var newPerson = restored.AddPerson("Cora", new Position(0, 0));

            Assert.DoesNotContain(restored.People, p => p != newPerson && p.Id == newPerson.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RestoredWorldContinuesResourceNodeIdSequenceWithoutCollisions()
    {
        var world = new WorldState();
        world.AddResourceNode(TestCatalogs.Apple, new Position(0, 0), 10);
        world.AddResourceNode(TestCatalogs.Apple, new Position(1, 1), 10);

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path);

            var newNode = restored.AddResourceNode(TestCatalogs.Apple, new Position(2, 2), 10);

            Assert.DoesNotContain(restored.ResourceNodes, n => n != newNode && n.Id == newNode.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RestoredWorldContinuesBuildingIdSequenceWithoutCollisions()
    {
        var world = new WorldState();
        world.AddBuilding(TestCatalogs.StorageHut, new Position(0, 0));
        world.AddBuilding(TestCatalogs.StorageHut, new Position(1, 1));

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path);

            var newBuilding = restored.AddBuilding(TestCatalogs.StorageHut, new Position(2, 2));

            Assert.DoesNotContain(restored.Buildings, b => b != newBuilding && b.Id == newBuilding.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadWithConfigurationProvidedWiresItIntoTheRestoredWorld()
    {
        var world = new WorldState();
        world.AddPerson("Ava", new Position(0, 0));

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var configuration = TestCatalogs.CreateConfiguration();
            var restored = SaveGameService.Load(path, configuration);

            var definition = restored.ResourceCatalog.Get(TestCatalogs.Apple);
            Assert.Equal("Apple", definition.DisplayName);

            var recipe = restored.RecipeCatalog.Get(TestCatalogs.Axe);
            Assert.Equal(TestCatalogs.WoodItem, recipe.InputItem);

            var buildingDefinition = restored.BuildingCatalog.Get(TestCatalogs.StorageHut);
            Assert.Equal(TestCatalogs.WoodItem, buildingDefinition.RequiredItem);

            var itemDefinition = restored.ItemCatalog.Get(TestCatalogs.WarmClothing);
            Assert.Equal(TestCatalogs.WarmClothingInsulation, itemDefinition.Insulation);

            Assert.Same(configuration.SeasonParameters, restored.SeasonParameters);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadWithoutConfigurationFallsBackToEmptyDefaults()
    {
        var world = new WorldState();
        world.AddPerson("Ava", new Position(0, 0));

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path);

            Assert.Throws<KeyNotFoundException>(() => restored.ResourceCatalog.Get(TestCatalogs.Apple));
            Assert.Throws<KeyNotFoundException>(() => restored.RecipeCatalog.Get(TestCatalogs.Axe));
            Assert.Throws<KeyNotFoundException>(() => restored.BuildingCatalog.Get(TestCatalogs.StorageHut));
            Assert.Throws<KeyNotFoundException>(() => restored.ItemCatalog.Get(TestCatalogs.WarmClothing));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadThrowsInvalidDataExceptionForNullContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "null");

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SaveGameService.Load(path));

            Assert.Contains(path, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
