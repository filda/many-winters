using System.Text.Json.Serialization;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.World;

// Replaces the old per-domain ResourceKindId/BuildingKindId: a growable resource and a building
// both name their kind this way now. ItemKindId stays separate - it names a thing once it is
// carried/stored/required, not a thing on the map.
[JsonConverter(typeof(EntityKindIdJsonConverter))]
public readonly record struct EntityKindId(string Value)
{
    public override string ToString() => Value;
}

public sealed class EntityKindIdJsonConverter : StringWrapperJsonConverter<EntityKindId>
{
    protected override EntityKindId Create(string value) => new(value);

    protected override string GetValue(EntityKindId instance) => instance.Value;
}
