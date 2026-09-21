using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Logic;

// What the player pointed at and what may be done with it. The heading names the thing in the
// player's own words, so the actions under it need not repeat it: "Apple tree" over "Gather",
// not a column of "Gather from the apple tree".
internal sealed record TargetMenu(string Heading, IReadOnlyList<ActionOffer> Offers);

// The other half of PersonActions: what the selected person may do with something else in the
// world. One overload per kind of thing there is rather than one abstraction over all of them -
// a tree, a person, a hut and a patch of ground have nothing in common but a position, and an
// interface over the four would be a place to put four switch cases.
//
// Same rules as PersonActions. Nothing is offered without something to act on, so no line can
// ever read "nothing here": what a kind of thing simply cannot do (felling a mushroom) is left
// out, and only what the moment forbids (too far, empty-handed) is shown greyed with the reason.
// Every offer carries where it happens, so a refusal for distance alone becomes a walk rather
// than an instruction to the player (see ActionOffer.Target).
//
// Engine-free, so the menu is an ordinary function of world state and can be tested without a
// running Godot. What a thing is called in English is decided here too, the way InspectorText
// decides the rest of the player's prose.
internal static class TargetActions
{
    // One overload per kind of thing there is, per EntityCategory, since Entity now covers a
    // resource, a pile and a building alike - a tree, a pile of apples and a hut still have
    // nothing in common but a position, so the switch stands in for what used to be three
    // separate parameter types.
    internal static TargetMenu For(WorldState world, Person actor, Entity entity) => entity.Category switch
    {
        EntityCategory.Growable => ForResource(world, actor, entity),
        EntityCategory.Pile => ForPile(world, actor, entity),
        EntityCategory.Building => ForBuilding(world, actor, entity),
        _ => throw new ArgumentOutOfRangeException(nameof(entity), entity.Category, "Unknown entity category."),
    };

    private static TargetMenu ForResource(WorldState world, Person actor, Entity node)
    {
        var resource = world.Configuration.ResourceCatalog.Get(node.Kind);
        var offers = new List<ActionOffer> { Gather(world, actor, node) };

        // Only for what can be felled at all: a standing "Fell" on every mushroom is a line the
        // player learns to ignore rather than a refusal they can do anything about.
        if (resource.CanFell)
        {
            offers.Add(ActionOffer.For("Fell", new FellCommand(actor, node), world, resource.Skill, node.Position));
        }

        return new TargetMenu(resource.DisplayName, offers);
    }

    // Split out because a left click on a resource means this and nothing else, so Main asks for
    // it by name rather than by taking whichever offer happens to come first.
    internal static ActionOffer Gather(WorldState world, Person actor, Entity node) =>
        ActionOffer.For(
            "Gather",
            new GatherCommand(actor, node),
            world,
            world.Configuration.ResourceCatalog.Get(node.Kind).Skill,
            node.Position);

    // The living and the dead are offered different things entirely, rather than one list half of
    // which is always refused: teaching a corpse and fathering a child with it are not choices
    // worth drawing, and neither is burying somebody who is still talking.
    internal static TargetMenu For(WorldState world, Person actor, Person target)
    {
        var offers = new List<ActionOffer>();

        if (ReferenceEquals(actor, target))
        {
            // Everything a person does to themselves is already on their own card (PersonActions),
            // which is on screen the whole time they are selected.
            return new TargetMenu(target.Name, offers);
        }

        if (target.IsAlive)
        {
            offers.AddRange(Lessons(world, actor, target));
            offers.Add(HaveAChild(world, actor, target));
            return new TargetMenu(target.Name, offers);
        }

        // No skill to grant by pointing at it, unlike gathering or eating: burying needs nothing
        // taught (BuryCommand has no knowledge gate), and marking the grave is earned by doing it.
        offers.Add(ActionOffer.For("Bury", new BuryCommand(actor, target), world, target: target.Position));

        // Nothing on the body, nothing to take: an enabled "Take what they carried" that empties
        // an empty pack is worse than no line at all. Both tiers count - somebody who died
        // holding nothing but the axe they made is carrying the thing most worth taking.
        if (target.Inventory.Counts.Count > 0 || target.Inventory.Assemblies.Count > 0)
        {
            offers.Add(ActionOffer.For("Take what they carried", new LootCommand(actor, target), world, target: target.Position));
        }

        return new TargetMenu(target.Name, offers);
    }

    // A pile is one kind of stock or one made thing (see Entity.Made), so the heading already
    // names it and the one offer under it is a bare verb - the same shape as a resource's
    // "Gather". A made thing is named the way it is named everywhere else: the band's own word
    // for it if they have coined one, and what it is made of if they have not.
    private static TargetMenu ForPile(WorldState world, Person actor, Entity pile) =>
        new(
            pile.Made is { } made
                ? InspectorText.ForWorkedThing(made, world)
                : world.Configuration.ItemCatalog.Get(new ItemKindId(pile.Kind.Value)).DisplayName,
            [PickUp(world, actor, pile)]);

    // Split out for the same reason as Gather and WalkTo: a left click on a pile means this and
    // nothing else.
    internal static ActionOffer PickUp(WorldState world, Person actor, Entity pile) =>
        ActionOffer.For("Pick up", new PickUpItemCommand(actor, pile), world, target: pile.Position);

