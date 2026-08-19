using ManyWinters.Core.Items;
using ManyWinters.Core.World;

namespace ManyWinters.Core.Construction;

public sealed class Building
{
    public required BuildingId Id { get; init; }

    public required BuildingKindId Kind { get; init; }

    public Position Position { get; set; }

    public float Condition { get; set; } = 100f;

    public Inventory Inventory { get; init; } = new();
}
