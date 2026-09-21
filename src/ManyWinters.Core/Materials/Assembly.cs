namespace ManyWinters.Core.Materials;

// An object: either one worked piece of a single substance, or two objects held together at a
// joint. Recursive and uncapped on purpose (see docs/materials-and-crafting-architecture.md
// section 6): every combinative verb is binary, so depth comes from binding a bound thing to a
// third thing, and absurd assemblies are limited by physics (weight accumulates, the weakest
// link governs) rather than a MaxParts constant nobody could defend.
//
// A closed two-case union rather than one record with optional components: unlike independent
// components where any combination is meaningful, exactly one of these two cases holds. The
// private constructor keeps the union closed.
//
// Thinner than section 6's full description for now, by the same rule that holds back unread
// material properties (section 2): a joint names neither the verb that made it nor the material
// that binds it, because nothing reads either yet.
public abstract record Assembly
{
    private Assembly()
    {
    }

    // Density times volume per part plus binding weight - same formula a stackable item's
    // weight uses, so both tiers weigh on one scale and working a thing neither creates nor
    // destroys weight.
    public abstract float Weight(MaterialCatalog materials);

    // The weakest link over both parts and joints (section 6) - a rope lashed to a rope lashed
    // to a rope is constructible and useless.
    public abstract float Durability(MaterialCatalog materials);

    // Quality (0-1) is how well the piece was worked. Volume is bulk in the same arbitrary units
    // as ItemDefinition.Volume, so an assembly's weight comes out comparable to a stackable
    // item's.
    public sealed record Part(MaterialId Material, FormId Form, float Quality = 0f, float Volume = 0f) : Assembly
    {
        // An undescribed material weighs nothing rather than throwing, matching how every other
        // reader of a material handles a missing one.
        public override float Weight(MaterialCatalog materials) => (materials.Find(Material)?.Density ?? 0f) * Volume;

        // A part is only as sound as its substance allows and its working achieved: brittle
        // stuff well worked and tough stuff botched both come out poor.
        public override float Durability(MaterialCatalog materials) => (materials.Find(Material)?.Toughness ?? 0f) * Quality;
    }

    // JointStrength (0-1) is what turns "every joint is a weak point" into a number Durability
    // can read. JointWeight is what the binding itself weighs - the cordage does not vanish just
    // because it is no longer a thing of its own.
    public sealed record Joined(float JointStrength, float JointWeight, Assembly Left, Assembly Right) : Assembly
    {
        public override float Weight(MaterialCatalog materials) => JointWeight + Left.Weight(materials) + Right.Weight(materials);

        public override float Durability(MaterialCatalog materials) =>
            Math.Min(JointStrength, Math.Min(Left.Durability(materials), Right.Durability(materials)));
    }
}
