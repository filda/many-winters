using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// The player's view of whoever is selected, when it is an animal rather than a person
// (docs/todo/fauna-plan.md, phase 2b) - the same idea as SelectionCardTests, narrowed to what
// AnimalCard actually shows: no actions, no pack, no knowledge.
public class AnimalCardTests
{
    [Fact]
    public void TheCardNamesTheSpecies()
    {
        var world = TestWorld.Create();
        var deer = TestWorld.AddAdultAnimal(world, new Position(0, 0));

        Assert.Equal("Deer", AnimalCard.For(world, deer).Title);
    }

    [Fact]
    public void AgeAndSexStandBesideTheTitle()
    {
        var world = TestWorld.Create();
        var deer = TestWorld.AddAdultAnimal(world, new Position(0, 0));

        Assert.Equal("woman", AnimalCard.For(world, deer).Beside);
    }

    [Fact]
    public void TheDeadSayOnlyThat()
    {
        var world = TestWorld.Create();
        var deer = TestWorld.AddAdultAnimal(world, new Position(0, 0));
        deer.IsAlive = false;

        Assert.Equal("deceased", AnimalCard.For(world, deer).Beside);
    }

    [Fact]
    public void SomethingWithNothingToDoIsIdle()
    {
        var world = TestWorld.Create();
        var deer = TestWorld.AddAdultAnimal(world, new Position(0, 0));

        Assert.Equal("Idle", AnimalCard.For(world, deer).Task);
    }

    [Fact]
    public void AGrazerNamesWhatItIsGathering()
    {
        var world = TestWorld.Create();
        var deer = TestWorld.AddAdultAnimal(world, new Position(0, 0));
        var node = new Entity { Kind = TestWorld.AppleTree, Category = EntityCategory.Growable, Position = new Position(3, 4) };
        deer.Tasks.Interrupt(new GatherTask(node, reachDistance: 2f));

        Assert.Equal("Gathering apple", AnimalCard.For(world, deer).Task);
    }

    [Fact]
    public void AFleeingAnimalIsSaidToBeFleeing()
    {
        var world = TestWorld.Create();
        var deer = TestWorld.AddAdultAnimal(world, new Position(0, 0));
        var person = TestWorld.AddAdult(world, "Ava", new Position(1, 0));
        deer.Tasks.Interrupt(new FleeTask(person, new SpeciesDefinition.FleeDefinition(FleeDistance: 8f, SafeDistance: 16f, SpeedPerTick: 0.6f)));

        Assert.Equal("Fleeing", AnimalCard.For(world, deer).Task);
    }

    [Fact]
    public void AFawnFollowingItsMotherNeedsNoNameToSayWhat()
    {
        var world = TestWorld.Create();
        var fawn = TestWorld.AddAdultAnimal(world, new Position(0, 0));
        var mother = TestWorld.AddAdultAnimal(world, new Position(1, 0));
        fawn.Tasks.Interrupt(new FollowTask(mother, keepWithin: 2f, speedPerTick: 0.25f));

        Assert.Equal("Keeping close to its mother", AnimalCard.For(world, fawn).Task);
    }

    // A corpse is not doing anything, and "At rest" under one reads as a joke - the same rule
    // SelectionCard follows for a dead person.
    [Fact]
    public void TheDeadAreNotDoingAnything()
    {
        var world = TestWorld.Create();
        var deer = TestWorld.AddAdultAnimal(world, new Position(0, 0));
        deer.IsAlive = false;

        Assert.Equal(string.Empty, AnimalCard.For(world, deer).Task);
    }

    // Nothing on a live deer to take yet, so the panel has no dead line to hide - AnimalCard
    // leaves it empty rather than saying "nothing left" of something still on its feet.
    [Fact]
    public void ALivingAnimalHasNoCarcassLine()
    {
        var world = TestWorld.Create();
        var deer = TestWorld.AddAdultAnimal(world, new Position(0, 0));

        Assert.Equal(string.Empty, AnimalCard.For(world, deer).Carcass);
    }

    [Fact]
    public void ACarcassListsWhatIsLeftOnIt()
    {
        var world = TestWorld.Create();
        var deer = TestWorld.AddAdultAnimal(world, new Position(0, 0));
        deer.IsAlive = false;
        deer.Inventory.Add(TestWorld.Meat, 30);
        deer.Inventory.Add(TestWorld.Hide, 1);
        deer.Inventory.Add(TestWorld.Bone, 4);
        deer.Inventory.Add(TestWorld.Sinew, 2);

        Assert.Equal("Carcass: Bone 4, Hide 1, Meat 30, Sinew 2", AnimalCard.For(world, deer).Carcass);
    }

    // A butchered-out carcass is worth saying so about, rather than an empty "Carcass: " that
    // reads like a bug.
    [Fact]
    public void AnEmptyCarcassSaysNothingIsLeft()
    {
        var world = TestWorld.Create();
        var deer = TestWorld.AddAdultAnimal(world, new Position(0, 0));
        deer.IsAlive = false;

        Assert.Equal("Nothing left", AnimalCard.For(world, deer).Carcass);
    }

    [Fact]
    public void TheFedMeterEmptiesAsHungerRises()
    {
        var world = TestWorld.Create();
        var deer = TestWorld.AddAdultAnimal(world, new Position(0, 0));

        Assert.Equal(1f, AnimalCard.For(world, deer).Fed.Fraction, 3);

        deer.Needs.Hunger = deer.MaxHunger / 2f;

        Assert.Equal(0.5f, AnimalCard.For(world, deer).Fed.Fraction, 3);
    }
}
