using ManyWinters.Core.Materials;

namespace ManyWinters.Core.Items;

// What an item is made of and what shape it has. Weight and insulation derive from the
// material (see ItemCatalog.WeightFor / InsulationFor), so a substance's properties are stated
// once.
public sealed record ItemDefinition(
    ItemKindId Id,
    string DisplayName,
    MaterialId Material,
    FormId Form,
    // Bulk of one unit, in arbitrary units; weight is this times the material's density. 0 for
    // anything that should not gate carry capacity.
    float Volume = 0f,
    // Needs.Hunger relieved per unit eaten (see EatCommand); 0 means "not food". Per item, not
    // per material: nothing reads a material's nutrition yet.
    float HungerRestoredPerUnit = 0f,
    // Flat carry-capacity bonus for having this kind at all (see WorldState.MaxCarryWeightFor) -
    // presence, not count. A property of the shape, not the substance, so it stays on the item.
    float CarryCapacityBonus = 0f,
    // What working this item leaves behind, one entry per verb it answers to (see
    // FormTransition). Null for anything nothing can be done to yet.
    IReadOnlyList<FormTransition>? Transitions = null);
