using ManyWinters.Core.Construction;
using ManyWinters.Core.Items;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.Materials;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.TestSupport;

// Mirrors the content files under src/ManyWinters.Godot/Content/ so tests exercise the same ids without touching disk.
public static class TestCatalogs
{
    public static readonly ResourceKindId Apple = new("apple");
    public static readonly ResourceKindId Pear = new("pear");
    public static readonly ResourceKindId Mushroom = new("mushroom");
    public static readonly ResourceKindId Potato = new("potato");
    public static readonly ResourceKindId Wood = new("wood");
    public static readonly ResourceKindId Grass = new("grass");

    // Former terrain decoration, now real ResourceNodes (MapLoader.ScatterDecorations).
    public static readonly ResourceKindId ConiferTree = new("conifer_tree");
    public static readonly ResourceKindId DeciduousTree = new("deciduous_tree");
    public static readonly ResourceKindId Bush = new("bush");
    public static readonly ResourceKindId Flower = new("flower");
    public static readonly ResourceKindId Fern = new("fern");
    public static readonly ResourceKindId RockPile = new("rock_pile");
    public static readonly ResourceKindId RockBoulder = new("rock_boulder");
    public static readonly ResourceKindId RockCluster = new("rock_cluster");
    public static readonly ResourceKindId TreeStump = new("tree_stump");
    public static readonly ResourceKindId FallenLog = new("fallen_log");

    public static readonly SkillTypeId Foraging = new("foraging");
    public static readonly SkillTypeId MushroomForaging = new("mushroom_foraging");
    private static readonly SkillTypeId RootDigging = new("root_digging");
    public static readonly SkillTypeId Woodcutting = new("woodcutting");
    private static readonly SkillTypeId Mining = new("mining");
    public static readonly SkillTypeId Burial = new("burial");

    // Nobody is born knowing how to eat or teach either - see SkillDefinition.BaseTechnique.
    private static readonly SkillTypeId Eating = new("eating");
    private static readonly SkillTypeId Teaching = new("teaching");

    // Never self-taught (see SkillDefinition.BaseTechnique's own doc comment) - the only way
    // any of these ever end up in a person's KnownTechniques is GrantTechniqueCommand (the
    // player) or TeachCommand (another person who already knows it).
    public static readonly TechniqueId BasicForaging = new("basic_foraging");
    public static readonly TechniqueId BasicMushroomForaging = new("basic_mushroom_foraging");
    private static readonly TechniqueId BasicRootDigging = new("basic_root_digging");
    public static readonly TechniqueId BasicWoodcutting = new("basic_woodcutting");
    public static readonly TechniqueId BasicMining = new("basic_mining");
    private static readonly TechniqueId BasicBurial = new("basic_burial");
    public static readonly TechniqueId BasicEating = new("basic_eating");
    public static readonly TechniqueId BasicTeaching = new("basic_teaching");

    public static readonly TechniqueId EfficientForaging = new("efficient_foraging");
    public static readonly TechniqueId EfficientMushroomForaging = new("efficient_mushroom_foraging");
    private static readonly TechniqueId EfficientRootDigging = new("efficient_root_digging");
    public static readonly TechniqueId EfficientWoodcutting = new("efficient_woodcutting");
    private static readonly TechniqueId EfficientMining = new("efficient_mining");
    public static readonly TechniqueId EfficientBurial = new("efficient_burial");
    public static readonly TechniqueId EfficientEating = new("efficient_eating");
    public static readonly TechniqueId EfficientTeaching = new("efficient_teaching");

    public static readonly ItemKindId WoodItem = new("wood");
    public static readonly ItemKindId Axe = new("axe");
    public static readonly ItemKindId WarmClothing = new("warm_clothing");
    public static readonly ItemKindId AppleItem = new("apple");
    private static readonly ItemKindId PearItem = new("pear");
    private static readonly ItemKindId MushroomItem = new("mushroom");
    private static readonly ItemKindId PotatoItem = new("potato");
    public static readonly ItemKindId GrassItem = new("grass");
    private static readonly ItemKindId StoneItem = new("stone");
    private static readonly ItemKindId Basket = new("basket");
    private static readonly ItemKindId Bag = new("bag");

    private static readonly MaterialId WoodMaterial = new("wood");
    private static readonly MaterialId StoneMaterial = new("stone");
    private static readonly MaterialId PlantFibreMaterial = new("plant_fibre");
    private static readonly MaterialId HideMaterial = new("hide");
    private static readonly MaterialId AppleMaterial = new("apple");
    private static readonly MaterialId PearMaterial = new("pear");
    private static readonly MaterialId PotatoMaterial = new("potato");
    private static readonly MaterialId MushroomMaterial = new("mushroom");

