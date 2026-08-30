namespace ManyWinters.Core.Items;

public sealed record ItemDefinition(
    ItemKindId Id,
    string DisplayName,
    float Insulation = 0f,
    // How much a single unit adds to a person's total carry weight (Person.MaxCarryWeight) -
    // 0 for anything that shouldn't gate capacity on its own (nothing currently needs that,
    // but unlike Insulation, most physical items *do* have some weight, so this has no
    // implicit "usually zero" assumption behind its default).
    float Weight = 0f,
    // How much Needs.Hunger a single unit relieves when eaten (see EatCommand) - 0 for
    // anything that isn't food, which also doubles as "is this item food at all".
    float HungerRestoredPerUnit = 0f,
    // A flat bonus to a person's carry capacity (see WorldState.MaxCarryWeightFor) for simply
    // having this kind in their inventory - a basket or a bag, not something that stacks with
    // more copies of itself (same "presence, not count" convention as Insulation).
    float CarryCapacityBonus = 0f);
