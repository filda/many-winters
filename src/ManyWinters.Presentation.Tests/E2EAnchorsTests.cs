using ManyWinters.Core.World;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

public class E2EAnchorsTests
{
    [Fact]
    public void FirstLivingPersonSkipsTheDead()
    {
        var world = TestWorld.Create();
        var dead = TestWorld.AddAdult(world, "Dead", new Position(0, 0));
        dead.IsAlive = false;
        var alive = TestWorld.AddAdult(world, "Alive", new Position(1, 1));

        Assert.Same(alive, E2EAnchors.FirstLivingPerson(world.People));
    }

    [Fact]
    public void FirstLivingPersonIsNullWhenNobodyIsAlive()
    {
        var world = TestWorld.Create();
        var dead = TestWorld.AddAdult(world, "Dead", new Position(0, 0));
        dead.IsAlive = false;

        Assert.Null(E2EAnchors.FirstLivingPerson(world.People));
    }

    [Fact]
    public void NearestWoodResourceNodePicksTheClosestWoodYieldingNodeToCamp()
    {
        var world = TestWorld.Create();
        var campCenter = new Position(0, 0);

        // A stump yields Wood; an apple tree yields Apple - not wood, so it must never be picked
        // even though this one sits closer to camp.
        var apple = new Entity { Kind = TestWorld.AppleTree, Category = EntityCategory.Growable, Position = new Position(1, 0) };
        var farStump = new Entity { Kind = TestWorld.Stump, Category = EntityCategory.Growable, Position = new Position(20, 0) };
        var nearStump = new Entity { Kind = TestWorld.Stump, Category = EntityCategory.Growable, Position = new Position(5, 0) };
        world.AddEntity(apple);
        world.AddEntity(farStump);
        world.AddEntity(nearStump);

        var picked = E2EAnchors.NearestWoodResourceNode(world.Entities, world.Configuration.ResourceCatalog, campCenter);

        Assert.Same(nearStump, picked);
    }

    [Fact]
    public void NearestWoodResourceNodeIgnoresBuildingsAndPilesEvenIfTheirKindWouldYieldWood()
    {
        var world = TestWorld.Create();
        var campCenter = new Position(0, 0);

        var pile = new Entity { Kind = TestWorld.Stump, Category = EntityCategory.Pile, Position = new Position(1, 0) };
        world.AddEntity(pile);

        Assert.Null(E2EAnchors.NearestWoodResourceNode(world.Entities, world.Configuration.ResourceCatalog, campCenter));
    }

    [Fact]
    public void NearestLivingAnimalSkipsTheDeadAndPicksTheClosest()
    {
        var world = TestWorld.Create();
        var campCenter = new Position(0, 0);

        var dead = TestWorld.AddAdultAnimal(world, new Position(1, 0));
        dead.IsAlive = false;
        TestWorld.AddAdultAnimal(world, new Position(20, 0));
        var near = TestWorld.AddAdultAnimal(world, new Position(5, 0));

        Assert.Same(near, E2EAnchors.NearestLivingAnimal(world.Animals, campCenter));
    }

    [Fact]
    public void NearestLivingAnimalIsNullWhenNoneAreAlive()
    {
        var world = TestWorld.Create();
        var dead = TestWorld.AddAdultAnimal(world, new Position(1, 0));
        dead.IsAlive = false;

        Assert.Null(E2EAnchors.NearestLivingAnimal(world.Animals, new Position(0, 0)));
    }
}
