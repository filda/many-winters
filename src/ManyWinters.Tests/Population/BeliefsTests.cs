using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;

namespace ManyWinters.Tests.Population;

// What one person takes a substance to be, which is not the same as what it is (see
// docs/materials-and-crafting-architecture.md section 7).
public class BeliefsTests
{
    private static readonly MaterialId Grass = new("plant_fibre");
    private static readonly MaterialId Stone = new("stone");

    private static readonly MaterialDefinition ActualGrass =
        new(Grass, "Plant Fibre", Density: 0.2f, Toughness: 0.5f, Flexibility: 0.7f, Fibrousness: 0.9f);

    [Fact]
    public void SomebodyWhoHasHandledNothingBelievesNothing()
    {
        var beliefs = new Beliefs();

        Assert.False(beliefs.HoldsAnythingAbout(Grass));
        Assert.Empty(beliefs.Held);
    }

    // An inkling is not knowledge: what has been noticed once is not yet firm enough to act on.
    [Fact]
    public void AnInklingIsNotYetSomethingToActOn()
    {
        var beliefs = new Beliefs();

        beliefs.Learn(Grass, MaterialProperty.Fibrousness, 0.9f, confidenceGained: 0.1f);

        Assert.False(beliefs.HoldsAnythingAbout(Grass));
        Assert.Equal(0f, beliefs.AsBelieved(ActualGrass).Fibrousness);
    }

    [Fact]
    public void HandlingSomethingOftenEnoughMakesWhatWasNoticedFirm()
    {
        var beliefs = new Beliefs();

        for (var i = 0; i < 10; i++)
        {
            beliefs.Learn(Grass, MaterialProperty.Fibrousness, 0.9f, confidenceGained: 0.2f);
        }

        Assert.True(beliefs.HoldsAnythingAbout(Grass));
        Assert.Equal(0.9f, beliefs.AsBelieved(ActualGrass).Fibrousness, 5);
    }

    [Fact]
    public void ConfidenceNeverPassesCertainty()
    {
        var beliefs = new Beliefs();

        for (var i = 0; i < 100; i++)
        {
            beliefs.Learn(Grass, MaterialProperty.Fibrousness, 0.9f, confidenceGained: 0.5f);
        }

        Assert.Equal(1f, Assert.Single(beliefs.Held).Value.Confidence, 5);
    }

    // The whole point of the shape: what somebody acts on need not match the world. Nothing
    // writes a wrong belief yet, but everything that reads one already copes with it.
    [Fact]
    public void AMistakenBeliefIsWhatTheyActOnRatherThanTheTruth()
    {
        var beliefs = new Beliefs();
        beliefs.Learn(Grass, MaterialProperty.Fibrousness, 0f, confidenceGained: 1f);
        beliefs.Learn(Grass, MaterialProperty.Flexibility, 0f, confidenceGained: 1f);

        var asTheySeeIt = beliefs.AsBelieved(ActualGrass);

        Assert.True(MaterialAffordances.CanTwist(ActualGrass));
        Assert.False(MaterialAffordances.CanTwist(asTheySeeIt));
    }

    // What they have no firm belief about reads as nothing they know of, not as the truth
    // leaking through.
    [Fact]
    public void WhatTheyHaveNoBeliefAboutReadsAsBlankRatherThanAsTheTruth()
    {
        var beliefs = new Beliefs();
        beliefs.Learn(Grass, MaterialProperty.Fibrousness, 0.9f, confidenceGained: 1f);

        var asTheySeeIt = beliefs.AsBelieved(ActualGrass);

        Assert.Equal(0.9f, asTheySeeIt.Fibrousness, 5);
        Assert.Equal(0f, asTheySeeIt.Flexibility);
        Assert.Equal(0f, asTheySeeIt.Density);
    }

    // Believing something about grass says nothing about stone.
    [Fact]
    public void KnowingOneSubstanceIsNotKnowingAnother()
    {
        var beliefs = new Beliefs();
        beliefs.Learn(Grass, MaterialProperty.Fibrousness, 0.9f, confidenceGained: 1f);

        Assert.True(beliefs.HoldsAnythingAbout(Grass));
        Assert.False(beliefs.HoldsAnythingAbout(Stone));
    }

    // The freshest handling wins rather than being averaged with what came before - which is how
    // a distorted account will later be able to talk somebody round.
    [Fact]
    public void NoticingSomethingNewOverwritesWhatWasBelievedBefore()
    {
        var beliefs = new Beliefs();
        beliefs.Learn(Stone, MaterialProperty.Hardness, 1f, confidenceGained: 1f);

        beliefs.Learn(Stone, MaterialProperty.Hardness, 0.2f, confidenceGained: 0.1f);

        Assert.Equal(0.2f, Assert.Single(beliefs.Held).Value.Value, 5);
    }

    [Fact]
    public void RestoringABeliefDoesNotCountAsNoticingItAgain()
    {
        var beliefs = new Beliefs();

        beliefs.Restore(Grass, MaterialProperty.Fibrousness, 0.9f, confidence: 0.4f);

        Assert.Equal(0.4f, Assert.Single(beliefs.Held).Value.Confidence, 5);
    }
}
