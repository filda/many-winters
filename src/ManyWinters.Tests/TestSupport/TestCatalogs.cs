using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.Population;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.TestSupport;

// Mirrors src/ManyWinters.Godot/Content/ so tests use the same ids without touching disk.
public static class TestCatalogs
{
    public static readonly EntityKindId Apple = new("apple");
    public static readonly EntityKindId Pear = new("pear");
    public static readonly EntityKindId Mushroom = new("mushroom");
    public static readonly EntityKindId Potato = new("potato");
    public static readonly EntityKindId Wood = new("wood");
    public static readonly EntityKindId Grass = new("grass");

    // Scattered across the map as decoration rather than placed individually.
    public static readonly EntityKindId ConiferTree = new("conifer_tree");
    public static readonly EntityKindId DeciduousTree = new("deciduous_tree");
    public static readonly EntityKindId Bush = new("bush");
    public static readonly EntityKindId Flower = new("flower");
    public static readonly EntityKindId Fern = new("fern");
    public static readonly EntityKindId RockPile = new("rock_pile");
    public static readonly EntityKindId RockBoulder = new("rock_boulder");
    public static readonly EntityKindId RockCluster = new("rock_cluster");
    public static readonly EntityKindId TreeStump = new("tree_stump");
    public static readonly EntityKindId FallenLog = new("fallen_log");

    public static readonly SkillTypeId Foraging = new("foraging");
    public static readonly SkillTypeId MushroomForaging = new("mushroom_foraging");
    private static readonly SkillTypeId RootDigging = new("root_digging");
    public static readonly SkillTypeId Woodcutting = new("woodcutting");
    private static readonly SkillTypeId Twisting = new("twisting");
    private static readonly SkillTypeId Binding = new("binding");
    private static readonly SkillTypeId Knapping = new("knapping");
    private static readonly SkillTypeId Sharpening = new("sharpening");
    private static readonly SkillTypeId Mining = new("mining");
    public static readonly SkillTypeId Burial = new("burial");
    private static readonly SkillTypeId Butchering = new("butchering");
    private static readonly SkillTypeId Hunting = new("hunting");

    // Nobody is born knowing how to eat or teach either - see SkillDefinition.BaseTechnique.
    private static readonly SkillTypeId Eating = new("eating");
    private static readonly SkillTypeId Teaching = new("teaching");

    // Never self-taught (see SkillDefinition.BaseTechnique): only GrantTechniqueCommand or
    // TeachCommand ever puts one of these into a person's KnownTechniques.
    public static readonly TechniqueId BasicForaging = new("basic_foraging");
    public static readonly TechniqueId BasicMushroomForaging = new("basic_mushroom_foraging");
    private static readonly TechniqueId BasicRootDigging = new("basic_root_digging");
    public static readonly TechniqueId BasicWoodcutting = new("basic_woodcutting");
    public static readonly TechniqueId BasicTwisting = new("basic_twisting");
    public static readonly TechniqueId BasicBinding = new("basic_binding");
    public static readonly TechniqueId BasicKnapping = new("basic_knapping");
    public static readonly TechniqueId BasicSharpening = new("basic_sharpening");
    public static readonly TechniqueId BasicMining = new("basic_mining");
    private static readonly TechniqueId BasicBurial = new("basic_burial");
    public static readonly TechniqueId BasicEating = new("basic_eating");
    public static readonly TechniqueId BasicTeaching = new("basic_teaching");
    public static readonly TechniqueId BasicButchering = new("basic_butchering");
    public static readonly TechniqueId BasicHunting = new("basic_hunting");

    public static readonly TechniqueId EfficientForaging = new("efficient_foraging");
    public static readonly TechniqueId EfficientMushroomForaging = new("efficient_mushroom_foraging");
    private static readonly TechniqueId EfficientRootDigging = new("efficient_root_digging");
    public static readonly TechniqueId EfficientWoodcutting = new("efficient_woodcutting");
    private static readonly TechniqueId EfficientTwisting = new("efficient_twisting");
    private static readonly TechniqueId EfficientBinding = new("efficient_binding");
    private static readonly TechniqueId EfficientKnapping = new("efficient_knapping");
    private static readonly TechniqueId EfficientSharpening = new("efficient_sharpening");
    private static readonly TechniqueId EfficientMining = new("efficient_mining");
    public static readonly TechniqueId EfficientBurial = new("efficient_burial");
    public static readonly TechniqueId EfficientEating = new("efficient_eating");
    public static readonly TechniqueId EfficientTeaching = new("efficient_teaching");
    public static readonly TechniqueId EfficientButchering = new("efficient_butchering");
    public static readonly TechniqueId EfficientHunting = new("efficient_hunting");

