using System.Text.Json.Serialization;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Population;

[JsonConverter(typeof(SpeciesIdJsonConverter))]
public readonly record struct SpeciesId(string Value)
{
    public override string ToString() => Value;
}

public sealed class SpeciesIdJsonConverter : StringWrapperJsonConverter<SpeciesId>
{
    protected override SpeciesId Create(string value) => new(value);

    protected override string GetValue(SpeciesId instance) => instance.Value;
}
