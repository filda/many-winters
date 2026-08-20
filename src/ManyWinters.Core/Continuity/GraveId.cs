using System.Globalization;

namespace ManyWinters.Core.Continuity;

public readonly record struct GraveId(int Value)
{
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
