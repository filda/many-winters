namespace ManyWinters.Core.Materials;

// What a material's physical properties let it be used for, derived rather than stored (see
// docs/materials-and-crafting-architecture.md section 2). Pure functions of a MaterialDefinition
// so they are testable without any engine setup, and so a new material affords whatever its
// numbers say without content authoring a second time per verb.
public static class MaterialAffordances
{
    // Fibrous enough to have strands, and pliable enough that those strands take a twist without
    // snapping - grass qualifies, a wood shaft does not.
    public static bool CanTwist(MaterialDefinition material) =>
        material.Fibrousness > 0.5f && material.Flexibility > 0.4f;

    // Hard enough to hold an edge, brittle enough to fracture into one under a strike - flint,
    // not wood.
    public static bool CanKnap(MaterialDefinition material) =>
        material.Hardness > 0.7f && material.Toughness < 0.3f;

    // Too brittle to survive a blow intact - crumbles rather than deforms.
    public static bool CanCrush(MaterialDefinition material) =>
        material.Toughness < 0.4f;

    // Pliable enough to curve without fracturing.
    public static bool CanBend(MaterialDefinition material) =>
        material.Flexibility > 0.5f;

    // Springs back hard enough to store energy - a bow stave, a snare, a trap trigger.
    public static bool HoldsTension(MaterialDefinition material) =>
        material.Elasticity > 0.6f;
}
