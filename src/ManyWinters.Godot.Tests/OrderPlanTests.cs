using ManyWinters.Core.World;
using ManyWinters.Godot.Logic;

namespace ManyWinters.Godot.Tests;

// The decision OrderCoordinator.Perform acts on, pulled out here because OrderCoordinator itself
// takes a WorldPresenter (Node-bound) and cannot be built without a running engine
// (docs/development.md, Godot-layer testability).
public class OrderPlanTests
{
    private static readonly Position Camp = new(0, 0);
    private static readonly Position FarAway = new(50, 0);

    // An offer carrying its own Pursuit task always installs it, whatever the blocker says about
    // distance - Hunt and Butcher own the walk themselves (docs/todo/fauna-plan.md, phase 3c).
    [Fact]
    public void AnOfferWithAPursuitTaskInstallsIt()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var deer = TestWorld.AddAdultAnimal(world, FarAway);

        var hunt = TargetActions.For(world, person, deer).Offers[0];

        Assert.NotNull(hunt.Pursuit);
        Assert.Equal(OrderDispatch.InstallPursuit, OrderPlan.For(hunt));
    }

    [Fact]
    public void AnOfferOutOfReachWithNoPursuitWalksFirst()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var node = new Entity { Kind = TestWorld.AppleTree, Category = EntityCategory.Growable, Position = FarAway, Growth = new GrowthState { RemainingAmount = 100, MaxAmount = 100 } };
        world.AddEntity(node);

        var gather = TargetActions.Gather(world, person, node);

        Assert.Null(gather.Pursuit);
        Assert.Equal(OrderDispatch.WalkThenExecute, OrderPlan.For(gather));
    }

    [Fact]
    public void AnOfferAlreadyInReachWithNoPursuitRunsNow()
    {
        var world = TestWorld.Create();
        var person = TestWorld.AddAdult(world, "Ava", Camp);
        var node = new Entity { Kind = TestWorld.AppleTree, Category = EntityCategory.Growable, Position = Camp, Growth = new GrowthState { RemainingAmount = 100, MaxAmount = 100 } };
        world.AddEntity(node);

        var gather = TargetActions.Gather(world, person, node);

        Assert.Equal(OrderDispatch.ExecuteNow, OrderPlan.For(gather));
    }
}
