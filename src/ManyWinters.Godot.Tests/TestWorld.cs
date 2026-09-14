using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Tests;

// A small world with catalogs, for the presentation logic that reads one (PersonActions,
// SelectionCard). ManyWinters.Tests has a fuller mirror of Content in its own TestCatalogs;
// this is a deliberate copy rather than a shared one, because the two test projects answer to
// different rules (see README.md) and only this side may not touch the engine.
internal static class TestWorld
{
    internal static readonly ItemKindId Wood = new("wood");
    internal static readonly ItemKindId Apple = new("apple");
    private static readonly ItemKindId Axe = new("axe");

    internal static readonly ResourceKindId AppleTree = new("apple");
    private static readonly ResourceKindId Stump = new("tree_stump");

    private static readonly SkillTypeId Foraging = new("foraging");
    internal static readonly TechniqueId BasicForaging = new("basic_foraging");
    private static readonly TechniqueId BasicEating = new("basic_eating");

    private static readonly BuildingKindId StorageHut = new("storage_hut");

    private const int AxeInputAmount = 5;
    private const int StorageHutInputAmount = 20;

    internal static WorldState Create()
    {
        var materials = new MaterialCatalog([
            new MaterialDefinition(new MaterialId("wood"), "Wood", 0.5f),
            new MaterialDefinition(new MaterialId("apple"), "Apple Flesh", 1f),
            new MaterialDefinition(new MaterialId("stone"), "Stone", 2f),
        ]);

        var items = new ItemCatalog(
            [
                new ItemDefinition(Wood, "Wood", new MaterialId("wood"), new FormId("stick"), 2f),
                new ItemDefinition(Apple, "Apple", new MaterialId("apple"), new FormId("whole"), 1f, 1f),
                new ItemDefinition(Axe, "Axe", new MaterialId("stone"), new FormId("wedge"), 2.5f),
            ],
            materials);

        return new WorldState(new WorldConfiguration(
            new ResourceCatalog([
                new ResourceDefinition(AppleTree, "Apple", Foraging, Apple, CanFell: true, FellLeaves: [new(new ResourceKindId("wood"), 30f)]),
                new ResourceDefinition(Stump, "Tree Stump", Foraging, Wood),
            ]),
            new SkillCatalog([
                new SkillDefinition(Foraging, "Foraging", BasicForaging, new TechniqueId("efficient_foraging")),
                new SkillDefinition(new SkillTypeId("eating"), "Eating", BasicEating, new TechniqueId("efficient_eating")),
                new SkillDefinition(new SkillTypeId("burial"), "Burial", new TechniqueId("basic_burial"), new TechniqueId("efficient_burial")),
            ]),
            new RecipeCatalog([new RecipeDefinition(Axe, Wood, AxeInputAmount)]),
            new BuildingCatalog([new BuildingDefinition(StorageHut, "Storage Hut", Wood, StorageHutInputAmount)]),
            materials,
            items,
            SeasonParameters.Default,
            SimulationRules.Default));
    }

    // Born to two named parents, for the card that says whose child somebody is.
    internal static Person AddChildOf(WorldState world, string name, Person mother, Person father)
    {
        var child = new Person
        {
            Name = name,
            Position = mother.Position,
            BirthTick = world.Clock.CurrentTick,
            Mother = mother,
            Father = father,
            Sex = Sex.Male,
        };

        world.AddPerson(child);
        return child;
    }

    // Grown, so nothing in a test is refused merely for being a child.
    internal static Person AddAdult(WorldState world, string name, Position position, Sex sex = Sex.Female)
    {
        var person = new Person
        {
            Name = name,
            Position = position,
            BirthTick = world.Clock.CurrentTick - (SimulationRules.Default.TicksPerYear * LifeStages.AdultAgeYears),
            Mother = Person.Unknown,
            Father = Person.Unknown,
            Sex = sex,
        };

        world.AddPerson(person);
        return person;
    }
}
