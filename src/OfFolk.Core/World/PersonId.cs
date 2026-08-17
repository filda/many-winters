using System.Globalization;

namespace OfFolk.Core.World;

public readonly record struct PersonId(int Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
