using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// The third reductive verb: curing turns rawhide into hide (see
// docs/materials-and-crafting-architecture.md section 3, "Where a verb lands is the item's own
// business", and docs/todo/fauna-plan.md phase 4d). Unlike Twist and Knap, this is the one verb
// that changes the substance itself rather than only its shape - what a rawhide's own transition
// says it becomes (FormTransition.Material) is hide, a material with no shelf life of its own.
//
// Nothing here names rawhide. Whatever would otherwise rot is what curing is for
// (MaterialAffordances.CanCure), so a second perishable hide-like material found later cures the
// same way with no new code.
public sealed record TanCommand(Person Person, ItemKindId Item) : ICommand
{
    // Directing somebody to tan is how they learn to tan (see ActionOffer.TeachFirst).
    public static readonly SkillTypeId Skill = new("tanning");

    // The verb itself, as content names it in an item's transitions (see FormTransition).
    public static readonly TechniqueId Verb = new("tan");

    private static readonly ReductiveWork Work = new(Verb, Skill, MaterialAffordances.CanCure, ActionBlocker.NotCurable);

    public ActionBlocker Blocker(WorldState world) => Work.Blocker(Person, Item, world);

    public void Execute(WorldState world) => Work.Execute(Person, Item, world);
}
