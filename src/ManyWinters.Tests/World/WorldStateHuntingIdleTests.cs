using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// WorldState.DecideIdleTask's hunting/butchering branch: tried after the existing "seek food
// when hungry" node/pile search comes up empty, and only while hungry - hunting is not busywork
// for an idle, fed person, and a hungry butcher is sent to a carcass already on the ground
// before a hunter is sent after a live one.
public class WorldStateHuntingIdleTests
{
    private static HomeRange NewHome(Position anchor) => new(anchor) { Radius = 15f, DriftMetresPerSeason = 0f };

    // "Urgently hungry" in these tests is 60, above HungerSeekFoodThreshold (50) but under the
    // lowest MaxHunger anyone can draw (MaxHunger 100 minus MaxHungerVariation 20%): the id is a
    // fresh Guid every run, and at 80 about one run in forty starved the person on the first
    // tick, freezing whatever task they held.
    private static Person NewPerson(WorldState world, Position position, bool huntingKnown = false, bool butcheringKnown = false, float hunger = 0f)
    {
        var person = world.SpawnPerson("Ava", position, initialAgeTicks: TestCatalogs.AdultAgeTicks);
        // NeedsToSeekFoodUrgently gates on knowing how to eat at all (WorldState.KnowsHowToEat) -
        // without it, hunger alone never opens the "seek food" branch these tests are about.
        person.KnownTechniques.Add(TestCatalogs.BasicEating);
        if (huntingKnown)
        {
            person.KnownTechniques.Add(TestCatalogs.BasicHunting);
        }

        if (butcheringKnown)
        {
            person.KnownTechniques.Add(TestCatalogs.BasicButchering);
        }

        person.Needs.Hunger = hunger;
        return person;
    }

