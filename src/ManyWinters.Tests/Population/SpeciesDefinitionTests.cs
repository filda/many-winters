using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.Population;

public class SpeciesDefinitionTests
{
    private static readonly LifeCycle LifeCycle = new(WeaningAgeYears: 1, AdultAgeYears: 4, ElderAgeYears: 7, MaxLifespanYears: 10);
    private static readonly MaterialId Apple = new("apple");
    private static readonly MaterialId Grass = new("grass");

    [Fact]
    public void DigestibilityOfAMaterialInTheDietIsWhatWasGiven()
    {
        var species = new SpeciesDefinition(Person.HumanSpecies, "Human", LifeCycle, [new SpeciesDefinition.DietEntry(Apple, 0.5f)]) { CollisionRadius = TestCatalogs.HumanCollisionRadius, HungerPerTickMultiplier = TestCatalogs.HumanHungerPerTickMultiplier };

        Assert.Equal(0.5f, species.DigestibilityOf(Apple));
    }

    [Fact]
    public void DigestibilityOfAMaterialNotInTheDietIsZero()
    {
        var species = new SpeciesDefinition(Person.HumanSpecies, "Human", LifeCycle, [new SpeciesDefinition.DietEntry(Apple, 1f)]) { CollisionRadius = TestCatalogs.HumanCollisionRadius, HungerPerTickMultiplier = TestCatalogs.HumanHungerPerTickMultiplier };

        Assert.Equal(0f, species.DigestibilityOf(Grass));
    }

    [Fact]
    public void ANullDietNormalizesToEmptyRatherThanNull()
    {
        var species = new SpeciesDefinition(Person.HumanSpecies, "Human", LifeCycle) { CollisionRadius = TestCatalogs.HumanCollisionRadius, HungerPerTickMultiplier = TestCatalogs.HumanHungerPerTickMultiplier };

        Assert.Empty(species.Diet);
        Assert.Equal(0f, species.DigestibilityOf(Apple));
    }

    // A species with no opinion never flees, which is what keeps a human out of
    // the idle flee check entirely.
    [Fact]
    public void FleeDefaultsToNull()
    {
        var species = new SpeciesDefinition(Person.HumanSpecies, "Human", LifeCycle) { CollisionRadius = TestCatalogs.HumanCollisionRadius, HungerPerTickMultiplier = TestCatalogs.HumanHungerPerTickMultiplier };

        Assert.Null(species.Flee);
    }
}
