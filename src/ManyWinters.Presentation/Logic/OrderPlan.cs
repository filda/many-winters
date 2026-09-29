namespace ManyWinters.Presentation.Logic;

// What Perform does with an accepted offer, worked out as a plain function of the offer alone -
// pulled out of the order coordinator, which is Node-bound and so untestable without an engine
// (docs/development.md, Godot-layer testability), so this one decision can be tested without one.
internal enum OrderDispatch
{
    // The offer carries its own task: install it and let the simulation's own tick loop own
    // the attempt from here on, walking or not - Hunt and Butcher both work this way.
    InstallPursuit,

    // The one distance the person can be sent to close themselves: walk first, and remember the
    // order for PendingOrders to fire once they arrive.
    WalkThenExecute,

    // Nothing stands between the order and carrying it out.
    ExecuteNow,
}

internal static class OrderPlan
{
    internal static OrderDispatch For(ActionOffer offer)
    {
        if (offer.Pursuit is not null)
        {
            return OrderDispatch.InstallPursuit;
        }

        return offer.NeedsWalkingTo && offer.Target is not null
            ? OrderDispatch.WalkThenExecute
            : OrderDispatch.ExecuteNow;
    }
}
