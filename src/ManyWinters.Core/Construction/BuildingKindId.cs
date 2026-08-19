using System.Text.Json.Serialization;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Construction;

[JsonConverter(typeof(BuildingKindIdJsonConverter))]
public readonly record struct BuildingKindId(string Value)
{
    public override string ToString() => Value;
}

public sealed class BuildingKindIdJsonConverter : StringWrapperJsonConverter<BuildingKindId>
{
    protected override BuildingKindId Create(string value) => new(value);

    protected override string GetValue(BuildingKindId instance) => instance.Value;
}