    private static readonly FormId Whole = new("whole");
    private static readonly FormId Stick = new("stick");
    private static readonly FormId Lump = new("lump");
    private static readonly FormId Fibre = new("fibre");
    private static readonly FormId Wedge = new("wedge");
    private static readonly FormId Vessel = new("vessel");
    private static readonly FormId Garment = new("garment");

    public const float AxeHarvestBonus = 15f;
    public const int AxeInputAmount = 5;
    private const int WarmClothingInputAmount = 10;
    private const float FoodHungerRestoredPerUnit = 1f;

    // Mirrors Content/materials/{id}/{id}.json. Weight is a material's density times an item's
    // volume (ItemCatalog.WeightFor), so these two blocks together reproduce exactly the
    // weights items used to state for themselves.
    private const float WoodDensity = 0.5f;
    private const float StoneDensity = 2f;
    private const float PlantFibreDensity = 0.2f;
    private const float HideDensity = 0.75f;
    private const float FoodDensity = 1f;
    private const float HideInsulation = 1f;

    private const float FoodVolume = 1f;
    private const float WoodVolume = 2f;
    private const float StoneVolume = 1f;
    private const float GrassVolume = 5f;
    private const float AxeVolume = 2.5f;
    private const float WarmClothingVolume = 4f;

    // Basket (wood, carried on the back) and bag (grass, lighter but holds less) - see
    // WorldState.MaxCarryWeightFor for how CarryCapacityBonus is applied.
    private const int BasketInputAmount = 8;
    private const float BasketVolume = 4f;
    private const float BasketCarryCapacityBonus = 20f;
    private const int BagInputAmount = 10;
    private const float BagVolume = 5f;
    private const float BagCarryCapacityBonus = 10f;
    private const float GrassRegenPerTick = 1f;

    public static readonly BuildingKindId StorageHut = new("storage_hut");
    public const int StorageHutInputAmount = 20;

    public const float ColdFoodYieldMultiplier = 0.4f;
    public const float FoodRegenPerTick = 1f;
    private const float WoodRegenPerTick = 0.5f;
    public const float FellWoodYield = 30f;

    // Former terrain decoration (see ConiferTree etc. above) - regenPerTick 0 for rocks/dead
    // wood (finite, never regrow) mirrors Content/resources/{kind}/{kind}.json exactly.
    private const float DecorationWoodRegenPerTick = 0.5f;
    private const float DecorationGroundCoverRegenPerTick = 1f;

    // Felling a standing forest tree leaves both a stump in its own spot (still has some wood
    // left to gather, but never regrows - a stump doesn't put out new branches) and a fallen
    // log nearby (the bulk of the trunk's wood, too much for one inventory load to carry off
    // in one go); felling a bush just leaves an ordinary small wood pile, the same kind a
    // cleared fruit tree leaves.
    private const float FellTreeStumpYield = 20f;
    private const float FellLogYield = 40f;
    private const float FellBushWoodYield = 30f;

    // Mirrors Content/resources/{kind}/{kind}.json's collisionRadius exactly - see
    // ResourceDefinition.CollisionRadius's own doc comment for why this is a deliberately
    // separate axis from the billboard sprite's height.
    private const float FruitTreeCollisionRadius = 0.35f;
    private const float BushCollisionRadius = 0.3f;
    private const float ForestTreeCollisionRadius = 0.4f;
    private const float RockPileCollisionRadius = 0.3f;
    private const float RockClusterCollisionRadius = 0.45f;
    private const float RockBoulderCollisionRadius = 0.6f;

    // Carry capacity (see CarryCapacity.BaseWeightFor) ramps up with age - most command tests
    // don't care about age at all, so they add people old enough to already be at the full
    // adult baseline rather than a newborn's reduced one.
    public static readonly long AdultAgeTicks = SimulationRules.Default.TicksPerYear * 4;

    private static IReadOnlyList<ClimateYield> ColdFoodYield => [new ClimateYield(Climate.Cold, ColdFoodYieldMultiplier)];

