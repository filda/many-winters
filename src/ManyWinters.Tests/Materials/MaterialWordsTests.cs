using ManyWinters.Core.Materials;

namespace ManyWinters.Tests.Materials;

// What a substance is like, in words - the only thing the player has to go on when forming a
// hypothesis at the workbench, since the numbers behind it are never shown (see
// docs/materials-and-crafting-architecture.md section 9).
public class MaterialWordsTests
{
    private static readonly MaterialId Id = new("test_material");

    private static MaterialDefinition With(
        float density = 0f,
        float hardness = 0f,
        float toughness = 0f,
        float flexibility = 0f,
        float elasticity = 0f,
        float fibrousness = 0f) =>
        new(Id, "Test Material", density, Hardness: hardness, Toughness: toughness, Flexibility: flexibility, Elasticity: elasticity, Fibrousness: fibrousness);

    [Fact]
    public void GrassReadsAsWhatItIs()
    {
        var grass = With(density: 0.2f, toughness: 0.5f, flexibility: 0.7f, fibrousness: 0.9f);

        Assert.Equal(["fibrous", "pliable", "light"], MaterialWords.For(grass));
    }

    [Fact]
    public void StoneReadsAsWhatItIs()
    {
        var stone = With(density: 2f, hardness: 1f, toughness: 0.2f);

        Assert.Equal(["hard", "brittle", "heavy"], MaterialWords.For(stone));
    }

    // Says what a thing is, never what it is for: no word here hands the player a verb.
    [Fact]
    public void NothingSaysWhatTheStuffIsGoodFor()
    {
        var grass = With(density: 0.2f, toughness: 0.5f, flexibility: 0.7f, fibrousness: 0.9f);

        Assert.DoesNotContain(MaterialWords.For(grass), word => word.Contains("twist", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ASubstanceNobodyDescribedSaysNothing()
    {
        Assert.Empty(MaterialWords.For(With()));
    }

    // Springy already tells the player it gives, and says more, so it stands in for pliable
    // rather than piling up beside it.
    [Fact]
    public void SomethingSpringyIsNotAlsoCalledPliable()
    {
        var sinew = With(flexibility: 0.8f, elasticity: 0.9f);

        Assert.Equal(["springy"], MaterialWords.For(sinew));
    }

    [Fact]
    public void SomethingThatGivesWithoutSpringingBackIsPliable()
    {
        var hide = With(flexibility: 0.8f, elasticity: 0.1f);

        Assert.Equal(["pliable"], MaterialWords.For(hide));
    }

    [Fact]
    public void ToughStuffIsCalledToughRatherThanBrittle()
    {
        var wood = With(toughness: 0.8f);

        Assert.Equal(["tough"], MaterialWords.For(wood));
    }

    // Middling stuff still has something to say about itself, or the most ordinary materials in
    // the game would describe themselves as nothing at all.
    [Fact]
    public void WoodReadsAsWhatItIs()
    {
        var wood = With(density: 0.5f, hardness: 0.4f, toughness: 0.7f, flexibility: 0.35f, fibrousness: 0.5f);

        Assert.Equal(["tough", "stiff"], MaterialWords.For(wood));
    }

    // The end of the flexibility axis a haft is chosen for.
    [Fact]
    public void SomethingThatWillNotGiveIsCalledStiff()
    {
        Assert.Contains("stiff", MaterialWords.For(With(flexibility: 0.2f)));
    }

    [Fact]
    public void SoftStuffIsSaidToBeSoft()
    {
        Assert.Contains("soft", MaterialWords.For(With(hardness: 0.2f)));
    }

    // A thing described four ways at once has told the player nothing, so the line is capped.
    [Fact]
    public void NothingIsDescribedInMoreThanThreeWords()
    {
        var everything = With(density: 3f, hardness: 1f, toughness: 0.1f, flexibility: 1f, elasticity: 1f, fibrousness: 1f);

        Assert.Equal(3, MaterialWords.For(everything).Count);
    }

    [Fact]
    public void TheSameSubstanceAlwaysReadsTheSameWay()
    {
        var stone = With(density: 2f, hardness: 1f, toughness: 0.2f);

        Assert.Equal(MaterialWords.For(stone), MaterialWords.For(stone));
    }
}