    private static TargetMenu ForBuilding(WorldState world, Person actor, Entity building)
    {
        var items = world.Configuration.ItemCatalog;
        var offers = new List<ActionOffer>();

        // A line per thing actually there to move, in either direction: a store is a list of what
        // it holds, and "Put in" with nothing to put in is not a choice. Both tiers in one list,
        // as on the person's own card (see PersonActions.Drops).
        foreach (var (label, what) in Movable(world, actor.Inventory))
        {
            offers.Add(ActionOffer.For(
                $"Put in {label}",
                new DepositCommand(actor, building, what),
                world,
                target: building.Position));
        }

        foreach (var (label, what) in Movable(world, building.Storage!))
        {
            offers.Add(ActionOffer.For(
                $"Take out {label}",
                new WithdrawCommand(actor, building, what),
                world,
                target: building.Position));
        }

        // Always offered, unlike the two above: a sound hut is still something the player may ask
        // about, and "Nothing to mend" is the answer.
        offers.Add(ActionOffer.For("Mend", new RepairCommand(actor, building), world, target: building.Position));

        return new TargetMenu(items.Get(new ItemKindId(building.Kind.Value)).DisplayName, offers);
    }

    internal static TargetMenu For(WorldState world, Person actor, Position ground)
    {
        var offers = new List<ActionOffer> { WalkTo(world, actor, ground) };

        // The other half of the recipe list PersonActions.Crafts offers: only for a recipe whose
        // output does not fit in the pack, which is what makes it worth choosing a spot for at
        // all. Same rule as the crafting lines on a person's own card: offered from the first
        // unit of the material, so "Build a store here" is a goal to work towards with the
        // blocker saying how far off it is, and absent entirely for somebody carrying none of it.
        foreach (var recipe in world.Configuration.RecipeCatalog.Definitions
                     .Where(recipe => actor.Inventory.Get(recipe.InputItem) > 0)
                     .Where(recipe => !PersonActions.FitsInInventory(world, actor, recipe.Output))
                     .OrderBy(recipe => recipe.Output.Value, StringComparer.Ordinal))
        {
            offers.Add(ActionOffer.For(
                $"Build {Lowered(world.Configuration.ItemCatalog.Get(recipe.Output).DisplayName)}",
                new MakeCommand(actor, recipe.Output, ground),
                world,
                target: ground));
        }

        // Not named after the spot: a patch of grass has no name, and coordinates are a fact about
        // the simulation rather than about the world the player is looking at.
        return new TargetMenu("This spot", offers);
    }

    // Split out for the same reason as Gather: a left click on bare ground means this and nothing
    // else.
    internal static ActionOffer WalkTo(WorldState world, Person actor, Position ground) =>
        ActionOffer.For("Walk here", new MoveCommand(actor, ground), world, target: ground);

    // A line per thing the teacher could pass on, not one "teach them the lot": a lesson is a
    // single technique, the way the band's own casual teaching hands over at most one per tick
    // between two people standing together (WorldState.AutoTeachNearbyPeople). Which one is the
    // player's choice, and the point of directing it at all - it is how somebody gets taught the
    // thing that will keep them alive before the dice get round to it.
    //
    // Base techniques only, again as the casual pass does: an efficient technique is worked out
    // by doing the thing over and over (SkillDefinition.EfficientTechnique), and handing it over
    // would skip the practice it stands for. Named by the skill rather than by the technique's
    // id, which is the debug inspector's business (see InspectorText.ForKnowledge).
    private static IEnumerable<ActionOffer> Lessons(WorldState world, Person actor, Person student) =>
        world.Configuration.SkillCatalog.Definitions
            .Where(skill => actor.KnownTechniques.Contains(skill.BaseTechnique))
            .Where(skill => !student.KnownTechniques.Contains(skill.BaseTechnique))
            .OrderBy(skill => skill.DisplayName, StringComparer.Ordinal)
            .Select(skill => ActionOffer.For(
                $"Teach {Lowered(skill.DisplayName)}",
                new TeachCommand(actor, student, skill.BaseTechnique),
                world,
                TeachCommand.TeachingSkill,
                student.Position));

    // Whose child it would be follows from who they are, not from who was pointed at first; two
    // of the same sex leave the command to refuse it (ActionBlocker.WrongSex). The name is drawn
    // the way the band draws its own children's names, so a child the player asks for is named
    // like any other (see WorldState.NameForNewborn).
    private static ActionOffer HaveAChild(WorldState world, Person actor, Person target)
    {
        var mother = actor.Sex == Sex.Female ? actor : target;
        var father = ReferenceEquals(mother, actor) ? target : actor;
        var name = world.NameForNewborn(mother, father, world.Clock.CurrentTick);

        return ActionOffer.For("Have a child", new BirthCommand(name, mother, father), world, target: target.Position);
    }

    // In item-id order, so a store's lines do not reshuffle between one opening of the menu and
    // the next (an Inventory is a dictionary, whose order is nobody's promise).
    // Everything in an inventory that can be moved somewhere else, named as the player reads it
    // and ordered together whichever tier it came out of. A stack moves whole; a made thing has
    // no count to move some of.
    private static IEnumerable<(string Label, CarriedThing What)> Movable(WorldState world, Inventory inventory)
    {
        var items = world.Configuration.ItemCatalog;

        var stock = inventory.Counts
            .Where(entry => entry.Value > 0)
            .Select(entry => (Label: Named(items, entry.Key), What: (CarriedThing)new CarriedThing.Stock(entry.Key, entry.Value)));

        var made = inventory.Assemblies
            .Select(thing => (Label: InspectorText.ForWorkedThing(thing, world), What: (CarriedThing)new CarriedThing.Worked(thing)));

        return stock.Concat(made).OrderBy(entry => entry.Label, StringComparer.Ordinal);
    }

    private static string Named(ItemCatalog items, ItemKindId item) => Lowered(items.Get(item).DisplayName);

    // Lower case inside a sentence the UI wrote: "Put in wood", not "Put in Wood" - the same way
    // the rest of the player's prose sets a thing's name mid-phrase (see InspectorText.ForWork).
    private static string Lowered(string displayName) => displayName.ToLowerInvariant();
}
