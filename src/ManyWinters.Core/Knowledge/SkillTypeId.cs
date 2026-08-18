using System.Text.Json.Serialization;
using ManyWinters.Core.Serialization;

namespace ManyWinters.Core.Knowledge;

[JsonConverter(typeof(SkillTypeIdJsonConverter))]
public readonly record struct SkillTypeId(string Value)
{
    public override string ToString() => Value;
}

public sealed class SkillTypeIdJsonConverter : StringWrapperJsonConverter<SkillTypeId>
{
    protected override SkillTypeId Create(string value) => new(value);

    protected override string GetValue(SkillTypeId instance) => instance.Value;
}
