using ManyWinters.Core.Commands;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Commands;

// Trying a thing is how somebody comes to know it. This is the *reach* a player buys by
// directing an attempt: idle hands only turn over the familiar, so without this nobody could
// ever find out about a substance the band understands nothing about (see
// docs/materials-and-crafting-architecture.md section 7, "How the two paths differ").
public class LearningByTryingTests
{
    private static readonly MaterialId PlantFibre = new("plant_fibre");
    private static readonly MaterialId WoodMaterial = new("wood");
    private static readonly MaterialId StoneMaterial = new("stone");

    // Nothing comes of merely carrying things here, so what a person ends up understanding, they
    // understood by working it.
    private static WorldState WorldWhereCarryingTeachesNothing() =>
        new(TestCatalogs.CreateConfiguration() with
        {
            Rules = SimulationRules.Default with { MaterialUnderstandingPerTick = 0f },
        });

    private static Person Twister(WorldState world)
    {
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicTwisting);
        person.Inventory.Add(TestCatalogs.GrassItem, TestCatalogs.GrassPerCord);

        return person;
    }

    private static void Practise(Person person, SkillTypeId skill)
    {
        for (var i = 0; i < 50; i++)
        {
            person.Skills.Increase(skill, 1f);
        }
    }

    [Fact]
    public void WorkingSomethingTeachesWhatItIs()
    {
        var world = WorldWhereCarryingTeachesNothing();
        var person = Twister(world);
        Practise(person, TwistCommand.Skill);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        var actual = world.Configuration.MaterialCatalog.Find(PlantFibre)!;
        Assert.True(person.Beliefs.IsFirm(PlantFibre, MaterialProperty.Fibrousness));
        Assert.Equal(actual.Fibrousness, person.Beliefs.AsBelieved(actual).Fibrousness, 5);
    }

    // A spoiled attempt says as much about the stuff as a good one - which is what makes "this
    // does not work" something a person can come to believe without any separate notion of
    // negative knowledge.
    [Fact]
    public void ASpoiledAttemptTeachesJustAsMuch()
    {
        var world = WorldWhereCarryingTeachesNothing();
        var person = Twister(world);
        while (WorkAttempt.Succeeds(person, TwistCommand.Skill, TwistCommand.Verb, world.Clock.CurrentTick))
        {
            world.Clock.Advance();
        }

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        Assert.Empty(person.Inventory.Assemblies);
        Assert.True(person.Beliefs.IsFirm(PlantFibre, MaterialProperty.Fibrousness));
    }

    // One go teaches what a season of carrying it about would: they had it in their hands and
    // saw what it did.
    [Fact]
    public void OneGoIsWorthASeasonOfCarryingItAbout()
    {
        var world = WorldWhereCarryingTeachesNothing();
        var carrier = world.SpawnPerson("Bran", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        carrier.Inventory.Add(TestCatalogs.GrassItem, TestCatalogs.GrassPerCord);
        var worker = Twister(world);
        Practise(worker, TwistCommand.Skill);

        world.Execute(new TwistCommand(worker, TestCatalogs.GrassItem));

        Assert.True(worker.Beliefs.IsFirm(PlantFibre, MaterialProperty.Fibrousness));
        Assert.False(carrier.Beliefs.IsFirm(PlantFibre, MaterialProperty.Fibrousness));
    }

    // Binding has hands on three things at once: both of what is joined and the cordage.
    [Fact]
    public void BindingTeachesEverySubstanceItHadHandsOn()
    {
        var world = WorldWhereCarryingTeachesNothing();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicBinding);
        person.Inventory.Add(TestCatalogs.WoodItem, 1);
        person.Inventory.Add(TestCatalogs.StoneItem, 1);
        person.Inventory.AddAssembly(new Assembly.Part(PlantFibre, TestCatalogs.Cord, Quality: 0.5f, Volume: 15f));

        world.Execute(new BindCommand(
            person,
            new CarriedThing.Stock(TestCatalogs.WoodItem),
            new CarriedThing.Stock(TestCatalogs.StoneItem)));

        Assert.True(person.Beliefs.IsFirm(WoodMaterial, MaterialProperty.Toughness));
        Assert.True(person.Beliefs.IsFirm(StoneMaterial, MaterialProperty.Hardness));
        Assert.True(person.Beliefs.IsFirm(PlantFibre, MaterialProperty.Fibrousness));
    }

    // An attempt that never happened teaches nothing: a refused command does nothing at all,
    // and being told no is not the same as having tried.
    [Fact]
    public void AnAttemptNobodyCouldMakeTeachesNothing()
    {
        var world = WorldWhereCarryingTeachesNothing();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);
        person.KnownTechniques.Add(TestCatalogs.BasicTwisting);
        person.Inventory.Add(TestCatalogs.GrassItem, TestCatalogs.GrassPerCord - 1);

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        Assert.Empty(person.Beliefs.Held);
    }

    // The reach the player is buying: somebody who understood nothing can be sent to try, and
    // comes back understanding - which then lets them work it of their own accord.
    [Fact]
    public void SomebodyWhoUnderstoodNothingCanBeSentToFindOut()
    {
        var world = WorldWhereCarryingTeachesNothing();
        var person = Twister(world);
        Practise(person, TwistCommand.Skill);

        Assert.False(person.Beliefs.HoldsAnythingAbout(PlantFibre));

        world.Execute(new TwistCommand(person, TestCatalogs.GrassItem));

        Assert.True(person.Beliefs.HoldsAnythingAbout(PlantFibre));
    }
}
