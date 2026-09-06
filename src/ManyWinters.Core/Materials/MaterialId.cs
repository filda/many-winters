using System.Text.Json.Serialization;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Materials;

[JsonConverter(typeof(MaterialIdJsonConverter))]
public readonly record struct MaterialId(string Value)
{
    public override string ToString() => Value;
}

public sealed class MaterialIdJsonConverter : StringWrapperJsonConverter<MaterialId>
{
    protected override MaterialId Create(string value) => new(value);

    protected override string GetValue(MaterialId instance) => instance.Value;
}
