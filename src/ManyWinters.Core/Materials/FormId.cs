using System.Text.Json.Serialization;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Materials;

// The shape a material has been worked into - fibre, stick, lump, wedge, vessel - as opposed to
// the substance (MaterialId). An axe is a wedge of a hard material; a stone lump cannot cut
// where a stone wedge can. Nothing reads a form yet; content declares it now so the affordance
// predicates that will ask do not need a second pass over every item file.
[JsonConverter(typeof(FormIdJsonConverter))]
public readonly record struct FormId(string Value)
{
    public override string ToString() => Value;
}

public sealed class FormIdJsonConverter : StringWrapperJsonConverter<FormId>
{
    protected override FormId Create(string value) => new(value);

    protected override string GetValue(FormId instance) => instance.Value;
}
