using ManyWinters.Core.Items;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Construction;

public sealed record BuildingDefinition(EntityKindId Id, string DisplayName, ItemKindId RequiredItem, int RequiredAmount);
