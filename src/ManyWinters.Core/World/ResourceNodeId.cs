namespace ManyWinters.Core.World;

public readonly record struct ResourceNodeId(Guid Value)
{
    public static ResourceNodeId New() => new(Guid.NewGuid());

    public static ResourceNodeId New(Random rng) => new(EntityId.NextGuid(rng));

    public int Seed => EntityId.SeedOf(Value);

    public override string ToString() => Value.ToString();
}
