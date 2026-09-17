namespace ManyWinters.Core.World;

// Replaces the old per-kind ResourceNodeId/ItemPileId/BuildingId: every map Entity (a growing
// resource, a dropped pile, a building) draws one of these the same way (see IdGeneration).
public readonly record struct EntityId(Guid Value)
{
    public static EntityId New() => new(Guid.NewGuid());

    public static EntityId New(Random rng) => new(IdGeneration.NextGuid(rng));

    public int Seed => IdGeneration.SeedOf(Value);

    public override string ToString() => Value.ToString();
}
