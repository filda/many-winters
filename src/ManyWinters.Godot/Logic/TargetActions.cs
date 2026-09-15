using ManyWinters.Core.Commands;
using ManyWinters.Core.Construction;
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
    internal static TargetMenu For(WorldState world, Person actor, ResourceNode node)
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
    internal static ActionOffer Gather(WorldState world, Person actor, ResourceNode node) =>
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
        // an empty pack is worse than no line at all.
        if (target.Inventory.Counts.Count > 0)
        {
            offers.Add(ActionOffer.For("Take what they carried", new LootCommand(actor, target), world, target: target.Position));
        }

        return new TargetMenu(target.Name, offers);
    }

    internal static TargetMenu For(WorldState world, Person actor, Building building)
    {
        var items = world.Configuration.ItemCatalog;
        var offers = new List<ActionOffer>();

        // A line per kind actually there to move, in either direction: a store is a list of what
        // it holds, and "Put in" with nothing to put in is not a choice.
        foreach (var (item, count) in Sorted(actor.Inventory))
        {
            offers.Add(ActionOffer.For(
                $"Put in {Named(items, item)}",
                new DepositCommand(actor, building, item, count),
                world,
                target: building.Position));
        }

        foreach (var (item, count) in Sorted(building.Inventory))
        {
            offers.Add(ActionOffer.For(
                $"Take out {Named(items, item)}",
                new WithdrawCommand(actor, building, item, count),
                world,
                target: building.Position));
        }

        // Always offered, unlike the two above: a sound hut is still something the player may ask
        // about, and "Nothing to mend" is the answer.
        offers.Add(ActionOffer.For("Mend", new RepairCommand(actor, building), world, target: building.Position));

        return new TargetMenu(world.Configuration.BuildingCatalog.Get(building.Kind).DisplayName, offers);
    }

    internal static TargetMenu For(WorldState world, Person actor, Position ground)
    {
        var offers = new List<ActionOffer> { WalkTo(world, actor, ground) };

        // Same rule as the crafting lines on a person's own card (PersonActions): offered from the
        // first unit of the material, so "Build a store here" is a goal to work towards with the
        // blocker saying how far off it is, and absent entirely for somebody carrying none of it.
        foreach (var building in world.Configuration.BuildingCatalog.Definitions
                     .Where(building => actor.Inventory.Get(building.RequiredItem) > 0)
                     .OrderBy(building => building.Id.Value, StringComparer.Ordinal))
        {
            offers.Add(ActionOffer.For(
                $"Build {Lowered(building.DisplayName)}",
                new ConstructCommand(actor, building.Id, ground),
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
    private static IEnumerable<KeyValuePair<ItemKindId, int>> Sorted(Inventory inventory) =>
        inventory.Counts.OrderBy(entry => entry.Key.Value, StringComparer.Ordinal);

    private static string Named(ItemCatalog items, ItemKindId item) => Lowered(items.Get(item).DisplayName);

    // Lower case inside a sentence the UI wrote: "Put in wood", not "Put in Wood" - the same way
    // the rest of the player's prose sets a thing's name mid-phrase (see InspectorText.ForWork).
    private static string Lowered(string displayName) => displayName.ToLowerInvariant();
}
