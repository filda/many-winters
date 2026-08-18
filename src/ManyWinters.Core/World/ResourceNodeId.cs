using System.Globalization;

namespace ManyWinters.Core.World;

public readonly record struct ResourceNodeId(int Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
