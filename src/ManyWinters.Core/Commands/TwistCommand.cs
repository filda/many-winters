using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// The first of the reductive verbs (see docs/materials-and-crafting-architecture.md section 3):
// grass into cord. What it turns into is the item's business, and whether the substance will
// take a twist at all is the material's, so neither answer is authored per outcome. The work is
// every reductive verb's work.
//
// The worked piece is the first thing in the game to be held as itself rather than as a count:
// its quality is this person's, earned at this moment, and two cords are not interchangeable.
public sealed record TwistCommand(Person Person, ItemKindId Item) : ICommand
{
    // Directing somebody to twist is how they learn to twist, the same way pointing at a tree
    // teaches gathering.
    public static readonly SkillTypeId Skill = new("twisting");

    // The verb, as content names it in an item's transitions.
    public static readonly TechniqueId Verb = new("twist");

    // Fibrous enough to have strands and pliable enough to take the twist - the material's
    // answer, not the item's.
    private static readonly ReductiveWork Work = new(Verb, Skill, MaterialAffordances.CanTwist, ActionBlocker.NotTwistable);

    public ActionBlocker Blocker(WorldState world) => Work.Blocker(Person, Item, world);

    public void Execute(WorldState world) => Work.Execute(Person, Item, world);
}
