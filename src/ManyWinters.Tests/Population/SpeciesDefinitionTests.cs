using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;

namespace ManyWinters.Tests.Population;

public class SpeciesDefinitionTests
{
    private static readonly LifeCycle LifeCycle = new(WeaningAgeYears: 1, AdultAgeYears: 4, ElderAgeYears: 7, MaxLifespanYears: 10);
    private static readonly MaterialId Apple = new("apple");
    private static readonly MaterialId Grass = new("grass");

    [Fact]
    public void DigestibilityOfAMaterialInTheDietIsWhatWasGiven()
    {
        var species = new SpeciesDefinition(Person.HumanSpecies, "Human", LifeCycle, [new SpeciesDefinition.DietEntry(Apple, 0.5f)]);

        Assert.Equal(0.5f, species.DigestibilityOf(Apple));
    }

    [Fact]
    public void DigestibilityOfAMaterialNotInTheDietIsZero()
    {
        var species = new SpeciesDefinition(Person.HumanSpecies, "Human", LifeCycle, [new SpeciesDefinition.DietEntry(Apple, 1f)]);

        Assert.Equal(0f, species.DigestibilityOf(Grass));
    }

    [Fact]
    public void ANullDietNormalizesToEmptyRatherThanNull()
    {
        var species = new SpeciesDefinition(Person.HumanSpecies, "Human", LifeCycle);

        Assert.Empty(species.Diet);
        Assert.Equal(0f, species.DigestibilityOf(Apple));
    }

    // The winter reserve: a species with no opinion runs at exactly the human rate.
    [Fact]
    public void HungerPerTickMultiplierDefaultsToOne()
    {
        var species = new SpeciesDefinition(Person.HumanSpecies, "Human", LifeCycle);

        Assert.Equal(1f, species.HungerPerTickMultiplier);
    }

    // A species with no opinion never flees, which is what keeps a human out of
    // WorldState.DecideIdleTask's flee check entirely.
    [Fact]
    public void FleeDefaultsToNull()
    {
        var species = new SpeciesDefinition(Person.HumanSpecies, "Human", LifeCycle);

        Assert.Null(species.Flee);
    }
}
