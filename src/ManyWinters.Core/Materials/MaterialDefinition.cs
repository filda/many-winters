namespace ManyWinters.Core.Materials;

// The substance an item is made of, as opposed to its shape (FormId). Physical properties live
// here and item numbers derive from them - see docs/materials-and-crafting-architecture.md.
// Only properties something reads are defined; the rest arrive with the affordance predicates
// that read them.
public sealed record MaterialDefinition(
    MaterialId Id,
    string DisplayName,
    // Weight per unit of ItemDefinition.Volume, roughly "1 is water"; only the scale relative
    // to carry capacity matters.
    float Density = 0f,
    // Cold insulation for anyone carrying anything made of this. Presence, not count: a second
    // hide does not warm twice as much.
    float Insulation = 0f,
    // Resistance to denting/scratching, 0-1; also feeds edge-related function scores later
    // (docs/materials-and-crafting-architecture.md section 4).
    float Hardness = 0f,
    // Resistance to fracturing under stress, 0-1 - the inverse of brittleness.
    float Toughness = 0f,
    float Flexibility = 0f,
    // How much the material springs back after being stretched or bent, 0-1 - distinct from
    // Flexibility: hide is pliable but does not spring back.
    float Elasticity = 0f,
    float Fibrousness = 0f,
    // How long a thing made of this material lasts, in ticks, from the moment it came to be as a
    // thing - picked, butchered, made - wherever it lies (docs/todo/fauna-plan.md, phase 4c,
    // "kroky 4c a 4d"). Null means it never spoils: bone, wood, stone, plant_fibre and tanned hide
    // are null; meat, rawhide and the plant foods have a number. Replaces the old flat
    // `Perishable` bool - a corpse's meat now rots on meat's own clock rather than all at once
    // when the body itself decays (WorldState.IsDecayed, which stays about the body).
    long? ShelfLifeTicks = null);
