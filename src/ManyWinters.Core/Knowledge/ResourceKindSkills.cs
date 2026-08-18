using ManyWinters.Core.World;

namespace ManyWinters.Core.Knowledge;

public static class ResourceKindSkills
{
    public static SkillType SkillFor(ResourceKind kind) => kind switch
    {
        ResourceKind.Apple => SkillType.Foraging,
        ResourceKind.Pear => SkillType.Foraging,
        ResourceKind.Mushroom => SkillType.MushroomForaging,
        ResourceKind.Potato => SkillType.RootDigging,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown resource kind."),
    };
}
