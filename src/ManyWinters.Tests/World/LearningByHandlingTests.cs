using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// Nobody is told that grass is fibrous; they carry it about and come to know. A person's
// understanding of the world is the sum of what they have had in their hands.
public class LearningByHandlingTests
{
    private static readonly MaterialId PlantFibre = new("plant_fibre");
    private static readonly MaterialId StoneMaterial = new("stone");

    private static Person Carrier(WorldState world)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.Tasks.Interrupt(new IdleTask());

        return person;
    }

    // A season of carrying it about, which is what the shipped rate is calibrated to.
    private static long ASeason(WorldState world) => world.Configuration.Rules.TicksPerSeason;

    [Fact]
    public void CarryingSomethingAboutTeachesWhatItIsLike()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Carrier(world);
        person.Inventory.Add(TestCatalogs.GrassItem, 5);

        world.Advance(ASeason(world));

        Assert.True(person.Beliefs.HoldsAnythingAbout(PlantFibre));
    }

    [Fact]
    public void WhatIsLearnedIsWhatTheSubstanceIsActuallyLike()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Carrier(world);
        person.Inventory.Add(TestCatalogs.GrassItem, 5);
        var actual = world.Configuration.MaterialCatalog.Find(PlantFibre)!;

        world.Advance(ASeason(world));

        Assert.Equal(actual.Fibrousness, person.Beliefs.AsBelieved(actual).Fibrousness, 5);
        Assert.Equal(actual.Flexibility, person.Beliefs.AsBelieved(actual).Flexibility, 5);
    }

    [Fact]
    public void AnEmptyHandedPersonComesToKnowNothing()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Carrier(world);

        world.Advance(ASeason(world) * 4);

        Assert.Empty(person.Beliefs.Held);
    }

    // Understanding takes handling, not a moment: one tick with a thing is an inkling.
    [Fact]
    public void AMomentWithSomethingIsNotYetUnderstanding()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Carrier(world);
        person.Inventory.Add(TestCatalogs.GrassItem, 5);

        world.Advance(1);

        Assert.False(person.Beliefs.HoldsAnythingAbout(PlantFibre));
    }

    [Fact]
    public void WhatTheyHaveNotHandledTheyDoNotKnow()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Carrier(world);
        person.Inventory.Add(TestCatalogs.GrassItem, 5);

        world.Advance(ASeason(world));

        Assert.True(person.Beliefs.HoldsAnythingAbout(PlantFibre));
        Assert.False(person.Beliefs.HoldsAnythingAbout(StoneMaterial));
    }

    // Every substance in a made thing, however deep: carrying a bound object about is having
    // your hands on everything it is made of.
    [Fact]
    public void CarryingAMadeThingTeachesEverySubstanceInIt()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Carrier(world);
        var cord = new Assembly.Part(PlantFibre, TestCatalogs.Cord, Quality: 0.5f, Volume: 15f);
        var head = new Assembly.Part(StoneMaterial, TestCatalogs.Wedge, Quality: 1f, Volume: 1f);
        person.Inventory.AddAssembly(new Assembly.Joined(0.8f, 3f, head, cord));

        world.Advance(ASeason(world));

        Assert.True(person.Beliefs.HoldsAnythingAbout(PlantFibre));
        Assert.True(person.Beliefs.HoldsAnythingAbout(StoneMaterial));
    }

    [Fact]
    public void TheDeadComeToKnowNothingMore()
    {
        var world = TestCatalogs.CreateWorld();
        var person = Carrier(world);
        person.Inventory.Add(TestCatalogs.GrassItem, 5);
        person.IsAlive = false;

        world.Advance(ASeason(world) * 2);

        Assert.Empty(person.Beliefs.Held);
    }

    // Idle hands turn over the familiar: a substance nobody has come to know is not one they
    // will idly think to work, however much of it they are holding. Directing is what reaches
    // past that (docs/materials-and-crafting-architecture.md section 7).
    [Fact]
    public void NobodyIdlyWorksASubstanceTheyDoNotYetUnderstand()
    {
        var world = new WorldState(TestCatalogs.CreateConfiguration() with
        {
            Rules = SimulationRules.Default with
            {
                IdleDiscoveryChancePerTick = 1f,
                // Never firm enough to act on, so the grass stays unfamiliar however long it is
                // carried - the point being that discovery waits on understanding.
                MaterialUnderstandingPerTick = 0f,
            },
        });
        var person = Carrier(world);
        person.Inventory.Add(TestCatalogs.GrassItem, 20);

        world.Advance(20);

        Assert.DoesNotContain(TestCatalogs.BasicTwisting, person.KnownTechniques);
    }
}
