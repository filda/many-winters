using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Commands;

// The second reductive verb: a stone lump struck until it fractures into a wedge - the first
// edge in the game, and so the first object that can fell a tree (see
// ItemCatalog.ChoppingScoreOf, FellCommand).
//
// Nothing here names stone. Hard enough to hold an edge and brittle enough to fracture into one
// is what knapping asks of a substance (MaterialAffordances.CanKnap), so a harder, more brittle
// stone found later knaps better with no new code - which is the whole of section 1's split
// between what a thing is made of and what shape it has been brought to.
public sealed record KnapCommand(Person Person, ItemKindId Item) : ICommand
{
    // Directing somebody to knap is how they learn to knap (see ActionOffer.TeachFirst).
    public static readonly SkillTypeId Skill = new("knapping");

    // The verb itself, as content names it in an item's transitions (see FormTransition).
    public static readonly TechniqueId Verb = new("knap");

    private static readonly ReductiveWork Work = new(Verb, Skill, MaterialAffordances.CanKnap, ActionBlocker.NotKnappable);

    public ActionBlocker Blocker(WorldState world) => Work.Blocker(Person, Item, world);

    public void Execute(WorldState world) => Work.Execute(Person, Item, world);
}
