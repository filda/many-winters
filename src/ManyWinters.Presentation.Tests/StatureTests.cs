using ManyWinters.Core.Population;
using ManyWinters.Presentation.Logic;

namespace ManyWinters.Presentation.Tests;

public class StatureTests
{
    private static readonly LifeCycle Human = new(1, 4, 7, 9);
    private static readonly LifeCycle Deer = new(1, 2, 8, 12);

    [Fact]
    public void ANewbornIsDrawnAtTheNewbornScale()
    {
        Assert.Equal(Stature.NewbornScale, Stature.ScaleFor(0, Human), 10);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5.5)]
    [InlineData(40)]
    public void AnAdultAndAnyoneOlderIsDrawnAtFullSize(double age)
    {
        Assert.Equal(1.0, Stature.ScaleFor(age, Human), 10);
    }

    [Fact]
    public void HalfwayToAdulthoodIsHalfwayBetweenNewbornAndFullSize()
    {
        Assert.Equal((Stature.NewbornScale + 1) / 2, Stature.ScaleFor(2, Human), 10);
        Assert.Equal((Stature.NewbornScale + 1) / 2, Stature.ScaleFor(1, Deer), 10);
    }

    [Fact]
    public void GrowthIsStrictlyIncreasingThroughoutChildhood()
    {
        var previous = Stature.ScaleFor(0, Human);
        for (var age = 0.25; age <= 4; age += 0.25)
        {
            var scale = Stature.ScaleFor(age, Human);
            Assert.True(scale > previous);
            previous = scale;
        }
    }

    // Whole winters lived, so a child of three and a half is still a child and turns adult
    // exactly at the adult age - the same answer the simulation gives.
    [Theory]
    [InlineData(0, LifeStage.Infant)]
    [InlineData(0.99, LifeStage.Infant)]
    [InlineData(1, LifeStage.Child)]
    [InlineData(3.5, LifeStage.Child)]
    [InlineData(4, LifeStage.Adult)]
    [InlineData(7.2, LifeStage.Elder)]
    public void TheStageIsThatOfTheWholeWintersLived(double age, LifeStage expected)
    {
        Assert.Equal(expected, Stature.StageAt(age, Human));
    }

    [Fact]
    public void ASpeciesThatIsGrownAtBirthDoesNotDivideByZero()
    {
        var instant = new LifeCycle(0, 0, 5, 9);

        Assert.Equal(1.0, Stature.ScaleFor(1, instant), 10);
        Assert.Equal(Stature.NewbornScale, Stature.ScaleFor(0, instant), 10);
    }
}
