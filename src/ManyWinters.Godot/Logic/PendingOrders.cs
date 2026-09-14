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
// the walk that carries it interrupts their current task (MoveCommand).
internal sealed class PendingOrders
{
    private readonly Dictionary<Person, ActionOffer> _orders = new();

    internal void Add(Person person, ActionOffer offer) => _orders[person] = offer;

    internal void Forget(Person person) => _orders.Remove(person);

    // Asked once a tick: which orders can now be carried out, and the quiet end of every order
    // that never will be. Still too far means still walking; nothing in the way means they have
    // arrived; any other answer means the world moved on while they walked - the tree felled by
    // somebody else, the corpse buried - which is the order's end rather than something to keep
    // waiting for. So is dying on the way.
    //
    // The offer is asked again rather than trusted (ActionOffer.Refreshed): the answer it was
    // made with is the one from where the person was standing when the player gave the order.
    internal IReadOnlyList<ActionOffer> Ready(WorldState world)
    {
        if (_orders.Count == 0)
        {
            return [];
        }

        var ready = new List<ActionOffer>();

        // Materialised: both branches below drop the order being looked at.
        foreach (var (person, offer) in _orders.ToList())
        {
            if (!person.IsAlive)
            {
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
        }

        return ready;
    }
}
