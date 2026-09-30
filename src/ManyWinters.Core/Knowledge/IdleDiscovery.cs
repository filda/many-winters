using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.Tasks;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Knowledge;

public static class IdleDiscovery
{
    // Handling a thing teaches what it is like. Nobody is told that grass is fibrous; they
    // carry it about and come to know. A person's understanding of the world is therefore the
    // sum of what they have actually had in their hands, which is why a band that never picks
    // anything up learns nothing about anything.
    public static void LearnWhatIsInHand(WorldState world)
    {
        var gained = world.Configuration.Rules.MaterialUnderstandingPerTick;

        foreach (var person in world.People)
        {
            if (!person.IsAlive)
            {
                continue;
            }

            foreach (var material in MaterialsInHand(world, person))
            {
                if (world.Configuration.MaterialCatalog.Find(material) is not { } actual)
                {
                    continue;
                }

                // Learned true: nothing distorts a belief yet, and what is noticed first-hand
                // would be the last thing to.
                person.Beliefs.LearnAll(actual, gained);
            }
        }
    }

    // Idle hands turning something over, and now and then working out how it is done (see
    // docs/materials-and-crafting-architecture.md section 7, "idle experimentation"). This is
    // what keeps knowledge living in people rather than in the player's head: a settlement left
    // alone still develops, and a band that comes after an extinction re-derives things for
    // itself instead of waiting to be shown.
    //
    // The rule is one sentence: a person works out how to do the thing they could have done
    // already, if only they had known how. Each verb's own command is asked what stands in the
    // way, and discovery happens exactly when the answer is "nothing but not knowing" - so
    // nothing here re-states what a verb needs, and a verb that grows a new requirement is
    // obeyed here for free.
    //
    // Undirected, unlike the workbench: what is tried is whatever is in their hands, taken at
    // random, and they do not choose it. The player who aims an attempt is buying aim, which is
    // what makes directing worth the time it costs (section 7, "How the two paths differ").
    public static void DiscoverByFiddling(WorldState world, long currentTick)
    {
        foreach (var person in world.People)
        {
            // Only genuinely idle hands: somebody walking somewhere or working a resource is
            // busy with that, and a person nobody has taught to be anywhere is the one with
            // time to turn a thing over.
            if (!person.IsAlive || person.Tasks.Current is not IdleTask)
            {
                continue;
            }

            if (TrialOf(world, person, currentTick) is not { } trial)
            {
                continue;
            }

            var (skill, command) = trial;
            if (command.Blocker(world) is not ActionBlocker.NotLearned)
            {
                continue;
            }

            if (world.Configuration.SkillCatalog.Find(skill) is { } definition
                && PassesIdleDiscoveryRoll(world, person, skill, currentTick))
            {
                person.KnownTechniques.Add(definition.BaseTechnique);
            }
        }
    }

    private static IEnumerable<MaterialId> MaterialsInHand(WorldState world, Person person)
    {
        var items = world.Configuration.ItemCatalog;

        return person.Inventory.Counts.Keys
            .Select(kind => items.Get(kind).Material)
            .Concat(person.Inventory.Assemblies.SelectMany(PartMaterialsOf))
            .Distinct();
    }

    // Every substance in a made thing, however deep: somebody carrying a hafted axe about has
    // their hands on both the stone and the wood.
    private static IEnumerable<MaterialId> PartMaterialsOf(Assembly assembly) => assembly switch
    {
        Assembly.Part part => [part.Material],
        Assembly.Joined joined => PartMaterialsOf(joined.Left).Concat(PartMaterialsOf(joined.Right)),
        _ => [],
    };

    // What this person happens to be turning over this tick: one thing out of the pack, or two.
    // Drawn from the same seeded stream as every other autonomous roll, so a replay fiddles with
    // the same things in the same order.
    // Only what they understand. Idle hands turn over the familiar, so a substance nobody has
    // yet come to know is not one they will idly think to work - the player can direct an
    // attempt on anything, and that difference in *reach* is what directing buys beyond speed
    // (docs/materials-and-crafting-architecture.md section 7).
    private static (SkillTypeId Skill, ICommand Command)? TrialOf(WorldState world, Person person, long currentTick)
    {
        var stock = person.Inventory.Counts.Keys
            .Where(kind => person.Beliefs.HoldsAnythingAbout(world.Configuration.ItemCatalog.Get(kind).Material))
            .OrderBy(kind => kind.Value, StringComparer.Ordinal)
            .ToList();
        var worked = person.Inventory.Assemblies;
        var things = stock.Count + worked.Count;
        if (things == 0)
        {
            return null;
        }

        var rng = new Random(SeedHash.Avalanche(unchecked((uint)(person.Id.Seed * 40503) ^ ((uint)currentTick * 2654435761u))));

        // Two things in hand is a chance to wonder what they would be together; one is a chance
        // to wonder what it would be on its own. With only one thing there is nothing to bind.
        if (things > 1 && rng.Next(2) == 0)
        {
            var left = TargetAt(stock, worked, rng.Next(things));
            var right = TargetAt(stock, worked, rng.Next(things));

            return (BindCommand.Skill, new BindCommand(person, left, right));
        }

        // Otherwise they turn one thing over by itself: raw stock gets worked down, and something
        // already made gets worked over, which today means its edge renewed. Either may come to
        // nothing - stuff that answers to no reductive verb, or a made thing with no edge on it.
        return WorkingOverOneThing(world, person, TargetAt(stock, worked, rng.Next(things)));
    }

    private static (SkillTypeId Skill, ICommand Command)? WorkingOverOneThing(WorldState world, Person person, CarriedThing picked) => picked switch
    {
        CarriedThing.Stock stock => ReductiveVerbs.For(person, stock.Kind, world.Configuration.ItemCatalog),
        CarriedThing.Worked worked when SharpenCommand.HasAnEdge(worked.Thing, world) =>
            (SharpenCommand.Skill, new SharpenCommand(person, worked.Thing)),
        _ => null,
    };

    // Concrete List rather than the interface: the caller has one, and indexing through
    // IReadOnlyList costs an interface dispatch per pick (CA1859).
    private static CarriedThing TargetAt(List<ItemKindId> stock, IReadOnlyList<Assembly> worked, int index) =>
        index < stock.Count
            ? new CarriedThing.Stock(stock[index], 1)
            : new CarriedThing.Worked(worked[index - stock.Count]);

    // Deterministic from the person, the verb and the tick, as every other roll is. A person's
    // own Curiosity scales it, which is the knob an NPC band turns down.
    private static bool PassesIdleDiscoveryRoll(WorldState world, Person person, SkillTypeId skill, long currentTick)
    {
        var chance = world.Configuration.Rules.IdleDiscoveryChancePerTick * person.Curiosity;
        var mixed = unchecked((uint)(person.Id.Seed * 2246822519) ^ (uint)(SeedHash.StableStringHash(skill.Value) * 3266489917) ^ ((uint)currentTick * 668265263u));

        // Stryker disable once Equality: NextDouble() returning exactly the chance has
        // probability zero, so < and <= are the same roll
        return new Random(SeedHash.Avalanche(mixed)).NextDouble() < chance;
    }
}
