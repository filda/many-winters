using ManyWinters.Core.Knowledge;

namespace ManyWinters.Core.World;

public sealed record ResourceDefinition(ResourceKindId Id, string DisplayName, SkillTypeId Skill);
