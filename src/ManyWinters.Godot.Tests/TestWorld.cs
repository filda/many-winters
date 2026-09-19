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

    internal static readonly EntityKindId AppleTree = new("apple");
    internal static readonly EntityKindId Stump = new("tree_stump");

    private static readonly SkillTypeId Foraging = new("foraging");
    private static readonly SkillTypeId Teaching = new("teaching");
    internal static readonly TechniqueId BasicForaging = new("basic_foraging");
    internal static readonly TechniqueId EfficientForaging = new("efficient_foraging");
    private static readonly TechniqueId BasicEating = new("basic_eating");
    internal static readonly TechniqueId BasicTeaching = new("basic_teaching");

    private static readonly EntityKindId StorageHut = new("storage_hut");
    private static readonly ItemKindId StorageHutItem = new("storage_hut");

    internal const int AxeInputAmount = 5;
    internal const int StorageHutInputAmount = 20;
    // Deliberately far beyond any realistic carry capacity (see MakeCommand): what routes it
    // into the world instead of the maker's pack.
    private const float StorageHutVolume = 200f;

    internal static WorldState Create()
    {
        var materials = new MaterialCatalog([
            new MaterialDefinition(new MaterialId("wood"), "Wood", 0.5f),
            new MaterialDefinition(new MaterialId("apple"), "Apple Flesh", 1f),
            new MaterialDefinition(new MaterialId("stone"), "Stone", 2f),
        ]);

        var forms = new FormCatalog([
            new FormDefinition(new FormId("stick"), "Stick"),
            new FormDefinition(new FormId("whole"), "Whole"),
            new FormDefinition(new FormId("wedge"), "Wedge", EdgeSharpness: 1f),
            new FormDefinition(new FormId("shelter"), "Shelter"),
        ]);

        var items = new ItemCatalog(
            [
                new ItemDefinition(Wood, "Wood", new MaterialId("wood"), new FormId("stick"), 2f),
                new ItemDefinition(Apple, "Apple", new MaterialId("apple"), new FormId("whole"), 1f, 1f),
                new ItemDefinition(Axe, "Axe", new MaterialId("stone"), new FormId("wedge"), 2.5f),
                new ItemDefinition(StorageHutItem, "Storage Hut", new MaterialId("wood"), new FormId("shelter"), StorageHutVolume),
            ],
            materials,
            forms);

        return new WorldState(new WorldConfiguration(
            new ResourceCatalog([
                new ResourceDefinition(AppleTree, "Apple", Foraging, Apple, CanFell: true, FellLeaves: [new(new EntityKindId("wood"), 30f)]),
                new ResourceDefinition(Stump, "Tree Stump", Foraging, Wood),
            ]),
            new SkillCatalog([
                new SkillDefinition(Foraging, "Foraging", BasicForaging, EfficientForaging),
                new SkillDefinition(new SkillTypeId("eating"), "Eating", BasicEating, new TechniqueId("efficient_eating")),
                new SkillDefinition(new SkillTypeId("burial"), "Burial", new TechniqueId("basic_burial"), new TechniqueId("efficient_burial")),
                new SkillDefinition(Teaching, "Teaching", BasicTeaching, new TechniqueId("efficient_teaching")),
            ]),
            new RecipeCatalog([
                new RecipeDefinition(Axe, Wood, AxeInputAmount),
                new RecipeDefinition(StorageHutItem, Wood, StorageHutInputAmount),
            ]),
            materials,
            forms,
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

    // A hut to put things into and take them back out of, for the menu a store offers.
    internal static Entity AddStorageHut(WorldState world, Position position)
    {
        var building = new Entity
        {
            Kind = StorageHut,
            Category = EntityCategory.Building,
            Position = position,
            Condition = 100f,
            Storage = new Inventory(),
        };
        world.AddEntity(building);
        return building;
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
