namespace ManyWinters.Core.World;

public sealed class ResourceNode
{
    public required ResourceNodeId Id { get; init; }

    public required ResourceKind Kind { get; init; }

    public Position Position { get; set; }

    public float RemainingAmount { get; set; }
}
