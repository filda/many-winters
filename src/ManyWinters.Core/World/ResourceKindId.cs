using System.Text.Json.Serialization;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.World;

[JsonConverter(typeof(ResourceKindIdJsonConverter))]
public readonly record struct ResourceKindId(string Value)
{
    public override string ToString() => Value;
}

public sealed class ResourceKindIdJsonConverter : StringWrapperJsonConverter<ResourceKindId>
{
    protected override ResourceKindId Create(string value) => new(value);

    protected override string GetValue(ResourceKindId instance) => instance.Value;
}
