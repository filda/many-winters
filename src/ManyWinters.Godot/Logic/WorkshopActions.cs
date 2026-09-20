using ManyWinters.Core.Commands;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// One thing in the pack the player can point at in the workshop: what to call it, and which of
// the two tiers it came out of (see Inventory, BindTarget).
internal readonly record struct WorkshopEntry(string Label, BindTarget Target);

// What the workshop panel offers, worked out apart from the panel that draws it.
//
// The player is never shown a list of verbs to choose from (see
// docs/materials-and-crafting-architecture.md section 7): they pick one thing or two and ask
// what comes of it, and this works out which verb that is. One thing is a reductive verb, two
// is a combinative one - the count of what was picked is the whole of the question, which is
// what keeps the panel the same shape however many verbs the game grows.
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
                $"{items.Get(entry.Key).DisplayName} x{entry.Value}",
                new BindTarget.Stock(entry.Key)));

        var worked = person.Inventory.Assemblies
            .Select(held => new WorkshopEntry(
                InspectorText.ForWorkedThing(held, world),
                new BindTarget.Worked(held)))
            .OrderBy(entry => entry.Label, StringComparer.Ordinal);

        return stock.Concat(worked).ToList();
    }

    // What the one thing in hand is like, in plain words - what the player has to go on when
    // forming a hypothesis, since the numbers behind it are never shown (see MaterialWords,
    // section 9). Only for a single pick: two things at once is a question about the pair, and
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
            BindTarget.Stock stock => world.Configuration.ItemCatalog.Get(stock.Kind).Material,
            BindTarget.Worked { Thing: Assembly.Part part } => part.Material,
            _ => (MaterialId?)null,
        };

        return id is { } material ? world.Configuration.MaterialCatalog.Find(material) : null;
    }

    // What trying the picked things together would be, or null when nothing would come of it -
    // an unworkable pick is not an error to word, it is simply not an offer.
    internal static ActionOffer? Attempt(WorldState world, Person person, IReadOnlyList<WorkshopEntry> picked) => picked.Count switch
    {
        1 => Reductive(world, person, picked[0]),
        2 => ActionOffer.For("Try it", new BindCommand(person, picked[0].Target, picked[1].Target), world, BindCommand.Skill),
        _ => null,
    };

    // Only raw stock can be worked down: the reductive verbs turn a material into a shape, and
    // nothing worked can be taken apart again yet. Which verb is the item's own business
    // (FormTransition, ReductiveVerbs), so this grows no wider as the vocabulary does.
    private static ActionOffer? Reductive(WorldState world, Person person, WorkshopEntry picked)
    {
        if (picked.Target is not BindTarget.Stock stock
            || ReductiveVerbs.For(person, stock.Kind, world.Configuration.ItemCatalog) is not { } work)
        {
            return null;
        }

        return ActionOffer.For("Try it", work.Command, world, work.Skill);
    }
}
