using ManyWinters.Core.Items;

namespace ManyWinters.Core.Construction;

public sealed record BuildingDefinition(BuildingKindId Id, string DisplayName, ItemKindId RequiredItem, int RequiredAmount);
