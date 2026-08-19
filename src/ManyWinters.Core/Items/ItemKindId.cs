using System.Text.Json.Serialization;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Items;

[JsonConverter(typeof(ItemKindIdJsonConverter))]
public readonly record struct ItemKindId(string Value)
{
    public override string ToString() => Value;
}

public sealed class ItemKindIdJsonConverter : StringWrapperJsonConverter<ItemKindId>
{
    protected override ItemKindId Create(string value) => new(value);

    protected override string GetValue(ItemKindId instance) => instance.Value;
}
