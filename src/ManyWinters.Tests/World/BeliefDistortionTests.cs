using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// Knowledge that survives but arrives wrong (see docs/knowledge-transmission-architecture.md).
// Nothing marks a belief as mistaken and nobody holding one can tell; two people simply come to
// disagree, and reality settles it when somebody next works the stuff.
public class BeliefDistortionTests
{
    private static readonly MaterialId PlantFibre = new("plant_fibre");
    private const MaterialProperty Fibrousness = MaterialProperty.Fibrousness;

    // Everyone talks, nobody learns by carrying: what changes hands here changed hands by word.
    private static WorldState TalkativeWorld(float distortion = 0.15f) =>
        new(TestCatalogs.CreateConfiguration() with
        {
            Rules = SimulationRules.Default with
            {
                BeliefSharingChancePerTick = 1f,
                MaterialUnderstandingPerTick = 0f,
                IdleDiscoveryChancePerTick = 0f,
                HearsayDistortion = distortion,
            },
        });

    // Pinned ids, because every roll here runs on them (see WorldState.Distorted): with random
    // ones these would re-roll the dice on every run and pass or fail by luck.
    private static Person Somebody(WorldState world, int seed, Position? position = null) =>
        world.SpawnPerson(TestIds.Person(seed), $"Person{seed}", position ?? new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

    private static void Holds(Person person, float value) =>
        person.Beliefs.Learn(PlantFibre, Fibrousness, value, confidenceGained: 1f);

    private static float Heard(Person person) =>
        person.Beliefs.Held[(PlantFibre, Fibrousness)].Value;

    [Fact]
    public void WhatArrivesIsNotQuiteWhatWasSaid()
    {
        var world = TalkativeWorld();
        var teller = Somebody(world, 1);
        var listener = Somebody(world, 2);
        Holds(teller, 0.9f);

        world.Advance(1);

        Assert.NotEqual(0.9f, Heard(listener));
    }

    // It copies, it does not move: the teller still knows what they knew.
    [Fact]
    public void TellingSomebodyDoesNotDisturbWhatTheTellerBelieves()
    {
        var world = TalkativeWorld();
        var teller = Somebody(world, 1);
        var listener = Somebody(world, 2);
        Holds(teller, 0.9f);

        world.Advance(1);

        Assert.Equal(0.9f, Heard(teller), 5);
        Assert.NotEqual(0.9f, Heard(listener));
    }

    // Nobody tells it better than they know it, so error is laid on error and a chain drifts
    // further than a single telling.
    //
    // Measured across many chains rather than one, because a hop is a step of a random walk:
    // any given second telling may happen to land back nearer the truth, and asserting on one
    // chain would be asserting on a coin toss. What the mechanic promises is that error
    // accumulates, and that is a statement about the average.
    [Fact]
    public void ErrorAccumulatesAlongAChainOfTellings()
    {
        var afterOneHop = 0f;
        var afterTwoHops = 0f;
        const int chains = 40;

        for (var chain = 0; chain < chains; chain++)
        {
            var world = TalkativeWorld();
            var first = Somebody(world, (chain * 3) + 1);
            var second = Somebody(world, (chain * 3) + 2, new Position(100, 0));
            var third = Somebody(world, (chain * 3) + 3, new Position(200, 0));
            Holds(first, 0.9f);

            // Out of each other's earshot, so the tale travels one hop at a time rather than
            // everybody hearing it from the source.
            second.Position = first.Position;
            world.Advance(1);
            second.Position = third.Position;
            world.Advance(1);

            afterOneHop += Math.Abs(Heard(second) - 0.9f);
            afterTwoHops += Math.Abs(Heard(third) - 0.9f);
        }

        Assert.True(afterOneHop > 0f);
        Assert.True(
            afterTwoHops > afterOneHop,
            $"over {chains} chains, one hop drifted {afterOneHop / chains} on average and two hops {afterTwoHops / chains}");
    }

    // The lever the player has over drift: somebody who knows how to teach passes it on as they
    // hold it.
    [Fact]
    public void APractisedTeacherPassesItOnIntact()
    {
        var world = TalkativeWorld();
        var teller = Somebody(world, 1);
        var listener = Somebody(world, 2);
        Holds(teller, 0.9f);
        for (var i = 0; i < 50; i++)
        {
            teller.Skills.Increase(TeachCommand.TeachingSkill, 1f);
        }

        world.Advance(1);

        Assert.Equal(0.9f, Heard(listener), 5);
    }

    // The other lever, and the one that keeps a settlement's knowledge honest: reality. Working
    // the stuff writes what it actually is over whatever was going around.
    [Fact]
    public void WorkingTheStuffSettlesTheMatter()
    {
        var world = TalkativeWorld();
        var person = Somebody(world, 1);
        person.KnownTechniques.Add(TestCatalogs.BasicTwisting);
        person.Inventory.Add(TestCatalogs.GrassItem, TestCatalogs.GrassPerCord);
        Holds(person, 0.1f);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        var actual = world.Configuration.MaterialCatalog.Find(PlantFibre)!;
        Assert.Equal(actual.Fibrousness, Heard(person), 5);
    }

    // A retelling never says a substance is less than not fibrous at all.
    [Fact]
    public void NoTaleMakesASubstanceLessThanNothing()
    {
        var world = TalkativeWorld(distortion: 5f);
        var teller = Somebody(world, 1);
        var listeners = new List<Person>();
        for (var i = 0; i < 20; i++)
        {
            listeners.Add(Somebody(world, i + 2));
        }

        Holds(teller, 0.05f);
        world.Advance(3);

        Assert.All(listeners, person => Assert.True(Heard(person) >= 0f));
    }

    // Distortion is a rule, not a fact of life: turned off, word of mouth is exact.
    [Fact]
    public void WithNothingToDistortItWordPassesExactly()
    {
        var world = TalkativeWorld(distortion: 0f);
        var teller = Somebody(world, 1);
        var listener = Somebody(world, 2);
        Holds(teller, 0.9f);

        world.Advance(1);

        Assert.Equal(0.9f, Heard(listener), 5);
    }

    // Two people who heard it from different places disagree, and nothing anywhere says which of
    // them is wrong. Noticing the discrepancy is the play (section 6).
    [Fact]
    public void TwoPeopleCanEndUpDisagreeingAboutTheSameSubstance()
    {
        var world = TalkativeWorld();
        var ava = Somebody(world, 1);
        var bran = Somebody(world, 2, new Position(100, 0));
        var cass = Somebody(world, 3, new Position(100, 0));
        Holds(ava, 0.9f);

        bran.Position = ava.Position;
        world.Advance(1);
        bran.Position = cass.Position;
        world.Advance(1);

        Assert.NotEqual(Heard(bran), Heard(cass));
    }
}
