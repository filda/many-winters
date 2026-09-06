using System.Text.Json.Serialization;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Materials;

// The shape a material has been worked into - fibre, stick, lump, wedge, vessel - as opposed to
// the substance itself (MaterialId). The pair is what an item is: an axe is a wedge of a hard
// material, cord is a fibrous material twisted into a cord, and a stone lump cannot cut where a
// stone wedge can.
//
// Nothing reads a form yet. It is declared now so that content already says what shape each
// item is by the time the affordance predicates that ask arrive, rather than every item file
// needing a second pass then.
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