    public static readonly ItemKindId WoodItem = new("wood");
    public static readonly ItemKindId Axe = new("axe");
    public static readonly ItemKindId WarmClothing = new("warm_clothing");
    public static readonly ItemKindId AppleItem = new("apple");
    private static readonly ItemKindId PearItem = new("pear");
    private static readonly ItemKindId MushroomItem = new("mushroom");
    private static readonly ItemKindId PotatoItem = new("potato");
    public static readonly ItemKindId GrassItem = new("grass");
    public static readonly ItemKindId StoneItem = new("stone");
    private static readonly ItemKindId Basket = new("basket");
    private static readonly ItemKindId Bag = new("bag");
    public static readonly ItemKindId StorageHutItem = new("storage_hut");

    // A dead animal's carcass (docs/todo/fauna-plan.md, phase 3): what ButcherCommand takes off
    // it, one item kind each - matching the well-known ids ButcherCommand itself asks for.
    public static readonly ItemKindId MeatItem = new("meat");
    public static readonly ItemKindId HideItem = new("hide");
    public static readonly ItemKindId BoneItem = new("bone");
    public static readonly ItemKindId SinewItem = new("sinew");

    private static readonly MaterialId WoodMaterial = new("wood");
    private static readonly MaterialId StoneMaterial = new("stone");
    private static readonly MaterialId PlantFibreMaterial = new("plant_fibre");
    private static readonly MaterialId HideMaterial = new("hide");
    private static readonly MaterialId AppleMaterial = new("apple");
    private static readonly MaterialId PearMaterial = new("pear");
    private static readonly MaterialId PotatoMaterial = new("potato");
    private static readonly MaterialId MushroomMaterial = new("mushroom");
    private static readonly MaterialId MeatMaterial = new("meat");
    private static readonly MaterialId BoneMaterial = new("bone");
    private static readonly MaterialId SinewMaterial = new("sinew");

    private static readonly FormId Whole = new("whole");
    private static readonly FormId Stick = new("stick");
    private static readonly FormId Lump = new("lump");
    private static readonly FormId Fibre = new("fibre");
    public static readonly FormId Wedge = new("wedge");
    private static readonly FormId Vessel = new("vessel");
    private static readonly FormId Garment = new("garment");
    private static readonly FormId Shelter = new("shelter");
    public static readonly FormId Cord = new("cord");

    public const int AxeInputAmount = 5;
    public const int GrassPerCord = 3;

    // One lump makes one wedge: knapping takes a stone whole rather than a handful, the way
    // twisting takes several blades of grass.
    public const int StonePerWedge = 1;
    private static readonly TechniqueId TwistVerb = new("twist");
    private static readonly TechniqueId KnapVerb = new("knap");
    private const int WarmClothingInputAmount = 10;
    private const float FoodHungerRestoredPerUnit = 1f;

    // Mirrors Content/materials/{id}/{id}.json. Weight is density times volume
    // (ItemCatalog.WeightFor).
    private const float WoodDensity = 0.5f;
    private const float WoodHardness = 0.4f;
    private const float WoodToughness = 0.7f;
    private const float WoodFlexibility = 0.35f;
    private const float WoodFibrousness = 0.5f;
    private const float StoneDensity = 2f;
    private const float StoneHardness = 1f;
    // Hard and brittle is what makes stone knappable (MaterialAffordances.CanKnap); a toughness
    // left at zero would read as "nobody said" to MaterialWords while the predicate treated it
    // as perfectly brittle.
    private const float StoneToughness = 0.15f;
    private const float PlantFibreDensity = 0.2f;
    private const float PlantFibreToughness = 0.5f;
    private const float PlantFibreFlexibility = 0.7f;
    private const float PlantFibreFibrousness = 0.9f;
    private const float HideDensity = 0.75f;
    private const float HideToughness = 0.6f;
    private const float HideFlexibility = 0.8f;
    private const float HideElasticity = 0.15f;
    private const float HideFibrousness = 0.4f;
    private const float FoodDensity = 1f;
    private const float HideInsulation = 1f;

