using System.Text.Json.Serialization;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Knowledge;

[JsonConverter(typeof(TechniqueIdJsonConverter))]
public readonly record struct TechniqueId(string Value)
{
    public override string ToString() => Value;
}

public sealed class TechniqueIdJsonConverter : StringWrapperJsonConverter<TechniqueId>
{
    protected override TechniqueId Create(string value) => new(value);

    protected override string GetValue(TechniqueId instance) => instance.Value;
}
