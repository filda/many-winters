using System.Globalization;

namespace ManyWinters.Core.Construction;

public readonly record struct BuildingId(int Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
