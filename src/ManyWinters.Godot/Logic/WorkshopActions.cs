using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// One thing in the pack the player can point at in the workshop: what to call it, how many of it
// are held, and which of the two tiers it came out of.
//
// The count is its own field rather than part of the label, because the bench draws the thing
// rather than naming it - the name is what the cursor gets, the count is a mark in the corner of
// the picture. A made thing is always one of itself.
internal readonly record struct WorkshopEntry(string Label, CarriedThing Target, int Count = 1);

// What the workshop panel offers, worked out apart from the panel that draws it.
//
// Two different questions live on the same bench. Working the pack itself is never a list of
// verbs to choose from (see docs/materials-and-crafting-architecture.md section 7): the player
// picks one thing or two and asks what comes of it, and this works out which verb that is. One
// thing is a reductive verb, two is a combinative one - the count of what was picked is the
// whole of the question, which is what keeps that half of the panel the same shape however many
// verbs the game grows. Making something from a recipe is the other half, and is named up front
// rather than discovered by trying things.
//
// Nothing here says "you could twist that": an attempt that leads nowhere comes back as no
// offer at all, and the panel says only that nothing comes of it. Finding out what works is the
// game.
internal static class WorkshopActions
{
    internal static IReadOnlyList<WorkshopEntry> Carried(WorldState world, Person person)
    {
        var items = world.Configuration.ItemCatalog;

        var stock = person.Inventory.Counts
            .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
            .Select(entry => new WorkshopEntry(
                items.Get(entry.Key).DisplayName,
                new CarriedThing.Stock(entry.Key),
                entry.Value));

        var worked = person.Inventory.Assemblies
            .Select(held => new WorkshopEntry(
                InspectorText.ForWorkedThing(held, world),
                new CarriedThing.Worked(held)))
            .OrderBy(entry => entry.Label, StringComparer.Ordinal);

        return stock.Concat(worked).ToList();
    }

    // What the one thing in hand is like, in plain words - what the player has to go on when
    // forming a hypothesis, since the numbers behind it are never shown. Only for a single pick:
    // two things at once is a question about the pair, and
    // a wall of adjectives is not an answer to it. Empty when nothing is known of the
    // substance, and the panel then says nothing rather than saying "unknown".
    internal static IReadOnlyList<string> WordsFor(WorldState world, Person person, IReadOnlyList<WorkshopEntry> picked)
    {
        if (picked.Count != 1 || MaterialOf(world, picked[0]) is not { } material)
        {
            return [];
        }

        // What this person takes it to be, not what it is: somebody who has never handled the
        // stuff has nothing to say about it, and the bench says nothing on their behalf.
        return MaterialWords.For(person.Beliefs.AsBelieved(material));
    }

    private static MaterialDefinition? MaterialOf(WorldState world, WorkshopEntry entry)
    {
        var id = entry.Target switch
        {
            CarriedThing.Stock stock => world.Configuration.ItemCatalog.Get(stock.Kind).Material,
            CarriedThing.Worked { Thing: Assembly.Part part } => part.Material,
            _ => (MaterialId?)null,
        };

        return id is { } material ? world.Configuration.MaterialCatalog.Find(material) : null;
    }

    // A recipe is named up front, unlike a reductive or combinative verb: the player already
    // knows an axe when they see one, and hiding the word "axe" behind "Make" would only be coy.
    // Only for a recipe the person can actually carry out right now - how far short they are of
    // the material is not this bench's business to explain - and only for a recipe whose output
    // actually fits in the pack; one heavy enough to need placing (a storage hut) is offered from
    // the ground instead, by pointing at it.
    internal static IReadOnlyList<ActionOffer> Recipes(WorldState world, Person person) =>
        world.Configuration.RecipeCatalog.Definitions
            .Where(recipe => person.Inventory.Get(recipe.InputItem) > 0)
            .Where(recipe => PersonActions.FitsInInventory(world, person, recipe.Output))
            .OrderBy(recipe => recipe.Output.Value, StringComparer.Ordinal)
            .Select(recipe => ActionOffer.For(
                $"Make {world.Configuration.ItemCatalog.Get(recipe.Output).DisplayName.ToLowerInvariant()}",
                new MakeCommand(person, recipe.Output),
                world))
            .Where(offer => offer.IsAvailable)
            .ToList();

    // Eating out of the pack without leaving the bench for the card - offered only for a single
    // pick that is actually food, the same restriction the card's own Eat carries. Not offered
    // for a made thing at all: nothing worked ever eats.
    internal static ActionOffer? Eat(WorldState world, Person person, IReadOnlyList<WorkshopEntry> picked) =>
        picked.Count == 1 && picked[0].Target is CarriedThing.Stock stock
            && world.Configuration.ItemCatalog.HungerRestoredPerUnitFor(stock.Kind) > 0f
            ? ActionOffer.For("Eat", new EatCommand(person, stock.Kind), world, EatCommand.Skill)
            : null;

    // Putting the one thing picked straight back down, the same act as the card's own Drop line
    // - a whole stack goes down at once, so the amount asked for is what is actually carried
    // rather than the single unit a pick stands for elsewhere on this bench.
    internal static ActionOffer? Drop(WorldState world, Person person, IReadOnlyList<WorkshopEntry> picked)
    {
        if (picked.Count != 1)
        {
            return null;
        }

        CarriedThing whole = picked[0].Target switch
        {
            CarriedThing.Stock stock => new CarriedThing.Stock(stock.Kind, person.Inventory.Get(stock.Kind)),
            CarriedThing.Worked worked => worked,
            _ => throw new ArgumentOutOfRangeException(nameof(picked), picked[0].Target, "Unknown kind of thing to put down."),
        };

        return ActionOffer.For("Drop", new DropCommand(person, whole), world);
    }

    // What trying the picked things together would be, or null when nothing would come of it -
    // an unworkable pick is not an error to word, it is simply not an offer.
    internal static ActionOffer? Attempt(WorldState world, Person person, IReadOnlyList<WorkshopEntry> picked) => picked.Count switch
    {
        1 => Reductive(world, person, picked[0]),
        2 => ActionOffer.For("Make", new BindCommand(person, picked[0].Target, picked[1].Target), world, BindCommand.Skill),
        _ => null,
    };

    // One thing picked, from either tier. Raw stock is worked down - which verb is the item's
    // own business - and a worked thing is worked over, which today means its edge renewed.
    // Neither branch grows as the vocabulary does, and neither names a verb to the player.
    private static ActionOffer? Reductive(WorldState world, Person person, WorkshopEntry picked) => picked.Target switch
    {
        CarriedThing.Stock stock => ReductiveVerbs.For(person, stock.Kind, world.Configuration.ItemCatalog) is { } work
            ? ActionOffer.For("Make", work.Command, world, work.Skill)
            : null,

        // Asked of the object rather than of the command, because a thing with no edge is not a
        // refusal to word - it is simply not an offer.
        CarriedThing.Worked worked when SharpenCommand.HasAnEdge(worked.Thing, world) =>
            ActionOffer.For("Make", new SharpenCommand(person, worked.Thing), world, SharpenCommand.Skill),

        _ => null,
    };
}
