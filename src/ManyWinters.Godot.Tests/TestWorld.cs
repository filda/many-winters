using ManyWinters.Core.Commands;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Godot.Tests;

// A small world with catalogs, for the presentation logic that reads one. ManyWinters.Tests has
// a fuller mirror of Content in its own test catalogs; this is a deliberate copy rather than a
// shared one, because the two test projects answer to different rules (see README.md) and only
// this side may not touch the engine.
internal static class TestWorld
{
    internal static readonly ItemKindId Wood = new("wood");
    internal static readonly ItemKindId Apple = new("apple");
    // A second edible kind, so tests can tell the food carried first from the food carried
    // second rather than only ever offering one candidate to choose between.
    internal static readonly ItemKindId Berry = new("berry");
    internal static readonly ItemKindId Grass = new("grass");
    private static readonly ItemKindId Axe = new("axe");
    internal static readonly ItemKindId Stone = new("stone");

    internal static readonly EntityKindId AppleTree = new("apple");
    internal static readonly EntityKindId Stump = new("tree_stump");

    private static readonly SkillTypeId Foraging = new("foraging");
    private static readonly SkillTypeId Teaching = new("teaching");
    internal static readonly TechniqueId BasicForaging = new("basic_foraging");

    // Home is required of every Person but none of the presentation logic reads it: a fresh camp
    // each time, in no world.
    internal static HomeRange AnyHome => new(new Position(0, 0)) { Radius = 8f, DriftMetresPerSeason = 0f };
    internal static readonly TechniqueId EfficientForaging = new("efficient_foraging");
    private static readonly TechniqueId BasicEating = new("basic_eating");
    internal static readonly TechniqueId BasicTeaching = new("basic_teaching");

    // Mirrors Content/skills/{hunting,butchering}/*.json and Content/items/{meat,hide,bone,sinew} -
    // only what the hunting and animal-card tests read: the ids HuntCommand/ButcherCommand ask
    // for and a carcass's own item kinds, named the way the player reads them.
    private static readonly TechniqueId BasicHunting = new("basic_hunting");
    private static readonly TechniqueId BasicButchering = new("basic_butchering");
    internal static readonly ItemKindId Meat = new("meat");
    internal static readonly ItemKindId Hide = new("hide");
    internal static readonly ItemKindId Bone = new("bone");
    internal static readonly ItemKindId Sinew = new("sinew");

    private static readonly EntityKindId StorageHut = new("storage_hut");
    private static readonly ItemKindId StorageHutItem = new("storage_hut");

    // A second thing too big to carry, so a spot on the ground can be offered more than one
    // building at once and their order put to the test - "hearth" sorts before "storage_hut".
    private static readonly ItemKindId Hearth = new("hearth");
    private const int HearthInputAmount = 10;

    // A second small thing the bench can make, wanting more wood than the axe does, so a person
    // carrying enough for both can be offered them in a stable order - "axe" sorts before
    // "chisel".
    private static readonly ItemKindId Chisel = new("chisel");
    internal const int ChiselInputAmount = 8;

    internal const int GrassPerCord = 3;
    internal static readonly FormId Cord = new("cord");
    internal static readonly TechniqueId BasicTwisting = new("basic_twisting");
    internal static readonly TechniqueId BasicBinding = new("basic_binding");
    internal static readonly TechniqueId BasicKnapping = new("basic_knapping");
    internal static readonly TechniqueId BasicSharpening = new("basic_sharpening");
    private static readonly FormId Wedge = new("wedge");

    internal const int AxeInputAmount = 5;
    internal const int StorageHutInputAmount = 20;
    // Deliberately far beyond any realistic carry capacity, so it routes into the world instead
    // of the maker's pack.
    private const float StorageHutVolume = 200f;

    // Mirrors Content/species/human/human.json: a deliberate copy, like every other catalog here,
    // rather than a shared one with ManyWinters.Tests.
    internal const long AdultAgeYears = 4;
    private static readonly LifeCycle HumanLifeCycle = new(WeaningAgeYears: 1, AdultAgeYears: AdultAgeYears, ElderAgeYears: 7, MaxLifespanYears: 10);

    // Mirrors Content/species/deer/deer.json, narrowed to what the age-and-sex wording tests
    // read: the life cycle alone, nothing about diet, herding or breeding.
    private static readonly SpeciesId DeerSpecies = new("deer");
    private const long DeerAdultAgeYears = 2;
    private static readonly LifeCycle DeerLifeCycle = new(WeaningAgeYears: 1, AdultAgeYears: DeerAdultAgeYears, ElderAgeYears: 6, MaxLifespanYears: 8);

    private const float CollisionRadius = 0.35f;
    private const float HungerPerTickMultiplier = 1f;

    // Both Apple and Berry share the "apple" material below, so this one entry keeps both edible -
    // all that is edible in this test world.
    private static readonly IReadOnlyList<SpeciesDefinition.DietEntry> HumanDiet = [new(new MaterialId("apple"), 1f)];

    internal static WorldState Create() => Create(SimulationRules.Default);

    // A short decay window, for tests that need an already-decayed creature without
    // simulating a season of ticks - mirrors the other project's own short-decay test world, a
    // deliberate copy like every other catalog here rather than a shared one with that project.
    internal static WorldState CreateWithShortCorpseDecay(long corpseDecayTicks) =>
        Create(SimulationRules.Default with { CorpseDecayTicks = corpseDecayTicks });

