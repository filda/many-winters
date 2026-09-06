namespace ManyWinters.Core.World;

public readonly record struct PersonId(Guid Value)
{
    public static PersonId New() => new(Guid.NewGuid());

    public static PersonId New(Random rng) => new(EntityId.NextGuid(rng));

    public int Seed => EntityId.SeedOf(Value);

    public override string ToString() => Value.ToString();
}