    // Mirrors grass.json's own hungerRestoredPerUnit (docs/todo/fauna-plan.md, step 0d): the
    // item's own nutrition, harmless to a human whose diet has no plant_fibre entry at all, is
    // what makes a deer's diet (above) able to restore hunger from it.
    private const float GrassHungerRestoredPerUnit = 0.5f;

    // Mirrors meat.json (docs/todo/fauna-plan.md, phase 3): meat is roughly five times as
    // calorie-dense as a piece of fruit (apple/pear/potato/mushroom all restore
    // FoodHungerRestoredPerUnit=1 per unit), the same order of magnitude real meat and fruit
    // differ by. A typical meal (TryAutoEat fires at Rules.HungerEatThreshold=25 and eats down
    // to zero) needs 25 apple units but only 5 meat units, so a deer's carcass (30 meat) covers
    // about six such meals - a real meal for a shipped band, not just one person's dinner.
    private const float MeatHungerRestoredPerUnit = 5f;

    // Mirrors bone.json (docs/todo/fauna-plan.md, phase 3): hard and tough like stone
    // (StoneHardness=1, StoneToughness=0.15 above) but noticeably lighter, and - unlike stone -
    // too tough to fracture into an edge (MaterialAffordances.CanKnap needs Toughness < 0.3), so
    // it comes out not-knappable and not-sharpenable from the properties alone, no special case
    // needed.
    private const float BoneDensity = 1.3f;
    private const float BoneHardness = 0.75f;
    private const float BoneToughness = 0.6f;
    private const float BoneFlexibility = 0.1f;
    private const float BoneElasticity = 0.05f;
    private const float BoneFibrousness = 0.1f;

    // Mirrors sinew.json (docs/todo/fauna-plan.md, phase 3): fibrous and flexible enough to
    // twist (MaterialAffordances.CanTwist needs Fibrousness > 0.5 and Flexibility > 0.4) and
    // elastic enough to hold tension (MaterialAffordances.HoldsTension needs Elasticity > 0.6) -
    // the bow/snare material the crafting doc names, and "sinew twisted is a sinew cord" (see
    // sinew's own twist FormTransition below).
    private const float SinewDensity = 0.9f;
    private const float SinewToughness = 0.5f;
    private const float SinewFlexibility = 0.6f;
    private const float SinewElasticity = 0.7f;
    private const float SinewFibrousness = 0.85f;

    private const float FoodVolume = 1f;
    private const float WoodVolume = 2f;
    private const float StoneVolume = 1f;
    private const float GrassVolume = 5f;
    private const float AxeVolume = 2.5f;
    private const float WarmClothingVolume = 4f;
    private const float HideVolume = 3f;
    private const float BoneVolume = 1f;
    private const float SinewVolume = 1f;
    public const int SinewPerCord = 3;

    // Basket (wood) and bag (grass, lighter but holds less); CarryCapacityBonus is applied in
    // WorldState.MaxCarryWeightFor.
    private const int BasketInputAmount = 8;
    private const float BasketVolume = 4f;
    private const float BasketCarryCapacityBonus = 20f;
    private const int BagInputAmount = 10;
    private const float BagVolume = 5f;
    private const float BagCarryCapacityBonus = 10f;
    private const float GrassRegenPerTick = 1f;

    public static readonly EntityKindId StorageHut = new("storage_hut");
    public const int StorageHutInputAmount = 20;
    // Deliberately far beyond any realistic carry capacity (see MakeCommand): what routes it
    // into the world instead of the maker's pack.
    private const float StorageHutVolume = 200f;

    public const float ColdFoodYieldMultiplier = 0.4f;
    public const float FoodRegenPerTick = 1f;
    private const float WoodRegenPerTick = 0.5f;
    public const float FellWoodYield = 30f;

    // Mirrors Content/resources/{kind}/{kind}.json. Rocks, stumps and logs get no regen: finite,
    // never regrow.
    private const float DecorationWoodRegenPerTick = 0.5f;
    private const float DecorationGroundCoverRegenPerTick = 1f;

    // Felling a forest tree leaves a stump (some wood, never regrows) and a fallen log (the bulk
    // of the trunk, more than one load); felling a bush leaves an ordinary wood pile.
    private const float FellTreeStumpYield = 20f;
    private const float FellLogYield = 40f;
    private const float FellBushWoodYield = 30f;

