namespace ManyWinters.Core.Materials;

// An object: either one worked piece of a single substance, or two objects held together at a
// joint. Recursive and uncapped on purpose (see docs/materials-and-crafting-architecture.md
// section 6) - because every combinative verb is binary, depth arises from binding a bound
// thing to a third thing, and absurd assemblies are answered by physics (weight accumulates,
// the weakest link governs) rather than by a MaxParts constant nobody could defend.
//
// A closed two-case union rather than one record with optional components (the shape Entity
// uses): Entity's components are independent and any combination of them is meaningful,
// whereas here exactly one of the two cases holds. The private constructor keeps the union
// closed, and each case answering for itself is what leaves no unreachable "neither" branch to
// read, test and mutate.
//
// Deliberately thinner than section 6's full description for now, by the same rule that holds
// back unread material properties (section 2): a joint names neither the verb that made it nor
// the material that binds it, because nothing reads either until the combinative verbs arrive
// to set them.
public abstract record Assembly
{
    private Assembly()
    {
    }

    // Density times volume, summed over every part, plus whatever the bindings themselves
    // weigh - the same formula a stackable item's weight uses, so the two tiers weigh on one
    // scale and working a thing neither creates nor destroys weight.
    public abstract float Weight(MaterialCatalog materials);

    // The weakest link, over both the parts and the joints (section 6) - which is what makes a
    // rope lashed to a rope lashed to a rope constructible and useless.
    public abstract float Durability(MaterialCatalog materials);

    // One worked piece: the substance, the shape it was worked into, how well it was worked
    // (Quality, 0-1, earned by whoever shaped it) and how much of it there is. Volume is bulk in
    // the same arbitrary units as ItemDefinition.Volume, so an assembly's weight comes out
    // comparable to a stackable item's (see ItemCatalog.WeightFor).
    public sealed record Part(MaterialId Material, FormId Form, float Quality = 0f, float Volume = 0f) : Assembly
    {
        // An undescribed material weighs nothing rather than throwing, as everywhere else that
        // reads one (see ItemCatalog.WeightFor, MaterialCatalog.Find).
        public override float Weight(MaterialCatalog materials) => (materials.Find(Material)?.Density ?? 0f) * Volume;

        // A part is only as sound as its substance allows and its working achieved, so brittle
        // stuff well worked and tough stuff botched both come out poor.
        public override float Durability(MaterialCatalog materials) => (materials.Find(Material)?.Toughness ?? 0f) * Quality;
    }

    // JointStrength is 0-1, set by whatever made the joint; it is what turns "every joint is a
    // weak point" into a number Durability can read. JointWeight is what the binding itself
    // weighs - the cordage that went into the lashing does not vanish just because it is no
    // longer a thing of its own.
    public sealed record Joined(float JointStrength, float JointWeight, Assembly Left, Assembly Right) : Assembly
    {
        public override float Weight(MaterialCatalog materials) => JointWeight + Left.Weight(materials) + Right.Weight(materials);

        public override float Durability(MaterialCatalog materials) =>
            Math.Min(JointStrength, Math.Min(Left.Durability(materials), Right.Durability(materials)));
    }
}
