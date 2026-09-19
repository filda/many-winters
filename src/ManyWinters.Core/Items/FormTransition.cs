using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;

namespace ManyWinters.Core.Items;

// What working this item with a given verb turns it into. Declared per item rather than read off
// a global "fibre becomes cord" table, so grass and sinew can twist into cords of their own
// while the verb vocabulary itself stays a small closed set (see
// docs/materials-and-crafting-architecture.md section 3, "Where a verb lands is the item's own
// business").
//
// Only the resulting Form is stated: the material carries over unchanged, and the worked piece's
// bulk is what went into it, so weight is conserved without a second number to author and keep
// in step (see TwistCommand).
public sealed record FormTransition(TechniqueId Verb, FormId Form, int InputAmount);
