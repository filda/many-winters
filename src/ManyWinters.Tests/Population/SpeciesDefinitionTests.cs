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
}
