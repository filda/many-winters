using ManyWinters.Core.Commands;
using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// Orders given to somebody who has to walk there first: they wait until the person arrives, and
// end quietly if the world moves on while they walk.
public class PendingOrdersTests
{
    private static readonly Position Camp = new(0, 0);
    private static readonly Position FarAway = new(50, 0);

    private static Entity AddNode(WorldState world, Position position)
    {
        var node = new Entity
        {
            Kind = TestWorld.AppleTree,
            Category = EntityCategory.Growable,
            Position = position,
            Growth = new GrowthState { RemainingAmount = 100, MaxAmount = 100 },
        };
        world.AddEntity(node);
        return node;
    }

    [Fact]
    public void AnOrderGivenAcrossTheClearingIsNotReadyYet()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var orders = new PendingOrders();
        orders.Add(person, TargetActions.Gather(world, person, AddNode(world, FarAway)));

        Assert.Empty(orders.Ready(world));
    }

    [Fact]
    public void AnOrderIsReadyOnceTheyHaveArrived()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var node = AddNode(world, FarAway);
        var orders = new PendingOrders();
        orders.Add(person, TargetActions.Gather(world, person, node));

        person.Position = node.Position;

        var ready = Assert.Single(orders.Ready(world));

        Assert.Equal(ActionBlocker.None, ready.Blocker);
        Assert.IsType<GatherCommand>(ready.Command);
    }

    // An order fires once: it is off the list the moment it is handed back, or every later tick
    // would gather from the same bush again.
    [Fact]
    public void AnOrderIsHandedBackOnlyOnce()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var node = AddNode(world, FarAway);
        var orders = new PendingOrders();
        orders.Add(person, TargetActions.Gather(world, person, node));
        person.Position = node.Position;

        Assert.Single(orders.Ready(world));
        Assert.Empty(orders.Ready(world));
    }

    // The answer the offer was made with is the one from where the person stood when the order
    // was given, so it has to be asked again on arrival (see ActionOffer.Refreshed).
    [Fact]
    public void TheOrderHandedBackIsAskedOfTheWorldAsItIsNow()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var node = AddNode(world, FarAway);
        var offer = TargetActions.Gather(world, person, node);
        var orders = new PendingOrders();
        orders.Add(person, offer);
        person.Position = node.Position;

        Assert.Equal(ActionBlocker.TooFar, offer.Blocker);
        Assert.Equal(ActionBlocker.None, Assert.Single(orders.Ready(world)).Blocker);
    }

    // The world moved on while they walked. That is the order's end, not something to keep
    // waiting for: the tree was felled by somebody else, so there is nothing to arrive at. It is
    // worth telling the player, unlike a quiet arrival.
    [Fact]
    public void AnOrderWhoseTargetIsGoneEndsRatherThanWaiting()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var node = AddNode(world, FarAway);
        var orders = new PendingOrders();
        orders.Add(person, TargetActions.Gather(world, person, node));

        node.Growth!.IsAlive = false;
        person.Position = node.Position;

        Assert.Empty(orders.Ready(world));
        var failed = Assert.Single(orders.Failed);
        Assert.Equal(person, failed.Person);
        Assert.Equal("Gather", failed.Label);

        // Reported once, the same as a successful order.
        Assert.Empty(orders.Ready(world));
        Assert.Empty(orders.Failed);
    }

    // Dying on the way is the order's end too, but nobody expects to be told a dead person's
    // errand fell through.
    [Fact]
    public void AnOrderGivenToSomebodyWhoDiesOnTheWayEndsWithoutBeingReportedAsFailed()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var node = AddNode(world, FarAway);
        var orders = new PendingOrders();
        orders.Add(person, TargetActions.Gather(world, person, node));

        person.Position = node.Position;
        person.IsAlive = false;

        Assert.Empty(orders.Ready(world));
        Assert.Empty(orders.Failed);
    }

    // A new order replaces whatever they were on their way to do, the same way the walk that
    // carries it interrupts their current task.
    [Fact]
    public void ASecondOrderReplacesTheFirst()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var first = AddNode(world, FarAway);
        var second = AddNode(world, new Position(50, 1));
        var orders = new PendingOrders();
        orders.Add(person, TargetActions.Gather(world, person, first));
        orders.Add(person, TargetActions.Gather(world, person, second));

        person.Position = FarAway;

        var ready = Assert.Single(orders.Ready(world));

        Assert.Equal(second, Assert.IsType<GatherCommand>(ready.Command).Node);
    }

    [Fact]
    public void AForgottenOrderNeverComesBack()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var node = AddNode(world, FarAway);
        var orders = new PendingOrders();
        orders.Add(person, TargetActions.Gather(world, person, node));

        orders.Forget(person);
        person.Position = node.Position;

        Assert.Empty(orders.Ready(world));
    }

    [Fact]
    public void NobodyWalkingAnywhereIsNothingToResolve()
    {
        Assert.Empty(new PendingOrders().Ready(TestWorld.Create()));
    }

    // One person walking is not the other's business: each order stands or falls on its own.
    [Fact]
    public void OnePersonArrivingDoesNotFireAnotherPersonsOrder()
    {
        var world = TestWorld.Create();
        var ava = TestWorld.AddAdult(world, "Ava", Camp);
        var bran = TestWorld.AddAdult(world, "Bran", Camp);
        var node = AddNode(world, FarAway);
        var orders = new PendingOrders();
        orders.Add(ava, TargetActions.Gather(world, ava, node));
        orders.Add(bran, TargetActions.Gather(world, bran, node));

        ava.Position = node.Position;

        Assert.Single(orders.Ready(world));
    }
}
