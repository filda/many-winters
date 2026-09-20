using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// What people standing together say to each other about the stuff of the world (see Beliefs).
// Talk rather than instruction: understanding spreads through a band before anybody has learned
// to teach.
public class BeliefSharingTests
{
    private static readonly MaterialId PlantFibre = new("plant_fibre");

    // Certain rather than rare, and nothing learned by handling, so what arrives in a listener
    // arrived by being told.
    private static WorldState TalkativeWorld() =>
        new(TestCatalogs.CreateConfiguration() with
        {
            Rules = SimulationRules.Default with
            {
                BeliefSharingChancePerTick = 1f,
                MaterialUnderstandingPerTick = 0f,
                IdleDiscoveryChancePerTick = 0f,
            },
        });

    private static Person Person(WorldState world, string name, Position? position = null) =>
        world.SpawnPerson(name, position ?? new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

    private static void ComesToKnow(Person person, float value = 0.9f) =>
        person.Beliefs.Learn(PlantFibre, MaterialProperty.Fibrousness, value, confidenceGained: 1f);

    [Fact]
    public void WhatOnePersonKnowsPassesToSomebodyStandingWithThem()
    {
        var world = TalkativeWorld();
        var teller = Person(world, "Ava");
        var listener = Person(world, "Bran");
        ComesToKnow(teller);

        world.Advance(1);

        Assert.True(listener.Beliefs.ConfidenceIn(PlantFibre, MaterialProperty.Fibrousness) > 0f);
    }

    // Nobody has to know how to teach to mention that grass is fibrous.
    [Fact]
    public void TellingSomebodyNeedsNoTeachingSkill()
    {
        var world = TalkativeWorld();
        var teller = Person(world, "Ava");
        var listener = Person(world, "Bran");
        ComesToKnow(teller);

        world.Advance(1);

        Assert.Empty(teller.KnownTechniques);
        Assert.NotEmpty(listener.Beliefs.Held);
    }

    // Held less firmly than what they could have found out for themselves: one mention is talk.
    [Fact]
    public void WhatSomebodyWasMerelyToldIsNotYetSomethingTheyWouldActOn()
    {
        var world = TalkativeWorld();
        var teller = Person(world, "Ava");
        var listener = Person(world, "Bran");
        ComesToKnow(teller);

        world.Advance(1);

        Assert.False(listener.Beliefs.IsFirm(PlantFibre, MaterialProperty.Fibrousness));
        Assert.Equal(
            world.Configuration.Rules.HearsayConfidence,
            listener.Beliefs.ConfidenceIn(PlantFibre, MaterialProperty.Fibrousness),
            5);
    }

    // Hearsay twice is conviction.
    [Fact]
    public void HearingTheSameThingAgainMakesItSomethingTheyWouldActOn()
    {
        var world = TalkativeWorld();
        var first = Person(world, "Ava");
        var second = Person(world, "Bran");
        var listener = Person(world, "Cass");
        ComesToKnow(first);
        ComesToKnow(second);

        world.Advance(4);

        Assert.True(listener.Beliefs.IsFirm(PlantFibre, MaterialProperty.Fibrousness));
    }

    [Fact]
    public void WhatIsPassedOnIsWhatTheTellerTakesToBeTrue()
    {
        var world = TalkativeWorld();
        var teller = Person(world, "Ava");
        var listener = Person(world, "Bran");
        ComesToKnow(teller, value: 0.42f);

        world.Advance(1);

        // The teller's own account, not the world's: what passes between people is what they
        // take to be so, which is what makes a distorted one able to travel later.
        var heard = Assert.Single(listener.Beliefs.Held);
        Assert.Equal(0.42f, heard.Value.Value, 5);
        Assert.NotEqual(0.42f, world.Configuration.MaterialCatalog.Find(PlantFibre)!.Fibrousness);
    }

    // Out of earshot is out of the conversation.
    [Fact]
    public void NobodyTellsSomebodyOnTheOtherSideOfTheMap()
    {
        var world = TalkativeWorld();
        var teller = Person(world, "Ava");
        var listener = Person(world, "Bran", new Position(500, 500));
        ComesToKnow(teller);

        world.Advance(20);

        Assert.Empty(listener.Beliefs.Held);
    }

    [Fact]
    public void NobodyPassesOnWhatTheyOnlyHalfBelieveThemselves()
    {
        var world = TalkativeWorld();
        var teller = Person(world, "Ava");
        var listener = Person(world, "Bran");
        teller.Beliefs.Learn(PlantFibre, MaterialProperty.Fibrousness, 0.9f, confidenceGained: 0.3f);

        world.Advance(20);

        Assert.Empty(listener.Beliefs.Held);
    }

    [Fact]
    public void TheDeadNeitherTellNorListen()
    {
        var world = TalkativeWorld();
        var teller = Person(world, "Ava");
        var listener = Person(world, "Bran");
        ComesToKnow(teller);
        listener.IsAlive = false;

        world.Advance(5);

        Assert.Empty(listener.Beliefs.Held);
    }

    // Nothing to say and nothing said: a band that understands nothing passes nothing about.
    [Fact]
    public void ABandThatKnowsNothingSaysNothing()
    {
        var world = TalkativeWorld();
        var ava = Person(world, "Ava");
        var bran = Person(world, "Bran");

        world.Advance(20);

        Assert.Empty(ava.Beliefs.Held);
        Assert.Empty(bran.Beliefs.Held);
    }

    // Understanding spreads through a band standing together, which is the point of the whole
    // pass: knowledge lives in people, and a crowd is how it gets about.
    [Fact]
    public void UnderstandingSpreadsThroughACrowdStandingTogether()
    {
        var world = TalkativeWorld();
        var knower = Person(world, "Ava");
        ComesToKnow(knower);
        var others = new List<Person>();
        for (var i = 0; i < 5; i++)
        {
            others.Add(Person(world, $"Other{i}"));
        }

        world.Advance(10);

        Assert.All(others, person => Assert.True(person.Beliefs.HoldsAnythingAbout(PlantFibre)));
    }
}
