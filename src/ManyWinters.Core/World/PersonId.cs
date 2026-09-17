namespace ManyWinters.Core.World;

public readonly record struct PersonId(Guid Value)
{
    public static PersonId New() => new(Guid.NewGuid());

    public static PersonId New(Random rng) => new(IdGeneration.NextGuid(rng));

    public int Seed => IdGeneration.SeedOf(Value);

    public override string ToString() => Value.ToString();
}