    [Fact]
    public void AHungryHunterWithAHerdInRangeIsSentToHunt()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(5, 0), NewHome(new Position(5, 0)));
        var hunter = NewPerson(world, position, huntingKnown: true, hunger: 60f);

        world.Advance(1);

        var task = Assert.IsType<HuntTask>(hunter.Tasks.Current);
        Assert.Same(deer, task.Prey);
    }

    // Not busywork: a fed person who happens to know hunting never bothers a herd.
    [Fact]
    public void AFedPersonWhoKnowsHuntingDoesNotHunt()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(5, 0), NewHome(new Position(5, 0)));
        var hunter = NewPerson(world, position, huntingKnown: true, hunger: 0f);

        world.Advance(5);

        Assert.IsNotType<HuntTask>(hunter.Tasks.Current);
    }

    // The two animal food steps trigger at HungerEatThreshold (25) rather than waiting for
    // HungerSeekFoodThreshold (50) - "would eat if they had something", not "must go find
    // something now". A hunt is a long trip, worth setting out on early.
    [Fact]
    public void AHunterHungryEnoughToEatButNotYetUrgentIsSentToHunt()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(5, 0), NewHome(new Position(5, 0)));
        var hunter = NewPerson(world, position, huntingKnown: true, hunger: 30f);

        world.Advance(1);

        var task = Assert.IsType<HuntTask>(hunter.Tasks.Current);
        Assert.Same(deer, task.Prey);
    }

    [Fact]
    public void AButcherHungryEnoughToEatButNotYetUrgentGoesToACarcass()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var carcass = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(3, 0), NewHome(new Position(3, 0)));
        carcass.IsAlive = false;
        carcass.Inventory.Add(TestCatalogs.MeatItem, TestCatalogs.DeerCarcassMeat);
        var person = NewPerson(world, position, butcheringKnown: true, hunger: 30f);

        world.Advance(1);

        var task = Assert.IsType<ButcherTask>(person.Tasks.Current);
        Assert.Same(carcass, task.Carcass);
    }

    // Below HungerEatThreshold: "would eat if they had something" does not hold at all yet, so
    // hunting still waits.
    [Fact]
    public void APersonBelowTheEatThresholdDoesNotHunt()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(5, 0), NewHome(new Position(5, 0)));
        var hunter = NewPerson(world, position, huntingKnown: true, hunger: 24f);

        world.Advance(1);

        Assert.IsNotType<HuntTask>(hunter.Tasks.Current);
    }

    // TryAutoEat, not a fresh hunt: someone hungry who already carries food eats it down rather
    // than being sent after more.
    [Fact]
    public void AHungryHunterWithMeatAlreadyInThePackEatsRatherThanHunting()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(5, 0), NewHome(new Position(5, 0)));
        var hunter = NewPerson(world, position, huntingKnown: true, hunger: 60f);
        hunter.Inventory.Add(TestCatalogs.MeatItem, 10);
        hunter.KnownTechniques.Add(TestCatalogs.BasicEating);

        world.Advance(1);

        Assert.IsNotType<HuntTask>(hunter.Tasks.Current);
        Assert.True(hunter.Needs.Hunger < 60f, "TryAutoEat should have eaten from the pack.");
    }

    // Butchering wins over hunting when both are known: a carcass already on the ground is a
    // meal without the risk of a miss.
    [Fact]
    public void AHungryButcherGoesToACarcassBeforeHuntingALivingDeer()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var carcass = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(3, 0), NewHome(new Position(3, 0)));
        carcass.IsAlive = false;
        carcass.Inventory.Add(TestCatalogs.MeatItem, TestCatalogs.DeerCarcassMeat);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(5, 0), NewHome(new Position(5, 0)));
        var person = NewPerson(world, position, huntingKnown: true, butcheringKnown: true, hunger: 60f);

        world.Advance(1);

        var task = Assert.IsType<ButcherTask>(person.Tasks.Current);
        Assert.Same(carcass, task.Carcass);
    }

    // A carcass with nothing left in it is not a meal - the hungry butcher falls through to
    // hunting instead.
    [Fact]
    public void APickedCleanCarcassIsSkippedInFavourOfHunting()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var pickedClean = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(3, 0), NewHome(new Position(3, 0)));
        pickedClean.IsAlive = false;
        var livingDeer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(5, 0), NewHome(new Position(5, 0)));
        var person = NewPerson(world, position, huntingKnown: true, butcheringKnown: true, hunger: 60f);

        world.Advance(1);

        var task = Assert.IsType<HuntTask>(person.Tasks.Current);
        Assert.Same(livingDeer, task.Prey);
    }

    // Neither known: falls through to plain wandering, same as anyone with no known skill at all.
    [Fact]
    public void AHungryPersonWithNeitherSkillDoesNotGetSentAnywhere()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(5, 0), NewHome(new Position(5, 0)));
        var person = NewPerson(world, position, hunger: 60f);

        world.Advance(1);

        Assert.IsType<IdleTask>(person.Tasks.Current);
    }

    // HuntTask is dropped the moment the prey dies to anyone, or wanders out of the search
    // radius, rather than the hunter walking on toward a corpse or off the map forever.
    [Fact]
    public void HuntingIsAbandonedOnceThePreyDies()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var deer = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(5, 0), NewHome(new Position(5, 0)));
        var hunter = NewPerson(world, position, huntingKnown: true, hunger: 60f);

        world.Advance(1);
        Assert.IsType<HuntTask>(hunter.Tasks.Current);

        deer.IsAlive = false;
        world.Advance(1);

        Assert.IsNotType<HuntTask>(hunter.Tasks.Current);
    }

    [Fact]
    public void ButcheringIsAbandonedOnceTheCarcassIsPickedClean()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var carcass = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(0.5, 0), NewHome(new Position(0.5, 0)));
        carcass.IsAlive = false;
        carcass.Inventory.Add(TestCatalogs.MeatItem, 1);
        var butcher = NewPerson(world, position, butcheringKnown: true, hunger: 60f);

        world.Advance(1);
        Assert.IsType<ButcherTask>(butcher.Tasks.Current);

        // One tick within reach takes the single unit of meat off the carcass.
        world.Advance(1);

        Assert.Empty(carcass.Inventory.Counts);
        Assert.IsNotType<ButcherTask>(butcher.Tasks.Current);
    }

    // A decayed carcass holds only bone, and bone is never a meal - nobody idle should be sent
    // to one.
    [Fact]
    public void AHungryPersonIsNeverSentToButcherAMeatlessCarcass()
    {
        var world = TestCatalogs.CreateWorldWithDeer();
        var position = new Position(0, 0);
        var carcass = world.SpawnAnimal(TestCatalogs.DeerSpeciesId, new Position(3, 0), NewHome(new Position(3, 0)));
        carcass.IsAlive = false;
        carcass.Inventory.Add(TestCatalogs.BoneItem, TestCatalogs.DeerCarcassBone);
        var person = NewPerson(world, position, butcheringKnown: true, hunger: 60f);

        world.Advance(1);

        Assert.IsNotType<ButcherTask>(person.Tasks.Current);
    }
}