    // Mirrors collisionRadius in Content/resources/{kind}/{kind}.json; deliberately independent of
    // the sprite's height (see ResourceDefinition.CollisionRadius).
    private const float FruitTreeCollisionRadius = 0.35f;
    private const float BushCollisionRadius = 0.3f;
    private const float ForestTreeCollisionRadius = 0.4f;
    private const float RockPileCollisionRadius = 0.3f;
    private const float RockClusterCollisionRadius = 0.45f;
    private const float RockBoulderCollisionRadius = 0.6f;

    // Carry capacity ramps up with age (CarryCapacity.BaseWeightFor); command tests that don't
    // care about age spawn people already at the adult baseline.
    public static readonly long AdultAgeTicks = SimulationRules.Default.TicksPerYear * 4;

    // Mirrors Content/species/human/human.json (docs/todo/fauna-plan.md, step 0c): the same
    // numbers LifeStages and SimulationRules.MaxLifespanYears used to hardcode, now the one
    // species every test world defines.
    public const long WeaningAgeYears = 1;
    public const long AdultAgeYears = 4;
    public const long ElderAgeYears = 7;
    private const long HumanMaxLifespanYears = 10;

    public static readonly LifeCycle HumanLifeCycle = new(WeaningAgeYears, AdultAgeYears, ElderAgeYears, HumanMaxLifespanYears);

    // Mirrors Content/species/human/human.json's diet (docs/todo/fauna-plan.md, step 0d): every
    // material whose item has a HungerRestoredPerUnit above zero here, all at digestibility 1 -
    // exactly what is edible today, so no test's behaviour changes.
    private static readonly IReadOnlyList<SpeciesDefinition.DietEntry> HumanDiet =
    [
        new(AppleMaterial, 1f),
        new(PearMaterial, 1f),
        new(MushroomMaterial, 1f),
        new(PotatoMaterial, 1f),
        new(MeatMaterial, 1f),
    ];

    private static readonly SpeciesDefinition HumanSpecies = new(Person.HumanSpecies, "Human", HumanLifeCycle, HumanDiet);

    // Mirrors Content/species/deer/deer.json (docs/todo/fauna-plan.md, phase 1a): only defined
    // when a test opts into it (CreateConfigurationWithDeer), so every test that doesn't care
    // about animals keeps seeing exactly the human-only catalog it always has.
    private const long DeerWeaningAgeYears = 1;
    private const long DeerAdultAgeYears = 2;
    private const long DeerElderAgeYears = 6;
    private const long DeerMaxLifespanYears = 8;

    public static readonly LifeCycle DeerLifeCycle = new(DeerWeaningAgeYears, DeerAdultAgeYears, DeerElderAgeYears, DeerMaxLifespanYears);

    private static readonly IReadOnlyList<SpeciesDefinition.DietEntry> DeerDiet =
    [
        new(PlantFibreMaterial, 1f),
        new(AppleMaterial, 1f),
    ];

    public const float DeerCollisionRadius = 0.6f;
    public const int DeerHerdMinSize = 6;
    public const int DeerHerdMaxSize = 10;
    private const float DeerHerdHomeRadius = 15f;
    private const float DeerHerdDriftMetresPerSeason = 20f;

    // Mirrors deer.json's breeding block (docs/todo/fauna-plan.md, phase 1b, "mnozeni"): mates in
    // Mild (Spring and Autumn in the shipped calendar - SeasonParameters.Default), carries for two
    // seasons, and must be under 40 hunger to count as eligible.
    private const long DeerGestationTicks = 150;
    private const float DeerConceptionChancePerTick = 0.02f;
    private const float DeerSatietyHungerBelow = 40f;

    // The shipped map's winter reserve (docs/todo/fauna-plan.md, phase 1's "Otevřené ladění"):
    // the largest multiplier at which the same herd, run through the shipped map's own year
    // (MapLoader.LoadDefault, see DeerHerdMilestoneTests), still ends up at or above where it
    // started (start 17, 5 births, end 18 at 0.28; end 16 already at 0.29 - a sharp cutoff, not a
    // knife-edge value, since 0.1 through 0.28 all land on the same 18).
    public const float DeerHungerPerTickMultiplier = 0.28f;

