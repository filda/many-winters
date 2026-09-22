using ManyWinters.Core.Commands;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// Orders given to somebody too far away to carry them out: they are sent walking, and the order
// waits here until they arrive. Without it every action aimed at something out of reach would be
// a greyed-out line telling the player to walk the person over first, and walking over is not
// the interesting half of "go and fell that tree".
//
// One order per person. A new one replaces whatever they were on their way to do, the same way
// the walk that carries it interrupts their current task.
// A dead end reached while walking: the target the player pointed at is no longer there to act
// on (felled by somebody else, buried already) by the time the person arrives. Named by who was
// sent and what they were sent to do, so the player can be told rather than left to notice the
// person just stopped.
internal readonly record struct FailedOrder(Person Person, string Label);

internal sealed class PendingOrders
{
    private readonly Dictionary<Person, ActionOffer> _orders = new();

    // The failures from the most recent Ready call, for whoever wants to tell the player about
    // them. Dying on the way is not included: nobody expects to be told a dead person's errand
    // fell through.
    internal IReadOnlyList<FailedOrder> Failed { get; private set; } = [];

    internal void Add(Person person, ActionOffer offer) => _orders[person] = offer;

    internal void Forget(Person person) => _orders.Remove(person);

    // Asked once a tick: which orders can now be carried out, and the quiet end of every order
    // that never will be. Still too far means still walking; nothing in the way means they have
    // arrived; any other answer means the world moved on while they walked - the tree felled by
    // somebody else, the corpse buried - which is the order's end rather than something to keep
    // waiting for. So is dying on the way.
    //
    // The offer is asked again rather than trusted: the answer it was made with is the one from
    // where the person was standing when the player gave the order.
    internal IReadOnlyList<ActionOffer> Ready(WorldState world)
    {
        // Stryker disable once Block: an empty-block mutant here is equivalent - the loop below
        // does nothing over an empty dictionary either way, and Failed ends up [] regardless.
        // This is purely the fast path for the common case of nobody walking anywhere.
        if (_orders.Count == 0)
        {
            Failed = [];
            return [];
        }

        var ready = new List<ActionOffer>();
        var failed = new List<FailedOrder>();

        // Materialised: both branches below drop the order being looked at.
        foreach (var (person, offer) in _orders.ToList())
        {
            if (!person.IsAlive)
            {
                // Stryker disable once Statement: dropping this Remove is equivalent - a dead
                // person fails the IsAlive check and continues past every later tick too, so
                // nothing Ready() or Failed ever report differs. It only spares the dictionary a
                // stale entry.
                _orders.Remove(person);
                continue;
            }

            var refreshed = offer.Refreshed(world);
            if (refreshed.Blocker is ActionBlocker.TooFar)
            {
                continue;
            }

            _orders.Remove(person);
            if (refreshed.Blocker is ActionBlocker.None)
            {
                ready.Add(refreshed);
            }
            else
            {
                failed.Add(new FailedOrder(person, offer.Label));
            }
        }

        Failed = failed;
        return ready;
    }
}
