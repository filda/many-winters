using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// What the player may do with the person they have selected - and only what is an act on that
// person themselves. Anything aimed at something else in the world (a tree to fell, a store to
// put wood into, somebody to bury or to have a child with) is asked for by pointing at the thing,
// not by picking a line off a card; those live in the contextual menu (docs/todo/todo.md).
//
// Nothing is offered without something to act on, so no entry here can ever read "nothing
// nearby". Every one arrives with the world's own answer about whether it can run, and the panel
// only has to draw what comes back (see ActionOffer, ActionBlocker).
//
// Engine-free, so the list is an ordinary function of world state and can be tested without a
// running Godot. What a thing is called in English is decided here too, the way InspectorText
// decides the rest of the inspector's prose.
internal static class PersonActions
{
    internal static IReadOnlyList<ActionOffer> For(WorldState world, Person person)
    {
        var offers = new List<ActionOffer>();

        if (Eat(world, person) is { } eat)
        {
            offers.Add(eat);
        }

        return offers;
    }

    // Offered only to someone actually carrying something edible. An Eat button on an empty pack
    // is an instruction to go and find food, which is not what pressing it would do.
    private static ActionOffer? Eat(WorldState world, Person person)
    {
        if (CarriedFood(world, person) is not { } item)
        {
            return null;
        }

        return ActionOffer.For("Eat", new EatCommand(person, item), world, EatCommand.Skill);
    }

    private static ItemKindId? CarriedFood(WorldState world, Person person) =>
        person.Inventory.Counts.Keys
            .Where(kind => world.Configuration.ItemCatalog.HungerRestoredPerUnitFor(kind) > 0f)
            .OrderBy(kind => kind.Value, StringComparer.Ordinal)
            .Cast<ItemKindId?>()
            .FirstOrDefault();
}
