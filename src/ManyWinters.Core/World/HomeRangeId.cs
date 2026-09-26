namespace ManyWinters.Core.World;

// Same shape as CreatureId/EntityId: drawn once by whoever creates the HomeRange, never handed
// out by a registry, so nothing has to be counted or saved to keep them unique.
public readonly record struct HomeRangeId(Guid Value)
{
    public static HomeRangeId New() => new(Guid.NewGuid());

    public static HomeRangeId New(Random rng) => new(IdGeneration.NextGuid(rng));

    public int Seed => IdGeneration.SeedOf(Value);

    public override string ToString() => Value.ToString();
}
