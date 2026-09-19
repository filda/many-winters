using ManyWinters.Core.Materials;

namespace ManyWinters.Tests.Materials;

public class MaterialAffordancesTests
{
    private static readonly MaterialId Id = new("test_material");

    private static MaterialDefinition With(
        float hardness = 0f,
        float toughness = 0f,
        float flexibility = 0f,
        float elasticity = 0f,
        float fibrousness = 0f) =>
        new(Id, "Test Material", Hardness: hardness, Toughness: toughness, Flexibility: flexibility, Elasticity: elasticity, Fibrousness: fibrousness);

    [Fact]
    public void GrassLikePropertiesCanTwist()
    {
        var grass = With(fibrousness: 0.8f, flexibility: 0.6f);

        Assert.True(MaterialAffordances.CanTwist(grass));
    }

    [Fact]
    public void CanTwistNeedsBothFibrousnessAndFlexibility()
    {
        var fibrousButStiff = With(fibrousness: 0.8f, flexibility: 0.2f);
        var flexibleButNotFibrous = With(fibrousness: 0.2f, flexibility: 0.8f);

        Assert.False(MaterialAffordances.CanTwist(fibrousButStiff));
        Assert.False(MaterialAffordances.CanTwist(flexibleButNotFibrous));
    }

    [Fact]
    public void FlintLikePropertiesCanKnap()
    {
        var flint = With(hardness: 0.9f, toughness: 0.1f);

        Assert.True(MaterialAffordances.CanKnap(flint));
    }

    [Fact]
    public void CanKnapNeedsBothHardnessAndBrittleness()
    {
        var hardButTough = With(hardness: 0.9f, toughness: 0.8f);
        var brittleButSoft = With(hardness: 0.2f, toughness: 0.1f);

        Assert.False(MaterialAffordances.CanKnap(hardButTough));
        Assert.False(MaterialAffordances.CanKnap(brittleButSoft));
    }

    [Fact]
    public void ABrittleMaterialCanBeCrushed()
    {
        var brittle = With(toughness: 0.1f);

        Assert.True(MaterialAffordances.CanCrush(brittle));
    }

    [Fact]
    public void AToughMaterialCannotBeCrushed()
    {
        var tough = With(toughness: 0.6f);

        Assert.False(MaterialAffordances.CanCrush(tough));
    }

    [Fact]
    public void AFlexibleMaterialCanBend()
    {
        var flexible = With(flexibility: 0.6f);

        Assert.True(MaterialAffordances.CanBend(flexible));
    }

    [Fact]
    public void AStiffMaterialCannotBend()
    {
        var stiff = With(flexibility: 0.2f);

        Assert.False(MaterialAffordances.CanBend(stiff));
    }

    [Fact]
    public void AnElasticMaterialHoldsTension()
    {
        var elastic = With(elasticity: 0.7f);

        Assert.True(MaterialAffordances.HoldsTension(elastic));
    }

    [Fact]
    public void APliableButInelasticMaterialDoesNotHoldTension()
    {
        // Hide bends (Flexibility) but does not spring back (Elasticity) - the distinction the
        // property split exists to make.
        var hide = With(flexibility: 0.8f, elasticity: 0.1f);

        Assert.False(MaterialAffordances.HoldsTension(hide));
    }

    [Fact]
    public void AMaterialDescribedWithNoPropertiesAffordsNothing()
    {
        var undescribed = With();

        Assert.False(MaterialAffordances.CanTwist(undescribed));
        Assert.False(MaterialAffordances.CanKnap(undescribed));
        Assert.False(MaterialAffordances.CanBend(undescribed));
        Assert.False(MaterialAffordances.HoldsTension(undescribed));
        // CanCrush is the one predicate a bare-zero material passes - Toughness 0 is brittle by
        // definition, not "undescribed". Documented here rather than left to look like an
        // oversight.
        Assert.True(MaterialAffordances.CanCrush(undescribed));
    }
}
