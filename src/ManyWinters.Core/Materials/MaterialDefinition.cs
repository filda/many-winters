namespace ManyWinters.Core.Materials;

// The substance an item is made of, as opposed to its shape (FormId). Physical properties live
// here and item numbers derive from them - see docs/materials-and-crafting-architecture.md.
// Only properties something reads are defined (Density: ItemCatalog.WeightFor, Insulation:
// ItemCatalog.InsulationFor); the rest arrive with the affordance predicates that read them.
public sealed record MaterialDefinition(
    MaterialId Id,
    string DisplayName,
    // Weight per unit of ItemDefinition.Volume, roughly "1 is water"; only the scale relative
    // to carry capacity matters.
    float Density = 0f,
    // Cold insulation for anyone carrying anything made of this (see WorldState.Advance).
    // Presence, not count: a second hide does not warm twice as much.
    float Insulation = 0f);
