using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;

namespace ManyWinters.Core.Items;

// What working this item with a given verb turns it into. Declared per item rather than read off
// a global "fibre becomes cord" table, so grass and sinew can twist into cords while the verb
// vocabulary stays a small closed set (see docs/materials-and-crafting-architecture.md section 3,
// "Where a verb lands is the item's business").
//
// The resulting Form is always stated; the material carries over unchanged unless the verb
// changes the substance itself, which is what curing does (rawhide tanned into hide) - that is
// why Material is here at all, a fifth fact only for the one class of verb that needs it. Either
// way the worked piece's bulk is what went into it, so weight is conserved without a second
// number to author and keep in step.
public sealed record FormTransition(TechniqueId Verb, FormId Form, int InputAmount, MaterialId? Material = null);