    // Faster than a person's own fastest walk (MoveCommand.SpeedPerTick 1f is a player-directed
    // walk; a deer clearly outrunning that on FleeDistance/SafeDistance mirrors deer.json).
    public const float DeerFleeDistance = 8f;
    private const float DeerSafeDistance = 16f;
    private const float DeerFleeSpeedPerTick = 0.6f;

    public static readonly SpeciesId DeerSpeciesId = new("deer");

    // Mirrors deer.json's carcass block (docs/todo/fauna-plan.md, phase 3): what ButcherCommand
    // finds in a dead deer's Inventory.
    public const int DeerCarcassMeat = 30;
    public const int DeerCarcassHide = 1;
    public const int DeerCarcassBone = 4;
    public const int DeerCarcassSinew = 2;

    private static readonly IReadOnlyList<SpeciesDefinition.CarcassYield> DeerCarcass =
    [
        new(MeatItem, DeerCarcassMeat),
        new(HideItem, DeerCarcassHide),
        new(BoneItem, DeerCarcassBone),
        new(SinewItem, DeerCarcassSinew),
    ];

    private static readonly SpeciesDefinition DeerSpecies = new(
        DeerSpeciesId,
        "Deer",
        DeerLifeCycle,
        DeerDiet,
        InnateTechniques: [BasicEating, BasicForaging],
        CanCarry: false,
        CollisionRadius: DeerCollisionRadius,
        Herd: new SpeciesDefinition.HerdDefinition(DeerHerdMinSize, DeerHerdMaxSize, DeerHerdHomeRadius, DeerHerdDriftMetresPerSeason),
        Breeding: new SpeciesDefinition.BreedingDefinition(Climate.Mild, DeerGestationTicks, DeerConceptionChancePerTick, DeerSatietyHungerBelow),
        HungerPerTickMultiplier: DeerHungerPerTickMultiplier,
        Flee: new SpeciesDefinition.FleeDefinition(DeerFleeDistance, DeerSafeDistance, DeerFleeSpeedPerTick),
        Carcass: DeerCarcass);

    private static SpeciesCatalog CreateSpeciesCatalog(SpeciesDefinition humanSpecies, SpeciesDefinition? deerSpecies = null) =>
        deerSpecies is null ? new([humanSpecies]) : new([humanSpecies, deerSpecies]);

    private static IReadOnlyList<ClimateYield> ColdFoodYield => [new ClimateYield(Climate.Cold, ColdFoodYieldMultiplier)];

    private static ResourceCatalog CreateResourceCatalog() => new(new[]
    {
        new ResourceDefinition(Apple, "Apple", Foraging, AppleItem, ColdFoodYield, FoodRegenPerTick, CanFell: true, FellLeaves: [new(Wood, FellWoodYield)], CollisionRadius: FruitTreeCollisionRadius),
        new ResourceDefinition(Pear, "Pear", Foraging, PearItem, ColdFoodYield, FoodRegenPerTick, CanFell: true, FellLeaves: [new(Wood, FellWoodYield)], CollisionRadius: FruitTreeCollisionRadius),
        new ResourceDefinition(Mushroom, "Mushroom", MushroomForaging, MushroomItem, ColdFoodYield, FoodRegenPerTick),
        new ResourceDefinition(Potato, "Potato", RootDigging, PotatoItem, ColdFoodYield, FoodRegenPerTick),
        new ResourceDefinition(Wood, "Wood", Woodcutting, WoodItem, RegenPerTick: WoodRegenPerTick),
        new ResourceDefinition(Grass, "Wild Grass", Foraging, GrassItem, RegenPerTick: GrassRegenPerTick),
        new ResourceDefinition(ConiferTree, "Conifer Tree", Woodcutting, WoodItem, RegenPerTick: DecorationWoodRegenPerTick, CanFell: true, FellLeaves: [new(TreeStump, FellTreeStumpYield), new(FallenLog, FellLogYield)], RequiresToolToFell: true, CollisionRadius: ForestTreeCollisionRadius),
        new ResourceDefinition(DeciduousTree, "Deciduous Tree", Woodcutting, WoodItem, RegenPerTick: DecorationWoodRegenPerTick, CanFell: true, FellLeaves: [new(TreeStump, FellTreeStumpYield), new(FallenLog, FellLogYield)], RequiresToolToFell: true, CollisionRadius: ForestTreeCollisionRadius),
        new ResourceDefinition(Bush, "Bush", Woodcutting, WoodItem, RegenPerTick: DecorationWoodRegenPerTick, CanFell: true, FellLeaves: [new(Wood, FellBushWoodYield)], CollisionRadius: BushCollisionRadius),
        new ResourceDefinition(Flower, "Flower", Foraging, GrassItem, RegenPerTick: DecorationGroundCoverRegenPerTick),
        new ResourceDefinition(Fern, "Fern", Foraging, GrassItem, RegenPerTick: DecorationGroundCoverRegenPerTick),
        new ResourceDefinition(RockPile, "Rock Pile", Mining, StoneItem, CollisionRadius: RockPileCollisionRadius),
        new ResourceDefinition(RockBoulder, "Rock Boulder", Mining, StoneItem, CollisionRadius: RockBoulderCollisionRadius),
        new ResourceDefinition(RockCluster, "Rock Cluster", Mining, StoneItem, CollisionRadius: RockClusterCollisionRadius),
        new ResourceDefinition(TreeStump, "Tree Stump", Woodcutting, WoodItem),
        new ResourceDefinition(FallenLog, "Fallen Log", Woodcutting, WoodItem),
    });

