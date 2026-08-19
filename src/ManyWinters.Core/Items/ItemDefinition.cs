namespace ManyWinters.Core.Items;

public sealed record ItemDefinition(ItemKindId Id, string DisplayName, float Insulation = 0f);
