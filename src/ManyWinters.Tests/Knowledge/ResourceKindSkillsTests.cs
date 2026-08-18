using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Knowledge;

public class ResourceKindSkillsTests
{
    [Theory]
    [InlineData(ResourceKind.Apple, SkillType.Foraging)]
    [InlineData(ResourceKind.Pear, SkillType.Foraging)]
    [InlineData(ResourceKind.Mushroom, SkillType.MushroomForaging)]
    [InlineData(ResourceKind.Potato, SkillType.RootDigging)]
    public void SkillForReturnsTheExpectedSkill(ResourceKind kind, SkillType expected)
    {
        Assert.Equal(expected, ResourceKindSkills.SkillFor(kind));
    }

    [Fact]
    public void SkillForThrowsForAnUnknownResourceKind()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ResourceKindSkills.SkillFor((ResourceKind)999));

        Assert.Contains("resource kind", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
