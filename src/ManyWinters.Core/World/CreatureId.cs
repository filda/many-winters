namespace ManyWinters.Core.World;

public readonly record struct CreatureId(Guid Value)
{
    public static CreatureId New() => new(Guid.NewGuid());

    public static CreatureId New(Random rng) => new(IdGeneration.NextGuid(rng));

    public int Seed => IdGeneration.SeedOf(Value);

    public override string ToString() => Value.ToString();
}
