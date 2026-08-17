using OfFolk.Core.Persistence;
using OfFolk.Core.World;

namespace OfFolk.Tests.Persistence;

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
        world.AddPerson("Bran", new Position(-3f, 0f));

        var path = Path.Combine(Path.GetTempPath(), $"offolk-savetest-{Guid.NewGuid():N}.json");
        try
        {
            SaveGameService.Save(world, path);
            var restored = SaveGameService.Load(path);

            Assert.Equal(world.Clock.CurrentTick, restored.Clock.CurrentTick);
            Assert.Equal(world.People.Count, restored.People.Count);

            var restoredAva = restored.People.Single(p => p.Name == "Ava");
            Assert.Equal(ava.Id, restoredAva.Id);
            Assert.Equal(ava.Position, restoredAva.Position);
            Assert.Equal(ava.Needs.Hunger, restoredAva.Needs.Hunger);
            Assert.Equal(ava.Needs.Fatigue, restoredAva.Needs.Fatigue);
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

        var path = Path.Combine(Path.GetTempPath(), $"offolk-savetest-{Guid.NewGuid():N}.json");
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
}
