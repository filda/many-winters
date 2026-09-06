using ManyWinters.Core.World;

namespace ManyWinters.Core.Construction;

public readonly record struct BuildingId(Guid Value)
{
    public static BuildingId New() => new(Guid.NewGuid());

    public int Seed => EntityId.SeedOf(Value);

    public override string ToString() => Value.ToString();
}