    private static ResourceCatalog CreateResourceCatalog() => new(new[]
    {
        new ResourceDefinition(Apple, "Apple", Foraging, AppleItem, ColdFoodYield, FoodRegenPerTick, CanFell: true, FellLeaves: [new(Wood, FellWoodYield)], CollisionRadius: FruitTreeCollisionRadius),
        new ResourceDefinition(Pear, "Pear", Foraging, PearItem, ColdFoodYield, FoodRegenPerTick, CanFell: true, FellLeaves: [new(Wood, FellWoodYield)], CollisionRadius: FruitTreeCollisionRadius),
        new ResourceDefinition(Mushroom, "Mushroom", MushroomForaging, MushroomItem, ColdFoodYield, FoodRegenPerTick),
        new ResourceDefinition(Potato, "Potato", RootDigging, PotatoItem, ColdFoodYield, FoodRegenPerTick),
        new ResourceDefinition(Wood, "Wood", Woodcutting, WoodItem, RegenPerTick: WoodRegenPerTick),
        new ResourceDefinition(Grass, "Wild Grass", Foraging, GrassItem, RegenPerTick: GrassRegenPerTick),
        new ResourceDefinition(ConiferTree, "Conifer Tree", Woodcutting, WoodItem, RegenPerTick: DecorationWoodRegenPerTick, CanFell: true, FellLeaves: [new(TreeStump, FellTreeStumpYield), new(FallenLog, FellLogYield)], CollisionRadius: ForestTreeCollisionRadius),
        new ResourceDefinition(DeciduousTree, "Deciduous Tree", Woodcutting, WoodItem, RegenPerTick: DecorationWoodRegenPerTick, CanFell: true, FellLeaves: [new(TreeStump, FellTreeStumpYield), new(FallenLog, FellLogYield)], CollisionRadius: ForestTreeCollisionRadius),
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
        new SkillDefinition(Woodcutting, "Woodcutting", BasicWoodcutting, EfficientWoodcutting, Axe, AxeHarvestBonus),
        new SkillDefinition(Mining, "Mining", BasicMining, EfficientMining),
        new SkillDefinition(Burial, "Burial", BasicBurial, EfficientBurial),
        new SkillDefinition(Eating, "Eating", BasicEating, EfficientEating),
        new SkillDefinition(Teaching, "Teaching", BasicTeaching, EfficientTeaching),
    });

    private static RecipeCatalog CreateRecipeCatalog() => new(new[]
    {
        new RecipeDefinition(Axe, WoodItem, AxeInputAmount),
        new RecipeDefinition(WarmClothing, WoodItem, WarmClothingInputAmount),
        new RecipeDefinition(Basket, WoodItem, BasketInputAmount),
        new RecipeDefinition(Bag, GrassItem, BagInputAmount),
    });

    private static BuildingCatalog CreateBuildingCatalog() => new(new[]
    {
        new BuildingDefinition(StorageHut, "Storage Hut", WoodItem, StorageHutInputAmount),
    });

    private static MaterialCatalog CreateMaterialCatalog() => new(new[]
    {
        new MaterialDefinition(WoodMaterial, "Wood", WoodDensity),
        new MaterialDefinition(StoneMaterial, "Stone", StoneDensity),
        new MaterialDefinition(PlantFibreMaterial, "Plant Fibre", PlantFibreDensity),
        new MaterialDefinition(HideMaterial, "Hide", HideDensity, HideInsulation),
        new MaterialDefinition(AppleMaterial, "Apple Flesh", FoodDensity),
        new MaterialDefinition(PearMaterial, "Pear Flesh", FoodDensity),
        new MaterialDefinition(PotatoMaterial, "Potato Flesh", FoodDensity),
        new MaterialDefinition(MushroomMaterial, "Mushroom Flesh", FoodDensity),
    });

    // The axe is stone and the warm clothing is hide, even though both are still crafted out of
    // wood - the single-input recipes are placeholders that the crafting plan's verbs replace,
    // and describing a hide garment as wooden to match one would have made wood itself warm.
    private static ItemCatalog CreateItemCatalog(MaterialCatalog materials) => new(new[]
    {
        new ItemDefinition(WarmClothing, "Warm Clothing", HideMaterial, Garment, WarmClothingVolume),
        new ItemDefinition(WoodItem, "Wood", WoodMaterial, Stick, WoodVolume),
        new ItemDefinition(Axe, "Axe", StoneMaterial, Wedge, AxeVolume),
        new ItemDefinition(AppleItem, "Apple", AppleMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(PearItem, "Pear", PearMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(MushroomItem, "Mushroom", MushroomMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(PotatoItem, "Potato", PotatoMaterial, Whole, FoodVolume, FoodHungerRestoredPerUnit),
        new ItemDefinition(GrassItem, "Grass", PlantFibreMaterial, Fibre, GrassVolume),
        new ItemDefinition(StoneItem, "Stone", StoneMaterial, Lump, StoneVolume),
        new ItemDefinition(Basket, "Basket", WoodMaterial, Vessel, BasketVolume, CarryCapacityBonus: BasketCarryCapacityBonus),
        new ItemDefinition(Bag, "Bag", PlantFibreMaterial, Vessel, BagVolume, CarryCapacityBonus: BagCarryCapacityBonus),
    }, materials);

    public static WorldConfiguration CreateConfiguration()
    {
        var materials = CreateMaterialCatalog();

        return new(
            CreateResourceCatalog(),
            CreateSkillCatalog(),
            CreateRecipeCatalog(),
            CreateBuildingCatalog(),
            materials,
            CreateItemCatalog(materials),
            SeasonParameters.Default,
            SimulationRules.Default);
    }

    public static WorldState CreateWorld() => new(CreateConfiguration());
}
