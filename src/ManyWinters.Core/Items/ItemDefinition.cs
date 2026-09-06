using ManyWinters.Core.Materials;

namespace ManyWinters.Core.Items;

// What an item is made of and what shape it has been worked into. Weight and insulation used to
// be typed onto each item here; they are now derived from the material (see
// ItemCatalog.WeightFor / InsulationFor), so a substance's properties are stated once and every
// item made of it follows, rather than each item carrying its own copy of the same truth.
public sealed record ItemDefinition(
    ItemKindId Id,
    string DisplayName,
    MaterialId Material,
    FormId Form,
    // How much substance one unit is, in arbitrary bulk units - weight is this times the
    // material's density. 0 for anything that shouldn't gate carry capacity on its own.
    float Volume = 0f,
    // How much Needs.Hunger a single unit relieves when eaten (see EatCommand) - 0 for
    // anything that isn't food, which also doubles as "is this item food at all". Still stated
    // per item rather than derived from the material: nothing reads a material's nutrition yet.
    float HungerRestoredPerUnit = 0f,
    // A flat bonus to a person's carry capacity (see WorldState.MaxCarryWeightFor) for simply
    // having this kind in their inventory - a basket or a bag, not something that stacks with
    // more copies of itself. A property of the shape rather than the substance, so it stays
    // here even once more of this record has moved to the material.
    float CarryCapacityBonus = 0f);