    private static SkillCatalog CreateSkillCatalog() => new(new[]
    {
        new SkillDefinition(Foraging, "Foraging", BasicForaging, EfficientForaging),
        new SkillDefinition(MushroomForaging, "Mushroom Foraging", BasicMushroomForaging, EfficientMushroomForaging),
        new SkillDefinition(RootDigging, "Root Digging", BasicRootDigging, EfficientRootDigging),
        new SkillDefinition(Woodcutting, "Woodcutting", BasicWoodcutting, EfficientWoodcutting, UsesChoppingScore: true),
        new SkillDefinition(Twisting, "Twisting", BasicTwisting, EfficientTwisting),
        new SkillDefinition(Binding, "Binding", BasicBinding, EfficientBinding),
        new SkillDefinition(Knapping, "Knapping", BasicKnapping, EfficientKnapping),
        new SkillDefinition(Sharpening, "Sharpening", BasicSharpening, EfficientSharpening),
        new SkillDefinition(Mining, "Mining", BasicMining, EfficientMining),
        new SkillDefinition(Burial, "Burial", BasicBurial, EfficientBurial),
        new SkillDefinition(Eating, "Eating", BasicEating, EfficientEating),
        new SkillDefinition(Teaching, "Teaching", BasicTeaching, EfficientTeaching),
        new SkillDefinition(Butchering, "Butchering", BasicButchering, EfficientButchering),
        new SkillDefinition(Hunting, "Hunting", BasicHunting, EfficientHunting),
    });

    private static RecipeCatalog CreateRecipeCatalog() => new(new[]
    {
        new RecipeDefinition(Axe, WoodItem, AxeInputAmount),
        new RecipeDefinition(WarmClothing, WoodItem, WarmClothingInputAmount),
        new RecipeDefinition(Basket, WoodItem, BasketInputAmount),
        new RecipeDefinition(Bag, GrassItem, BagInputAmount),
        new RecipeDefinition(StorageHutItem, WoodItem, StorageHutInputAmount),
    });

    // Mirrors Content/forms/{id}/{id}.json: only a wedge presents an edge, which is what keeps a
    // raw lump of the same stone from scoring as a tool (see ItemCatalog.ChoppingScoreFor).
    private const float WedgeEdgeSharpness = 1f;
    private const float CordLashingStrength = 1f;

    // A shaft doubles the blow of what is lashed to its end (see ItemCatalog.ChoppingScoreOf).
    private const float StickHaftLeverage = 1f;

    private static FormCatalog CreateFormCatalog() => new(new[]
    {
        new FormDefinition(Whole, "Whole"),
        new FormDefinition(Stick, "Stick", HaftLeverage: StickHaftLeverage),
        new FormDefinition(Lump, "Lump"),
        new FormDefinition(Fibre, "Fibre"),
        new FormDefinition(Wedge, "Wedge", WedgeEdgeSharpness),
        new FormDefinition(Vessel, "Vessel"),
        new FormDefinition(Garment, "Garment"),
        new FormDefinition(Shelter, "Shelter"),
        new FormDefinition(Cord, "Cord", LashingStrength: CordLashingStrength),
    });

