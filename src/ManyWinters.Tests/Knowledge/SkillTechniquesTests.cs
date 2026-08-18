using ManyWinters.Core.Knowledge;

namespace ManyWinters.Tests.Knowledge;

public class SkillTechniquesTests
{
    [Theory]
    [InlineData(SkillType.Foraging, Technique.EfficientForaging)]
    [InlineData(SkillType.MushroomForaging, Technique.EfficientMushroomForaging)]
    [InlineData(SkillType.RootDigging, Technique.EfficientRootDigging)]
    public void EfficientTechniqueForReturnsTheExpectedTechnique(SkillType skill, Technique expected)
    {
        Assert.Equal(expected, SkillTechniques.EfficientTechniqueFor(skill));
    }

    [Fact]
    public void EfficientTechniqueForThrowsForAnUnknownSkillType()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => SkillTechniques.EfficientTechniqueFor((SkillType)999));

        Assert.Contains("skill type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
