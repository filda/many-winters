using ManyWinters.Core.Items;
using ManyWinters.Core.Materials;

namespace ManyWinters.Core.Commands;

// One thing out of somebody's pack, from either of its two tiers (see Inventory): so many units
// of raw stock, or one object they made. Anything that acts on "a thing a person is carrying"
// asks for it this way - binding two of them together, working one over, putting one down - so
// none of those has to know the tiers exist.
//
// It is also what lets depth need no special case: a made thing is a thing, so what was bound
// binds again (see docs/materials-and-crafting-architecture.md section 6).
public abstract record CarriedThing
{
    private CarriedThing()
    {
    }

    // Amount defaults to one because most callers want one of something; dropping is the caller
    // that puts down a whole stack at once.
    public sealed record Stock(ItemKindId Kind, int Amount = 1) : CarriedThing;

    public sealed record Worked(Assembly Thing) : CarriedThing;
}
