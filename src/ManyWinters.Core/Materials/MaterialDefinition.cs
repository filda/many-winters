namespace ManyWinters.Core.Materials;

// The substance an item is made of, as opposed to the shape it has been worked into (FormId).
// Physical properties live here and item-level numbers are derived from them, so "how heavy is
// a stone axe" is answered by stone's density rather than by a number typed onto the axe - see
// docs/materials-and-crafting-architecture.md.
//
// Only the properties something already reads are defined: Density (ItemCatalog.WeightFor) and
// Insulation (ItemCatalog.InsulationFor). The rest of the set in that document - hardness,
// toughness, flexibility, elasticity, fibrousness, flammability, plasticity - arrives together
// with the affordance predicates that read them, rather than sitting here unused first.
public sealed record MaterialDefinition(
    MaterialId Id,
    string DisplayName,
    // Weight per unit of ItemDefinition.Volume. Roughly "1 is water", so wood floats and stone
    // does not, but only the scale relative to carry capacity actually matters.
    float Density = 0f,
    // How much someone carrying anything made of this is insulated from the cold (see
    // WorldState.Advance). Same "presence, not count" convention this carried as an item field:
    // a second hide doesn't warm anyone twice as much.
    float Insulation = 0f);
