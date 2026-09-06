using ManyWinters.Core.Items;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Construction;

public sealed class Building
{
    public BuildingId Id { get; init; } = BuildingId.New();

    public required BuildingKindId Kind { get; init; }

    public Position Position { get; init; }

    public float Condition { get; set; } = 100f;

    public Inventory Inventory { get; } = new();
}
