namespace ManyWinters.Core.Continuity;

public readonly record struct GraveId(Guid Value)
{
    public static GraveId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
