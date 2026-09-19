using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// What the player may do with the person they have selected - and only what is an act on that
// person themselves: eating out of their own pack, making something out of what is in it.
// Anything aimed at something else in the world (a tree to fell, a store to put wood into,
// somebody to bury or to have a child with) is asked for by pointing at the thing, not by
// picking a line off a card; those are TargetActions.
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

        offers.AddRange(Crafts(world, person));
        offers.AddRange(Twists(world, person));
        offers.AddRange(Drops(world, person));

        return offers;
    }

    // Working a material with one's hands is an act on the person themselves, like making
    // something out of the pack, so it belongs on their card. Offered only for what this person
    // actually carries and what the stuff itself will take (MaterialAffordances.CanTwist read
    // through the command), so the card never lists a way to ruin something that cannot be done.
    //
    // This is the plain first home for a verb; the workshop panel described in
    // docs/materials-and-crafting-architecture.md section 7 is where the player will eventually
    // try things on each other without being told in advance what works.
    private static IEnumerable<ActionOffer> Twists(WorldState world, Person person) =>
        person.Inventory.Counts.Keys
            .Where(kind => world.Configuration.ItemCatalog.TransitionFor(kind, TwistCommand.Verb) is not null)
            .OrderBy(kind => kind.Value, StringComparer.Ordinal)
            .Select(kind => ActionOffer.For(
                $"Twist {world.Configuration.ItemCatalog.Get(kind).DisplayName.ToLowerInvariant()}",
                new TwistCommand(person, kind),
                world,
                TwistCommand.Skill));

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

    // Making something out of what is in the pack is an act on the person themselves, so it
    // belongs on their card rather than in a menu aimed at something in the world. Only for a
    // recipe whose output actually fits in the pack - one heavy enough to need placing (a
    // storage hut) is offered from the ground instead (see TargetActions), the same "goes where
    // it is heavy enough to need going" rule MakeCommand itself runs on.
    //
    // Offered from the first unit of the material, not from the whole cost: "Make an axe" over
    // two of the five wood it takes is a goal the player can send them after, with the blocker
    // saying how far off it is. Carrying none of the material at all and the line is absent -
    // the same rule Eat follows, and the reason the card never grows a column of things nobody
    // could make.
    private static IEnumerable<ActionOffer> Crafts(WorldState world, Person person) =>
        world.Configuration.RecipeCatalog.Definitions
            .Where(recipe => person.Inventory.Get(recipe.InputItem) > 0)
            .Where(recipe => FitsInInventory(world, person, recipe.Output))
            .OrderBy(recipe => recipe.Output.Value, StringComparer.Ordinal)
            .Select(recipe => ActionOffer.For(
                $"Make {world.Configuration.ItemCatalog.Get(recipe.Output).DisplayName.ToLowerInvariant()}",
                new MakeCommand(person, recipe.Output),
                world));

    // Shared with TargetActions, which offers the opposite half of the same recipe list - the
    // one live check MakeCommand itself runs to decide where an output lands.
    internal static bool FitsInInventory(WorldState world, Person person, ItemKindId output) =>
        person.Inventory.HasRoomFor(output, world.Configuration.ItemCatalog, world.MaxCarryWeightFor(person));

    // Dropping is offered per item kind actually carried, for the same reason Crafts is: a line
    // for material nobody has would just be an invitation to go find some, which pressing it
    // could not do. Drops the whole stack, matching the "one pile at a time" shape DropItemCommand
    // already has.
    private static IEnumerable<ActionOffer> Drops(WorldState world, Person person) =>
        person.Inventory.Counts
            .Where(carried => carried.Value > 0)
            .OrderBy(carried => carried.Key.Value, StringComparer.Ordinal)
            .Select(carried => ActionOffer.For(
                $"Drop {world.Configuration.ItemCatalog.Get(carried.Key).DisplayName.ToLowerInvariant()}",
                new DropItemCommand(person, carried.Key, carried.Value),
                world));

    private static ItemKindId? CarriedFood(WorldState world, Person person) =>
        person.Inventory.Counts.Keys
            .Where(kind => world.Configuration.ItemCatalog.HungerRestoredPerUnitFor(kind) > 0f)
            .OrderBy(kind => kind.Value, StringComparer.Ordinal)
            .Cast<ItemKindId?>()
            .FirstOrDefault();
}