    private static MaterialCatalog CreateMaterialCatalog() => new(new[]
    {
        new MaterialDefinition(WoodMaterial, "Wood", WoodDensity, Hardness: WoodHardness, Toughness: WoodToughness, Flexibility: WoodFlexibility, Fibrousness: WoodFibrousness),
        new MaterialDefinition(StoneMaterial, "Stone", StoneDensity, Hardness: StoneHardness, Toughness: StoneToughness),
        new MaterialDefinition(PlantFibreMaterial, "Plant Fibre", PlantFibreDensity, Toughness: PlantFibreToughness, Flexibility: PlantFibreFlexibility, Fibrousness: PlantFibreFibrousness),
        new MaterialDefinition(HideMaterial, "Hide", HideDensity, HideInsulation, Toughness: HideToughness, Flexibility: HideFlexibility, Elasticity: HideElasticity, Fibrousness: HideFibrousness),
        new MaterialDefinition(AppleMaterial, "Apple Flesh", FoodDensity),
        new MaterialDefinition(PearMaterial, "Pear Flesh", FoodDensity),
        new MaterialDefinition(PotatoMaterial, "Potato Flesh", FoodDensity),
        new MaterialDefinition(MushroomMaterial, "Mushroom Flesh", FoodDensity),
        new MaterialDefinition(MeatMaterial, "Meat", FoodDensity),
        new MaterialDefinition(BoneMaterial, "Bone", BoneDensity, Hardness: BoneHardness, Toughness: BoneToughness, Flexibility: BoneFlexibility, Elasticity: BoneElasticity, Fibrousness: BoneFibrousness),
        new MaterialDefinition(SinewMaterial, "Sinew", SinewDensity, Toughness: SinewToughness, Flexibility: SinewFlexibility, Elasticity: SinewElasticity, Fibrousness: SinewFibrousness),
    });

