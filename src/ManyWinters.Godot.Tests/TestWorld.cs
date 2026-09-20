using ManyWinters.Core.Commands;
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
    internal static readonly ItemKindId Grass = new("grass");
    private static readonly ItemKindId Axe = new("axe");
    internal static readonly ItemKindId Stone = new("stone");

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

    internal const int GrassPerCord = 3;
    internal static readonly FormId Cord = new("cord");
    internal static readonly TechniqueId BasicTwisting = new("basic_twisting");
    internal static readonly TechniqueId BasicBinding = new("basic_binding");
    internal static readonly TechniqueId BasicKnapping = new("basic_knapping");
    private static readonly FormId Wedge = new("wedge");

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
            new MaterialDefinition(new MaterialId("stone"), "Stone", 2f, Hardness: 1f, Toughness: 0.15f),
            new MaterialDefinition(new MaterialId("plant_fibre"), "Plant Fibre", 0.2f, Toughness: 0.5f, Flexibility: 0.7f, Fibrousness: 0.9f),
        ]);

        var forms = new FormCatalog([
            new FormDefinition(new FormId("stick"), "Stick"),
            new FormDefinition(new FormId("whole"), "Whole"),
            new FormDefinition(new FormId("wedge"), "Wedge", EdgeSharpness: 1f),
            new FormDefinition(new FormId("lump"), "Lump"),
            new FormDefinition(new FormId("shelter"), "Shelter"),
            new FormDefinition(new FormId("fibre"), "Fibre"),
            new FormDefinition(Cord, "Cord", LashingStrength: 1f),
        ]);

        var items = new ItemCatalog(
            [
                new ItemDefinition(Wood, "Wood", new MaterialId("wood"), new FormId("stick"), 2f),
                new ItemDefinition(Apple, "Apple", new MaterialId("apple"), new FormId("whole"), 1f, 1f),
                new ItemDefinition(Axe, "Axe", new MaterialId("stone"), new FormId("wedge"), 2.5f),
                new ItemDefinition(StorageHutItem, "Storage Hut", new MaterialId("wood"), new FormId("shelter"), StorageHutVolume),
                new ItemDefinition(Grass, "Grass", new MaterialId("plant_fibre"), new FormId("fibre"), 5f,
                    Transitions: [new FormTransition(TwistCommand.Verb, Cord, GrassPerCord)]),
                new ItemDefinition(Stone, "Stone", new MaterialId("stone"), new FormId("lump"), 1f,
                    Transitions: [new FormTransition(KnapCommand.Verb, Wedge, 1)]),
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
                new SkillDefinition(TwistCommand.Skill, "Twisting", BasicTwisting, new TechniqueId("efficient_twisting")),
                new SkillDefinition(BindCommand.Skill, "Binding", BasicBinding, new TechniqueId("efficient_binding")),
                new SkillDefinition(KnapCommand.Skill, "Knapping", BasicKnapping, new TechniqueId("efficient_knapping")),
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

    // Long enough with what they are carrying to have come to know it (see Beliefs): what the
    // bench says about a substance is what this person believes of it, not what is true.
    internal static void LetThemComeToKnow(WorldState world, Person person)
    {
        var perTick = world.Configuration.Rules.MaterialUnderstandingPerTick;
        foreach (var material in new[] { new MaterialId("plant_fibre"), new MaterialId("wood"), new MaterialId("stone"), new MaterialId("apple") })
        {
            if (world.Configuration.MaterialCatalog.Find(material) is not { } actual)
            {
                continue;
            }

            foreach (var property in Enum.GetValues<MaterialProperty>())
            {
                var value = property switch
                {
                    MaterialProperty.Density => actual.Density,
                    MaterialProperty.Hardness => actual.Hardness,
                    MaterialProperty.Toughness => actual.Toughness,
                    MaterialProperty.Flexibility => actual.Flexibility,
                    MaterialProperty.Elasticity => actual.Elasticity,
                    MaterialProperty.Fibrousness => actual.Fibrousness,
                    _ => 0f,
                };

                person.Beliefs.Learn(material, property, value, perTick * 100f);
            }
        }
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