    private static WorldState Create(SimulationRules rules)
    {
        var materials = new MaterialCatalog([
            new MaterialDefinition(new MaterialId("wood"), "Wood", 0.5f),
            new MaterialDefinition(new MaterialId("apple"), "Apple Flesh", 1f),
            new MaterialDefinition(new MaterialId("stone"), "Stone", 2f, Hardness: 1f, Toughness: 0.15f),
            new MaterialDefinition(new MaterialId("plant_fibre"), "Plant Fibre", 0.2f, Toughness: 0.5f, Flexibility: 0.7f, Fibrousness: 0.9f),
            // A carcass's own materials (mirrors Content/materials/{meat,bone,sinew}), narrowed to
            // the density AnimalCard/ItemCatalog need - a display name and a weight are all this
            // test world asks a carcass's leavings for.
            new MaterialDefinition(new MaterialId("meat"), "Meat", 1f),
            new MaterialDefinition(new MaterialId("hide"), "Hide", 0.75f),
            new MaterialDefinition(new MaterialId("bone"), "Bone", 1.3f),
            new MaterialDefinition(new MaterialId("sinew"), "Sinew", 0.9f),
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
                new ItemDefinition(Berry, "Berry", new MaterialId("apple"), new FormId("whole"), 0.2f, 1f),
                new ItemDefinition(Axe, "Axe", new MaterialId("stone"), new FormId("wedge"), 2.5f),
                new ItemDefinition(Chisel, "Chisel", new MaterialId("stone"), new FormId("wedge"), 1f),
                new ItemDefinition(StorageHutItem, "Storage Hut", new MaterialId("wood"), new FormId("shelter"), StorageHutVolume),
                new ItemDefinition(Hearth, "Hearth", new MaterialId("stone"), new FormId("shelter"), StorageHutVolume),
                new ItemDefinition(Grass, "Grass", new MaterialId("plant_fibre"), new FormId("fibre"), 5f,
                    Transitions: [new FormTransition(TwistCommand.Verb, Cord, GrassPerCord)]),
                new ItemDefinition(Stone, "Stone", new MaterialId("stone"), new FormId("lump"), 1f,
                    Transitions: [new FormTransition(KnapCommand.Verb, Wedge, 1)]),
                // A carcass's own leavings (mirrors Content/items/{meat,hide,bone,sinew}) - food
                // only for meat, the same as production content, though no test here needs it
                // edible.
                new ItemDefinition(Meat, "Meat", new MaterialId("meat"), new FormId("whole"), 1f, HungerRestoredPerUnit: 5f),
                new ItemDefinition(Hide, "Hide", new MaterialId("hide"), new FormId("whole"), 3f),
                new ItemDefinition(Bone, "Bone", new MaterialId("bone"), new FormId("whole"), 1f),
                new ItemDefinition(Sinew, "Sinew", new MaterialId("sinew"), new FormId("whole"), 1f),
            ],
            materials,
            forms);

        return new WorldState(new WorldConfiguration(
            new SpeciesCatalog(
            [
                new SpeciesDefinition(Person.HumanSpecies, "Human", HumanLifeCycle, HumanDiet) { CollisionRadius = CollisionRadius, HungerPerTickMultiplier = HungerPerTickMultiplier },
                new SpeciesDefinition(DeerSpecies, "Deer", DeerLifeCycle, CanCarry: false) { CollisionRadius = CollisionRadius, HungerPerTickMultiplier = HungerPerTickMultiplier },
            ]),
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
                new SkillDefinition(SharpenCommand.Skill, "Sharpening", BasicSharpening, new TechniqueId("efficient_sharpening")),
                new SkillDefinition(HuntCommand.Skill, "Hunting", BasicHunting, new TechniqueId("efficient_hunting")),
                new SkillDefinition(ButcherCommand.Skill, "Butchering", BasicButchering, new TechniqueId("efficient_butchering")),
            ]),
            new RecipeCatalog([
                new RecipeDefinition(Axe, Wood, AxeInputAmount),
                new RecipeDefinition(Chisel, Wood, ChiselInputAmount),
                new RecipeDefinition(StorageHutItem, Wood, StorageHutInputAmount),
                new RecipeDefinition(Hearth, Wood, HearthInputAmount),
            ]),
            materials,
            forms,
            items,
            SeasonParameters.Default,
            rules));
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
            Home = mother.Home,
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

    // Long enough with what they are carrying to have come to know it: what the bench says
    // about a substance is what this person believes of it, not what is true.
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
            BirthTick = world.Clock.CurrentTick - (SimulationRules.Default.TicksPerYear * AdultAgeYears),
            Mother = Person.Unknown,
            Father = Person.Unknown,
            Sex = sex,
            Home = AnyHome,
        };

        world.AddPerson(person);
        return person;
    }

    // Grown, on a home range of its own - the animal counterpart of AddAdult, for AnimalCard and
    // task-wording tests that need a Creature which is not a Person.
    internal static Animal AddAdultAnimal(WorldState world, Position position, Sex sex = Sex.Female)
    {
        var home = new HomeRange(position) { Radius = 10f, DriftMetresPerSeason = 0f };
        world.AddHomeRange(home);

        var id = CreatureId.New();
        var animal = new Animal(DeerSpecies, home)
        {
            Id = id,
            Position = position,
            BirthTick = world.Clock.CurrentTick - (SimulationRules.Default.TicksPerYear * DeerAdultAgeYears),
            Sex = sex,
        };

        world.AddAnimal(animal);
        return animal;
    }
}