    // The axe is stone and the warm clothing hide although both are crafted from wood: the
    // single-input recipes are placeholders, and a wooden garment would make wood itself warm.
    private static ItemCatalog CreateItemCatalog(MaterialCatalog materials, FormCatalog forms) => new(new[]
    {
        new ItemDefinition(WarmClothing, "Warm Clothing", HideMaterial, Garment, WarmClothingVolume),
        new ItemDefinition(WoodItem, "Wood", WoodMaterial, Stick, WoodVolume),
        new ItemDefinition(Axe, "Axe", StoneMaterial, Wedge, AxeVolume),
        new ItemDefinition(AppleItem, "Apple", AppleMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(PearItem, "Pear", PearMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(MushroomItem, "Mushroom", MushroomMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(PotatoItem, "Potato", PotatoMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(GrassItem, "Grass", PlantFibreMaterial, Fibre, GrassVolume, GrassHungerRestoredPerUnit, Transitions: [new FormTransition(TwistVerb, Cord, GrassPerCord)]),
        new ItemDefinition(StoneItem, "Stone", StoneMaterial, Lump, StoneVolume, Transitions: [new FormTransition(KnapVerb, Wedge, StonePerWedge)]),
        new ItemDefinition(Basket, "Basket", WoodMaterial, Vessel, BasketVolume, CarryCapacityBonus: BasketCarryCapacityBonus),
        new ItemDefinition(Bag, "Bag", PlantFibreMaterial, Vessel, BagVolume, CarryCapacityBonus: BagCarryCapacityBonus),
        new ItemDefinition(StorageHutItem, "Storage Hut", WoodMaterial, Shelter, StorageHutVolume),
        new ItemDefinition(MeatItem, "Meat", MeatMaterial, Lump, FoodVolume, MeatHungerRestoredPerUnit),
        new ItemDefinition(HideItem, "Hide", HideMaterial, Whole, HideVolume),
        new ItemDefinition(BoneItem, "Bone", BoneMaterial, Stick, BoneVolume),
        new ItemDefinition(SinewItem, "Sinew", SinewMaterial, Fibre, SinewVolume, Transitions: [new FormTransition(TwistVerb, Cord, SinewPerCord)]),
    }, materials, forms);

    public static WorldConfiguration CreateConfiguration()
    {
        var materials = CreateMaterialCatalog();
        var forms = CreateFormCatalog();

        return new(
            CreateSpeciesCatalog(HumanSpecies),
            CreateResourceCatalog(),
            CreateSkillCatalog(),
            CreateRecipeCatalog(),
            materials,
            forms,
            CreateItemCatalog(materials, forms),
            SeasonParameters.Default,
            SimulationRules.Default);
    }

    public static WorldState CreateWorld() => new(CreateConfiguration());

    // Every Person.MaxHunger comes out at exactly SimulationRules.MaxHunger. The shipped game
    // draws one per person, so a test pinning an exact tick of death would otherwise assert
    // against a draw. Tests about the spread itself use CreateWorld.
    public static WorldConfiguration CreateConfigurationWithoutHungerVariation() =>
        CreateConfiguration() with { Rules = SimulationRules.Default with { MaxHungerVariation = 0f } };

    public static WorldState CreateWorldWithoutHungerVariation() => new(CreateConfigurationWithoutHungerVariation());

    // For tests that shrink a lifespan to make old age arrive after a handful of ticks instead of
    // ten winters (WorldStateTests' ShortLifeRules used to do this via SimulationRules.MaxLifespanYears,
    // which moved onto the species' own LifeCycle in step 0c). Keeps the standard diet - only the
    // life cycle differs.
    public static WorldConfiguration CreateConfigurationWithLifeCycle(LifeCycle humanLifeCycle) =>
        CreateConfigurationWithSpecies(HumanSpecies with { LifeCycle = humanLifeCycle });

    // For tests that need a human species with a diet (or anything else about the species) other
    // than the standard one - e.g. a species that cannot digest the apple's material at all,
    // to prove EatCommand actually consults it (docs/todo/fauna-plan.md, step 0d).
    public static WorldConfiguration CreateConfigurationWithSpecies(SpeciesDefinition humanSpecies) =>
        CreateConfiguration() with { SpeciesCatalog = CreateSpeciesCatalog(humanSpecies) };

    // For tests about Animal/HomeRange/SpawnAnimalCommand and MapLoader's starting herds
    // (docs/todo/fauna-plan.md, phase 1a): the human catalog plus the one deer species above.
    public static WorldConfiguration CreateConfigurationWithDeer() =>
        CreateConfiguration() with { SpeciesCatalog = CreateSpeciesCatalog(HumanSpecies, DeerSpecies) };

    public static WorldState CreateWorldWithDeer() => new(CreateConfigurationWithDeer());

    // For tests about WorldState.BreedAnimals (docs/todo/fauna-plan.md, phase 1b, "mnozeni") that
    // need a chance, gestation or satiety threshold other than the shipped deer.json's, so a
    // condition can be proven with a handful of ticks instead of replaying the real numbers.
    public static WorldConfiguration CreateConfigurationWithDeerBreeding(SpeciesDefinition.BreedingDefinition breeding) =>
        CreateConfiguration() with { SpeciesCatalog = CreateSpeciesCatalog(HumanSpecies, DeerSpecies with { Breeding = breeding }) };

    // The shipped axe-grade sharp hafted tool HuntCommand's own arithmetic is pinned against
    // (docs/todo/fauna-plan.md phase 3, SimulationRules.HuntingHitChancePerToolScore): a knapped
    // wedge lashed to a stick, both practised to mastery (WorkAttempt.QualityFor is 1 at
    // Skills.LevelAfter(50) - see MakingAnAxeTests.Toolmaker). Built directly from the parts
    // rather than by executing Knap/Twist/Bind, so a test can pin its exact chopping score
    // without also depending on those commands' own dice.
    //
    // ChoppingScoreOf works out to EdgeSharpness(Wedge=1) * Hardness(Stone=1) * sqrt(weight
    // density(Stone=2)*volume(1)=2) * (1 + HaftLeverage(Stick=1) * JointStrength(0.5)) ~= 2.121 -
    // the haft side scores nothing on its own (Stick has no EdgeSharpness), so the max in
    // ItemCatalog.ChoppingScoreOf always picks the head's own reading.
    public static Assembly CreateTestAxe(WorldState world)
    {
        var itemCatalog = world.Configuration.ItemCatalog;
        var axe = itemCatalog.Get(Axe);
        var wood = itemCatalog.Get(WoodItem);

        var head = new Assembly.Part(axe.Material, axe.Form, Quality: 1f, Volume: StonePerWedge * StoneVolume);
        var haft = new Assembly.Part(wood.Material, wood.Form, Quality: 1f, Volume: WoodVolume);

        return new Assembly.Joined(JointStrength: 0.5f, JointWeight: 0f, head, haft);
    }
}
