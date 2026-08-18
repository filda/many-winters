using ManyWinters.Core.Persistence;
using ManyWinters.Core.World;

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
        var bran = world.AddPerson("Bran", new Position(-3f, 0f));
        bran.IsAlive = false;
        bran.Needs.Hunger = 100;

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

            var restoredBran = restored.People.Single(p => p.Name == "Bran");
            Assert.False(restoredBran.IsAlive);
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
