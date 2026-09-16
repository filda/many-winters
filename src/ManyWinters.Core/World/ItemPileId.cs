namespace ManyWinters.Core.World;

public readonly record struct ItemPileId(Guid Value)
{
    public static ItemPileId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
