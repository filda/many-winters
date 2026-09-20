using ManyWinters.Core.Continuity;
using ManyWinters.Core.Materials;

namespace ManyWinters.Tests.Continuity;

// The words a band has for the things it makes. Nothing is named in advance: a shape nobody has
// a word for is described by what it is made of, and a word is something people earn by making
// the thing (see docs/materials-and-crafting-architecture.md section 8).
public class VocabularyTests
{
    private static readonly MaterialId Stone = new("stone");
    private static readonly MaterialId Wood = new("wood");
    private static readonly MaterialId Flint = new("flint");

    private static readonly FormId Wedge = new("wedge");
    private static readonly FormId Shaft = new("shaft");
    private static readonly FormId Cord = new("cord");

    private static Assembly.Joined Axe(MaterialId head) =>
        new Assembly.Joined(0.8f, 1f, new Assembly.Part(head, Wedge, 1f, 3f), new Assembly.Part(Wood, Shaft, 1f, 4f));

    [Fact]
    public void ABandStartsWithNoWordForAnything()
    {
        var vocabulary = new Vocabulary();

        Assert.False(vocabulary.HasAWordFor(Axe(Stone)));
        Assert.Null(vocabulary.WordFor(Axe(Stone)));
    }

    [Fact]
    public void OnceNamedAThingIsCalledThat()
    {
        var vocabulary = new Vocabulary();

        vocabulary.Name(Axe(Stone), "axe");

        Assert.Equal("axe", vocabulary.WordFor(Axe(Stone)));
    }

    // The word is for the shape, not for the substance: an axe is an axe whether its head is
    // stone or flint, which is how the band gets the word for free on everything it makes that
    // way afterwards - including out of substances nobody had then heard of.
    [Fact]
    public void TheWordCoversTheSameShapeInAnySubstance()
    {
        var vocabulary = new Vocabulary();
        vocabulary.Name(Axe(Stone), "axe");

        Assert.Equal("axe", vocabulary.WordFor(Axe(Flint)));
    }

    [Fact]
    public void ADifferentShapeIsADifferentThingAndHasNoWordYet()
    {
        var vocabulary = new Vocabulary();
        vocabulary.Name(Axe(Stone), "axe");

        var cord = new Assembly.Part(Wood, Cord, 0.5f, 1f);

        Assert.Null(vocabulary.WordFor(cord));
    }

    // Lashing a head to a haft and a haft to a head is the same kind of thing, and a band does
    // not earn two words for it.
    [Fact]
    public void WhichWayRoundItWasTiedDoesNotMakeItAnotherThing()
    {
        var vocabulary = new Vocabulary();
        var head = new Assembly.Part(Stone, Wedge, 1f, 3f);
        var haft = new Assembly.Part(Wood, Shaft, 1f, 4f);
        vocabulary.Name(new Assembly.Joined(0.8f, 1f, head, haft), "axe");

        Assert.Equal("axe", vocabulary.WordFor(new Assembly.Joined(0.8f, 1f, haft, head)));
    }

    // How well it was made is not what it is: a botched axe is still an axe.
    [Fact]
    public void HowWellItWasMadeDoesNotChangeWhatItIs()
    {
        var vocabulary = new Vocabulary();
        vocabulary.Name(Axe(Stone), "axe");

        var botched = new Assembly.Joined(0.1f, 1f, new Assembly.Part(Stone, Wedge, 0.2f, 3f), new Assembly.Part(Wood, Shaft, 0.3f, 4f));

        Assert.Equal("axe", vocabulary.WordFor(botched));
    }

    // Depth tells things apart: a thing with another thing lashed to it is not the first thing.
    [Fact]
    public void SomethingBuiltOnTopOfItIsNotTheSameThing()
    {
        var vocabulary = new Vocabulary();
        vocabulary.Name(Axe(Stone), "axe");

        var axeWithSomethingElseLashedOn = new Assembly.Joined(0.8f, 1f, Axe(Stone), new Assembly.Part(Stone, Wedge, 1f, 3f));

        Assert.Null(vocabulary.WordFor(axeWithSomethingElseLashedOn));
    }

    // Renaming is the band's own business: they changed their minds about what to call it.
    [Fact]
    public void ABandMayChangeItsMindAboutWhatToCallSomething()
    {
        var vocabulary = new Vocabulary();
        vocabulary.Name(Axe(Stone), "chopper");

        vocabulary.Name(Axe(Stone), "axe");

        Assert.Equal("axe", vocabulary.WordFor(Axe(Stone)));
        Assert.Single(vocabulary.Words);
    }

    [Fact]
    public void ARestoredWordIsTheWordTheBandHad()
    {
        var vocabulary = new Vocabulary();
        var signature = AssemblyPattern.SignatureOf(Axe(Stone));

        vocabulary.Restore(signature, "axe");

        Assert.Equal("axe", vocabulary.WordFor(Axe(Flint)));
    }
}
