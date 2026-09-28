using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;
using ManyWinters.Tests.TestSupport;

namespace ManyWinters.Tests.World;

// Nutrition is the item's own, digestibility the species', and this is the one place the two
// are combined.
public class WorldStateFoodTests
{
    [Fact]
    public void AtFullDigestibilityAnItemRestoresExactlyItsOwnNutrition()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

        Assert.Equal(1f, world.HungerRestoredPerUnitFor(person, TestCatalogs.AppleItem));
    }

    [Fact]
    public void HalfDigestibilityHalvesWhatTheItemWouldOtherwiseRestore()
    {
        var species = new SpeciesDefinition(Person.HumanSpecies, "Human", TestCatalogs.HumanLifeCycle,
            [new SpeciesDefinition.DietEntry(new MaterialId("apple"), 0.5f)])
        { CollisionRadius = TestCatalogs.HumanCollisionRadius, HungerPerTickMultiplier = TestCatalogs.HumanHungerPerTickMultiplier };
        var configuration = TestCatalogs.CreateConfigurationWithSpecies(species);
        var world = new WorldState(configuration);
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

        Assert.Equal(0.5f, world.HungerRestoredPerUnitFor(person, TestCatalogs.AppleItem));
    }

    [Fact]
    public void AMaterialOutsideTheDietRestoresNothing()
    {
        var species = new SpeciesDefinition(Person.HumanSpecies, "Human", TestCatalogs.HumanLifeCycle) { CollisionRadius = TestCatalogs.HumanCollisionRadius, HungerPerTickMultiplier = TestCatalogs.HumanHungerPerTickMultiplier };
        var configuration = TestCatalogs.CreateConfigurationWithSpecies(species);
        var world = new WorldState(configuration);
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

        Assert.Equal(0f, world.HungerRestoredPerUnitFor(person, TestCatalogs.AppleItem));
    }

    [Fact]
    public void AnItemWithNoNutritionOfItsOwnRestoresNothingEvenAtFullDigestibility()
    {
        var world = TestCatalogs.CreateWorld();
        var person = world.SpawnPerson("Ava", new Position(0, 0), initialAgeTicks: TestCatalogs.AdultAgeTicks);

        Assert.Equal(0f, world.HungerRestoredPerUnitFor(person, TestCatalogs.WoodItem));
    }
}
