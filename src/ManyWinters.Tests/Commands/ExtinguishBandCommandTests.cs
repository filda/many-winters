using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

public class ExtinguishBandCommandTests
{
    [Fact]
    public void ExecuteKillsEveryLivingPerson()
    {
        var world = TestCatalogs.CreateWorld();
        world.SpawnPerson("Ava", new Position(0, 0));
        world.SpawnPerson("Bran", new Position(1, 0));
        world.SpawnPerson("Sela", new Position(2, 0));

        world.Execute(new ExtinguishBandCommand());

        Assert.All(world.People, person => Assert.False(person.IsAlive));
    }

    [Fact]
    public void ExecuteRecordsTheCurrentTickAsEveryDeathTick()
    {
        var world = TestCatalogs.CreateWorld();
        world.Clock.Advance(500);
        world.SpawnPerson("Ava", new Position(0, 0));
        world.SpawnPerson("Bran", new Position(1, 0));

        world.Execute(new ExtinguishBandCommand());

        Assert.All(world.People, person => Assert.Equal(500, person.DeathTick));
    }

    // Hunger, because that is what the epitaph reads for any death that was not of old age
    // (Epitaph.Died) - the extinction tells the same story a starved band would.
    [Fact]
    public void ExecuteRecordsHungerAsTheCauseOfDeath()
    {
        var world = TestCatalogs.CreateWorld();
        var ava = world.SpawnPerson("Ava", new Position(0, 0));

        world.Execute(new ExtinguishBandCommand());

        Assert.Equal(DeathCause.Hunger, ava.CauseOfDeath);
    }

    // The point of writing the deaths directly rather than maxing out hunger: TryAutoEat would
    // feed a carrier back below the threshold every tick, and the band would never die out.
    [Fact]
    public void ExecuteKillsEvenSomeoneWhoCouldFeedThemselves()
    {
        var world = TestCatalogs.CreateWorld();
        var carrier = world.SpawnPerson("Ava", new Position(0, 0));
        carrier.KnownTechniques.Add(TestCatalogs.BasicEating);
        carrier.Inventory.Add(TestCatalogs.AppleItem, 10);

        world.Execute(new ExtinguishBandCommand());

        // The tick that would have auto-fed a merely starving person back below the threshold
        // (WorldState.TryAutoEat) changes nothing here.
        world.Advance(1);

        Assert.False(carrier.IsAlive);
    }

    [Fact]
    public void ExecuteLeavesTheAlreadyDeadAlone()
    {
        var world = TestCatalogs.CreateWorld();
        var deceased = world.SpawnPerson("Odo", new Position(0, 0));
        deceased.IsAlive = false;
        deceased.DeathTick = 100;
        deceased.CauseOfDeath = DeathCause.OldAge;
        world.Clock.Advance(500);
        world.SpawnPerson("Ava", new Position(1, 0));

        world.Execute(new ExtinguishBandCommand());

        Assert.Equal(100, deceased.DeathTick);
        Assert.Equal(DeathCause.OldAge, deceased.CauseOfDeath);
    }

    // A debug lever, not a player action: there is no state in which it refuses.
    [Fact]
    public void NothingEverBlocksExtinguishingTheBand()
    {
        var world = TestCatalogs.CreateWorld();

        Assert.Equal(ActionBlocker.None, new ExtinguishBandCommand().Blocker(world));
    }
}
