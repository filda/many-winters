namespace ManyWinters.Core.Knowledge;

public static class SkillTechniques
{
    public static Technique EfficientTechniqueFor(SkillType skill) => skill switch
    {
        SkillType.Foraging => Technique.EfficientForaging,
        SkillType.MushroomForaging => Technique.EfficientMushroomForaging,
        SkillType.RootDigging => Technique.EfficientRootDigging,
        _ => throw new ArgumentOutOfRangeException(nameof(skill), skill, "Unknown skill type."),
    };
}
