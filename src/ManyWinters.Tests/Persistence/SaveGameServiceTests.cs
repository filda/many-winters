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
        var bran = world.AddPerson("Bran", new Position(-3f, 0f));
        bran.IsAlive = false;
        bran.Needs.Hunger = 100;
        var node = world.AddResourceNode(TestCatalogs.Apple, new Position(4f, 5f), 42f);

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

            var restoredBran = restored.People.Single(p => p.Name == "Bran");
            Assert.False(restoredBran.IsAlive);

            Assert.Equal(world.ResourceNodes.Count, restored.ResourceNodes.Count);
            var restoredNode = Assert.Single(restored.ResourceNodes);
            Assert.Equal(node.Id, restoredNode.Id);
            Assert.Equal(node.Kind, restoredNode.Kind);
            Assert.Equal(node.Position, restoredNode.Position);
            Assert.Equal(node.RemainingAmount, restoredNode.RemainingAmount);
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
    public void LoadWithBothCatalogsProvidedWiresThemIntoTheRestoredWorld()
    {
        var world = new WorldState();
        world.AddPerson("Ava", new Position(0, 0));

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path, TestCatalogs.CreateResourceCatalog(), TestCatalogs.CreateSkillCatalog());

            var definition = restored.ResourceCatalog.Get(TestCatalogs.Apple);
            Assert.Equal("Apple", definition.DisplayName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadWithOnlyOneCatalogProvidedFallsBackToEmptyDefaultsForBoth()
    {
        var world = new WorldState();
        world.AddPerson("Ava", new Position(0, 0));

        var path = Path.Combine(Path.GetTempPath(), $"manywinters-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path, TestCatalogs.CreateResourceCatalog(), skillCatalog: null);

            Assert.Throws<KeyNotFoundException>(() => restored.ResourceCatalog.Get(TestCatalogs.Apple));
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
