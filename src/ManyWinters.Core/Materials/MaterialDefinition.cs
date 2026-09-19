namespace ManyWinters.Core.Materials;

// The substance an item is made of, as opposed to its shape (FormId). Physical properties live
// here and item numbers derive from them - see docs/materials-and-crafting-architecture.md.
// Only properties something reads are defined (Density: ItemCatalog.WeightFor, Insulation:
// ItemCatalog.InsulationFor, Hardness/Toughness/Flexibility/Elasticity/Fibrousness:
// MaterialAffordances); the rest arrive with the affordance predicates that read them.
public sealed record MaterialDefinition(
    MaterialId Id,
    string DisplayName,
    // Weight per unit of ItemDefinition.Volume, roughly "1 is water"; only the scale relative
    // to carry capacity matters.
    float Density = 0f,
    // Cold insulation for anyone carrying anything made of this (see WorldState.Advance).
    // Presence, not count: a second hide does not warm twice as much.
    float Insulation = 0f,
    // Resistance to denting/scratching, 0-1. Read by MaterialAffordances.CanKnap and, later, by
    // edge-related function scores (docs/materials-and-crafting-architecture.md section 4).
    float Hardness = 0f,
    // Resistance to fracturing under stress, 0-1 - the inverse of brittleness. Read by
    // MaterialAffordances.CanKnap (as brittleness) and CanCrush.
    float Toughness = 0f,
    // How readily the material bends without breaking, 0-1. Read by MaterialAffordances.CanTwist
    // and CanBend.
    float Flexibility = 0f,
    // How much the material springs back after being stretched or bent, 0-1 - distinct from
    // Flexibility: hide is pliable but does not spring back. Read by
    // MaterialAffordances.HoldsTension.
    float Elasticity = 0f,
    // How much the material is made of separable strands, 0-1. Read by
    // MaterialAffordances.CanTwist.
    float Fibrousness = 0f);
